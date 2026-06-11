using System.Text.Json;
using GestionePrenotazioni.Web.Domain;

namespace GestionePrenotazioni.Web.Services;

public sealed record GuidedEventShift(string Name, TimeOnly? StartsAt);

public sealed record GuidedEventSetupResult(
    int CreatedDates,
    int ReusedDates,
    int CreatedShifts,
    int ReusedShifts,
    int CreatedTables,
    Guid? FirstDateId,
    Guid? FirstShiftId);

public sealed class AppStore
{
    private readonly object gate = new();
    private readonly PasswordService passwordService;
    private readonly string dataFilePath;
    private AppData data = new();

    public AppStore(IWebHostEnvironment environment, IConfiguration configuration, PasswordService passwordService)
    {
        this.passwordService = passwordService;
        var dataPath = configuration["AppData:Path"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataPath);
        dataFilePath = Path.Combine(dataPath, "gestione-prenotazioni.json");
        LoadOrSeed();
    }

    public IReadOnlyList<Organization> Organizations => Snapshot(data.Organizations);
    public IReadOnlyList<ApplicationUser> Users => Snapshot(data.Users);
    public IReadOnlyList<FairEvent> Events => Snapshot(data.Events);
    public IReadOnlyList<EventDate> Dates => Snapshot(data.Dates);
    public IReadOnlyList<Shift> Shifts => Snapshot(data.Shifts);
    public IReadOnlyList<DiningTable> Tables => Snapshot(data.Tables);
    public IReadOnlyList<Reservation> Reservations => Snapshot(data.Reservations);
    public IReadOnlyList<Domain.TableAssignment> Assignments => Snapshot(data.Assignments);
    public IReadOnlyList<AuditLogEntry> AuditLogs => Snapshot(data.AuditLogs);

    public ApplicationUser? ValidateUser(string userName, string password)
    {
        lock (gate)
        {
            var user = data.Users.FirstOrDefault(item =>
                item.IsActive &&
                item.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            return user is not null && passwordService.Verify(password, user.PasswordHash)
                ? user
                : null;
        }
    }

    public bool UserCanAccessOrganization(ApplicationUser user, Guid organizationId)
    {
        return user.Role == UserRole.Admin || user.OrganizationId == organizationId;
    }

    public bool UserCanAccessEvent(ApplicationUser user, Guid eventId)
    {
        lock (gate)
        {
            return TryOrganizationIdForEvent(eventId, out var organizationId) &&
                UserCanAccessOrganization(user, organizationId);
        }
    }

    public bool UserCanAccessEventDate(ApplicationUser user, Guid eventDateId)
    {
        lock (gate)
        {
            return TryOrganizationIdForEventDate(eventDateId, out var organizationId) &&
                UserCanAccessOrganization(user, organizationId);
        }
    }

    public bool UserCanAccessShift(ApplicationUser user, Guid shiftId)
    {
        lock (gate)
        {
            return TryOrganizationIdForShift(shiftId, out var organizationId) &&
                UserCanAccessOrganization(user, organizationId);
        }
    }

    public bool UserCanAccessTable(ApplicationUser user, Guid tableId)
    {
        lock (gate)
        {
            return TryOrganizationIdForTable(tableId, out var organizationId) &&
                UserCanAccessOrganization(user, organizationId);
        }
    }

    public bool UserCanAccessReservation(ApplicationUser user, Guid reservationId)
    {
        lock (gate)
        {
            return TryOrganizationIdForReservation(reservationId, out var organizationId) &&
                UserCanAccessOrganization(user, organizationId);
        }
    }

    public bool UserCanAccessUser(ApplicationUser user, Guid targetUserId)
    {
        lock (gate)
        {
            var target = data.Users.FirstOrDefault(item => item.Id == targetUserId);
            return target is not null && UserCanAccessOrganization(user, target.OrganizationId);
        }
    }

    public void RecordLoginAttempt(string userName, bool succeeded, ApplicationUser? user, string? remoteAddress = null, string? userAgent = null)
    {
        lock (gate)
        {
            var auditUser = user ?? data.Users.FirstOrDefault(item =>
                item.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            var organizationId = auditUser?.OrganizationId ?? data.Organizations.FirstOrDefault()?.Id ?? Guid.Empty;
            var userId = auditUser?.Id ?? Guid.Empty;
            var safeUserName = string.IsNullOrWhiteSpace(userName) ? "(vuoto)" : userName.Trim();
            var details = $"Utente: {safeUserName}";
            if (!string.IsNullOrWhiteSpace(remoteAddress))
            {
                details += $" | IP: {remoteAddress.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                details += $" | User-Agent: {userAgent.Trim()}";
            }

            WriteAudit(
                organizationId,
                userId,
                "Login",
                userId,
                succeeded ? "login-success" : "login-failed",
                details);
            Save();
        }
    }

    public Organization AddOrganization(string name, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome organizzazione e obbligatorio.");
            EnsureUniqueOrganizationName(cleanName);
            var organization = new Organization { Name = cleanName };
            data.Organizations.Add(organization);
            WriteAudit(organization.Id, userId, nameof(Organization), organization.Id, "create", organization.Name);
            Save();
            return organization;
        }
    }

    public void RenameOrganization(Guid organizationId, string name, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome organizzazione e obbligatorio.");
            var organization = data.Organizations.First(item => item.Id == organizationId);
            EnsureUniqueOrganizationName(cleanName, organizationId);
            var before = organization.Name;
            organization.Name = cleanName;
            WriteAudit(organization.Id, userId, nameof(Organization), organization.Id, "update", BeforeAfter(before, organization.Name));
            Save();
        }
    }

    public bool DeleteOrganization(Guid organizationId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var organization = data.Organizations.FirstOrDefault(item => item.Id == organizationId);
            if (organization is null)
            {
                error = "Organizzazione non trovata.";
                return false;
            }

            if (data.Organizations.Count <= 1)
            {
                error = "Non puoi cancellare l'ultima organizzazione.";
                return false;
            }

            if (data.Users.Any(item => item.Id == userId && item.OrganizationId == organizationId))
            {
                error = "Non puoi cancellare l'organizzazione con cui hai effettuato l'accesso.";
                return false;
            }

            var summary = new CascadeDeleteSummary();
            foreach (var eventId in data.Events.Where(item => item.OrganizationId == organizationId).Select(item => item.Id).ToArray())
            {
                DeleteEventGraph(eventId, summary);
            }

            summary.Users += data.Users.RemoveAll(item => item.OrganizationId == organizationId);
            data.Organizations.Remove(organization);
            summary.Organizations++;

            WriteAudit(organizationId, userId, nameof(Organization), organization.Id, "delete", $"{organization.Name}. {summary}");
            Save();
            return true;
        }
    }

    public ApplicationUser AddUser(Guid organizationId, string userName, string password, UserRole role, string? displayName, Guid userId)
    {
        lock (gate)
        {
            var cleanUserName = CleanRequired(userName, "Il nome utente e obbligatorio.");
            _ = CleanRequired(password, "La password e obbligatoria.");
            if (!data.Organizations.Any(item => item.Id == organizationId))
            {
                throw new InvalidOperationException("Organizzazione non trovata.");
            }

            EnsureUniqueUserName(cleanUserName);
            var user = new ApplicationUser
            {
                OrganizationId = organizationId,
                UserName = cleanUserName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                PasswordHash = passwordService.Hash(password),
                Role = role
            };
            data.Users.Add(user);
            WriteAudit(organizationId, userId, nameof(ApplicationUser), user.Id, "create", $"{user.UserName} ({user.Role})");
            Save();
            return user;
        }
    }

    public void SetUserActive(Guid targetUserId, bool isActive, Guid userId)
    {
        lock (gate)
        {
            var user = data.Users.First(item => item.Id == targetUserId);
            var before = user.IsActive ? "attivo" : "disattivato";
            user.IsActive = isActive;
            var after = user.IsActive ? "attivo" : "disattivato";
            WriteAudit(user.OrganizationId, userId, nameof(ApplicationUser), user.Id, isActive ? "activate" : "deactivate", $"{user.UserName}. {BeforeAfter(before, after)}");
            Save();
        }
    }

    public bool DeleteUser(Guid targetUserId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            if (targetUserId == userId)
            {
                error = "Non puoi cancellare l'utente con cui hai effettuato l'accesso.";
                return false;
            }

            var user = data.Users.FirstOrDefault(item => item.Id == targetUserId);
            if (user is null)
            {
                error = "Utente non trovato.";
                return false;
            }

            if (user.Role == UserRole.Admin &&
                data.Users.Count(item => item.OrganizationId == user.OrganizationId && item.Role == UserRole.Admin && item.IsActive && item.Id != targetUserId) == 0)
            {
                error = "Non puoi cancellare l'ultimo admin attivo dell'organizzazione.";
                return false;
            }

            data.Users.Remove(user);
            WriteAudit(user.OrganizationId, userId, nameof(ApplicationUser), user.Id, "delete", $"{user.UserName} ({user.Role})");
            Save();
            return true;
        }
    }

    public FairEvent AddEvent(Guid organizationId, string name, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome evento e obbligatorio.");
            if (!data.Organizations.Any(item => item.Id == organizationId))
            {
                throw new InvalidOperationException("Organizzazione non trovata.");
            }

            EnsureUniqueEventName(organizationId, cleanName);
            var fairEvent = new FairEvent { OrganizationId = organizationId, Name = cleanName };
            data.Events.Add(fairEvent);
            WriteAudit(organizationId, userId, nameof(FairEvent), fairEvent.Id, "create", fairEvent.Name);
            Save();
            return fairEvent;
        }
    }

    public void RenameEvent(Guid eventId, string name, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome evento e obbligatorio.");
            var fairEvent = data.Events.First(item => item.Id == eventId);
            EnsureUniqueEventName(fairEvent.OrganizationId, cleanName, eventId);
            var before = fairEvent.Name;
            fairEvent.Name = cleanName;
            WriteAudit(fairEvent.OrganizationId, userId, nameof(FairEvent), eventId, "update", BeforeAfter(before, fairEvent.Name));
            Save();
        }
    }

    public void SetEventArchived(Guid eventId, bool isArchived, Guid userId)
    {
        lock (gate)
        {
            var fairEvent = data.Events.First(item => item.Id == eventId);
            var before = fairEvent.IsArchived ? "archiviato" : "attivo";
            fairEvent.IsArchived = isArchived;
            if (isArchived)
            {
                fairEvent.IsDefault = false;
            }
            var after = fairEvent.IsArchived ? "archiviato" : "attivo";
            WriteAudit(fairEvent.OrganizationId, userId, nameof(FairEvent), eventId, isArchived ? "archive" : "restore", $"{fairEvent.Name}. {BeforeAfter(before, after)}");
            Save();
        }
    }

    public void SetDefaultEvent(Guid eventId, Guid userId)
    {
        lock (gate)
        {
            var fairEvent = data.Events.First(item => item.Id == eventId);
            if (fairEvent.IsArchived)
            {
                throw new InvalidOperationException("Non puoi impostare come predefinito un evento archiviato.");
            }

            foreach (var item in data.Events.Where(item => item.OrganizationId == fairEvent.OrganizationId))
            {
                item.IsDefault = item.Id == eventId;
            }

            WriteAudit(fairEvent.OrganizationId, userId, nameof(FairEvent), eventId, "set-default", fairEvent.Name);
            Save();
        }
    }

    public bool DeleteEvent(Guid eventId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var fairEvent = data.Events.FirstOrDefault(item => item.Id == eventId);
            if (fairEvent is null)
            {
                error = "Evento non trovato.";
                return false;
            }

            var organizationId = fairEvent.OrganizationId;
            var name = fairEvent.Name;
            var summary = new CascadeDeleteSummary();
            DeleteEventGraph(eventId, summary);
            WriteAudit(organizationId, userId, nameof(FairEvent), eventId, "delete", $"{name}. {summary}");
            Save();
            return true;
        }
    }

    public EventDate AddEventDate(Guid eventId, DateOnly date, Guid userId)
    {
        lock (gate)
        {
            if (!data.Events.Any(item => item.Id == eventId))
            {
                throw new InvalidOperationException("Evento non trovato.");
            }

            EnsureUniqueEventDate(eventId, date);
            var eventDate = new EventDate { EventId = eventId, Date = date };
            data.Dates.Add(eventDate);
            WriteAudit(OrganizationIdForEvent(eventId), userId, nameof(EventDate), eventDate.Id, "create", eventDate.Date.ToString("yyyy-MM-dd"));
            Save();
            return eventDate;
        }
    }

    public GuidedEventSetupResult ConfigureEventSchedule(
        Guid eventId,
        DateOnly startsOn,
        DateOnly endsOn,
        IReadOnlyList<GuidedEventShift> shifts,
        int tablesPerShift,
        int tableCapacity,
        Guid userId)
    {
        lock (gate)
        {
            if (!data.Events.Any(item => item.Id == eventId))
            {
                throw new InvalidOperationException("Evento non trovato.");
            }

            if (endsOn < startsOn)
            {
                throw new InvalidOperationException("La data finale deve essere uguale o successiva alla data iniziale.");
            }

            var dayCount = endsOn.DayNumber - startsOn.DayNumber + 1;
            if (dayCount > 45)
            {
                throw new InvalidOperationException("La configurazione guidata accetta al massimo 45 giorni alla volta.");
            }

            if (tablesPerShift <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tablesPerShift), "Il numero di tavoli per turno deve essere maggiore di 0.");
            }

            if (tableCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tableCapacity), "La capienza deve essere maggiore di 0.");
            }

            var cleanShifts = CleanGuidedShifts(shifts);
            var organizationId = OrganizationIdForEvent(eventId);
            var createdDates = 0;
            var reusedDates = 0;
            var createdShifts = 0;
            var reusedShifts = 0;
            var createdTables = 0;
            Guid? firstDateId = null;
            Guid? firstShiftId = null;

            for (var current = startsOn; current.DayNumber <= endsOn.DayNumber; current = current.AddDays(1))
            {
                var eventDate = data.Dates.FirstOrDefault(item => item.EventId == eventId && item.Date == current);
                if (eventDate is null)
                {
                    eventDate = new EventDate { EventId = eventId, Date = current };
                    data.Dates.Add(eventDate);
                    createdDates++;
                }
                else
                {
                    reusedDates++;
                }

                firstDateId ??= eventDate.Id;

                foreach (var template in cleanShifts)
                {
                    var shift = data.Shifts.FirstOrDefault(item =>
                        item.EventDateId == eventDate.Id &&
                        item.Name.Equals(template.Name, StringComparison.OrdinalIgnoreCase));

                    if (shift is null)
                    {
                        shift = new Shift
                        {
                            EventDateId = eventDate.Id,
                            Name = template.Name,
                            StartsAt = template.StartsAt
                        };
                        data.Shifts.Add(shift);
                        createdShifts++;
                    }
                    else
                    {
                        reusedShifts++;
                    }

                    firstShiftId ??= shift.Id;
                    var existingTables = data.Tables.Count(item => item.ShiftId == shift.Id);
                    var missingTables = Math.Max(0, tablesPerShift - existingTables);
                    if (missingTables > 0)
                    {
                        AddMissingTables(shift.Id, missingTables, tableCapacity);
                        createdTables += missingTables;
                    }
                }
            }

            var details = $"Date create: {createdDates}, date esistenti: {reusedDates}, turni creati: {createdShifts}, turni esistenti: {reusedShifts}, tavoli aggiunti: {createdTables}";
            WriteAudit(organizationId, userId, nameof(FairEvent), eventId, "guided-setup", details);
            Save();
            return new GuidedEventSetupResult(createdDates, reusedDates, createdShifts, reusedShifts, createdTables, firstDateId, firstShiftId);
        }

        void AddMissingTables(Guid shiftId, int count, int capacity)
        {
            var usedCodes = data.Tables
                .Where(table => table.ShiftId == shiftId)
                .Select(table => table.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextNumber = 1;

            for (var index = 0; index < count; index++)
            {
                string code;
                do
                {
                    code = $"{nextNumber:00}";
                    nextNumber++;
                }
                while (!usedCodes.Add(code));

                data.Tables.Add(new DiningTable
                {
                    ShiftId = shiftId,
                    Code = code,
                    Capacity = capacity
                });
            }
        }
    }

    public void UpdateEventDate(Guid eventDateId, DateOnly date, Guid userId)
    {
        lock (gate)
        {
            var eventDate = data.Dates.First(item => item.Id == eventDateId);
            EnsureUniqueEventDate(eventDate.EventId, date, eventDateId);
            var before = eventDate.Date;
            eventDate.Date = date;
            WriteAudit(OrganizationIdForEvent(eventDate.EventId), userId, nameof(EventDate), eventDateId, "update", BeforeAfter(before.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM-dd")));
            Save();
        }
    }

    public bool DeleteEventDate(Guid eventDateId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var eventDate = data.Dates.FirstOrDefault(item => item.Id == eventDateId);
            if (eventDate is null)
            {
                error = "Data non trovata.";
                return false;
            }

            var organizationId = OrganizationIdForEvent(eventDate.EventId);
            var date = eventDate.Date;
            var summary = new CascadeDeleteSummary();
            DeleteDateGraph(eventDateId, summary);
            WriteAudit(organizationId, userId, nameof(EventDate), eventDateId, "delete", $"{date:yyyy-MM-dd}. {summary}");
            Save();
            return true;
        }
    }

    public Shift AddShift(Guid eventDateId, string name, TimeOnly? startsAt, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome turno e obbligatorio.");
            if (!data.Dates.Any(item => item.Id == eventDateId))
            {
                throw new InvalidOperationException("Data non trovata.");
            }

            EnsureUniqueShiftName(eventDateId, cleanName);
            var shift = new Shift { EventDateId = eventDateId, Name = cleanName, StartsAt = startsAt };
            data.Shifts.Add(shift);
            WriteAudit(OrganizationIdForEventDate(eventDateId), userId, nameof(Shift), shift.Id, "create", shift.Name);
            Save();
            return shift;
        }
    }

    public void UpdateShift(Guid shiftId, string name, TimeOnly? startsAt, Guid userId)
    {
        lock (gate)
        {
            var cleanName = CleanRequired(name, "Il nome turno e obbligatorio.");
            var shift = data.Shifts.First(item => item.Id == shiftId);
            EnsureUniqueShiftName(shift.EventDateId, cleanName, shiftId);
            var before = $"{shift.Name} {shift.StartsAt}";
            shift.Name = cleanName;
            shift.StartsAt = startsAt;
            WriteAudit(OrganizationIdForShift(shiftId), userId, nameof(Shift), shiftId, "update", BeforeAfter(before, $"{shift.Name} {shift.StartsAt}"));
            Save();
        }
    }

    public void SetShiftClosed(Guid shiftId, bool isClosed, Guid userId)
    {
        lock (gate)
        {
            var shift = data.Shifts.First(item => item.Id == shiftId);
            var before = shift.IsClosed ? "archiviato" : "aperto";
            shift.IsClosed = isClosed;
            var after = shift.IsClosed ? "archiviato" : "aperto";
            WriteAudit(OrganizationIdForShift(shiftId), userId, nameof(Shift), shiftId, isClosed ? "archive" : "restore", $"{shift.Name}. {BeforeAfter(before, after)}");
            Save();
        }
    }

    public int ArchivePastShifts(Guid eventId, DateTime now, Guid userId)
    {
        lock (gate)
        {
            var dateIds = data.Dates
                .Where(item => item.EventId == eventId)
                .Select(item => item.Id)
                .ToHashSet();
            var shifts = data.Shifts
                .Where(item => dateIds.Contains(item.EventDateId))
                .ToArray();
            var byDate = shifts
                .GroupBy(item => item.EventDateId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var count = 0;

            foreach (var shift in shifts.Where(item => !item.IsClosed))
            {
                var date = data.Dates.First(item => item.Id == shift.EventDateId);
                if (!ShiftIsPast(date, shift, byDate[shift.EventDateId], now))
                {
                    continue;
                }

                shift.IsClosed = true;
                count++;
                WriteAudit(OrganizationIdForEvent(eventId), userId, nameof(Shift), shift.Id, "archive", shift.Name);
            }

            if (count > 0)
            {
                Save();
            }

            return count;
        }
    }

    public bool DeleteShift(Guid shiftId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var shift = data.Shifts.FirstOrDefault(item => item.Id == shiftId);
            if (shift is null)
            {
                error = "Turno non trovato.";
                return false;
            }

            var organizationId = OrganizationIdForEventDate(shift.EventDateId);
            var name = shift.Name;
            var summary = new CascadeDeleteSummary();
            DeleteShiftGraph(shiftId, summary);
            WriteAudit(organizationId, userId, nameof(Shift), shiftId, "delete", $"{name}. {summary}");
            Save();
            return true;
        }
    }

    public void AddTables(Guid shiftId, int count, int capacity, Guid userId)
    {
        lock (gate)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Il numero di tavoli deve essere maggiore di 0.");
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "La capienza deve essere maggiore di 0.");
            }

            if (!data.Shifts.Any(item => item.Id == shiftId))
            {
                throw new InvalidOperationException("Turno non trovato.");
            }

            var usedCodes = data.Tables
                .Where(table => table.ShiftId == shiftId)
                .Select(table => table.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextNumber = 1;
            for (var index = 0; index < count; index++)
            {
                string code;
                do
                {
                    code = $"{nextNumber:00}";
                    nextNumber++;
                }
                while (!usedCodes.Add(code));

                var table = new DiningTable
                {
                    ShiftId = shiftId,
                    Code = code,
                    Capacity = capacity
                };
                data.Tables.Add(table);
                WriteAudit(OrganizationIdForShift(shiftId), userId, nameof(DiningTable), table.Id, "create", $"{table.Code} ({table.Capacity})");
            }

            Save();
        }
    }

    public bool DeleteTable(Guid tableId, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var table = data.Tables.FirstOrDefault(item => item.Id == tableId);
            if (table is null)
            {
                error = "Tavolo non trovato.";
                return false;
            }

            var organizationId = OrganizationIdForShift(table.ShiftId);
            var code = table.Code;
            var affectedReservationIds = data.Assignments
                .Where(item => item.TableIds.Contains(tableId))
                .SelectMany(item => item.ReservationIds)
                .Distinct()
                .ToArray();

            var removedAssignments = data.Assignments.RemoveAll(item => item.TableIds.Contains(tableId));
            data.Tables.Remove(table);

            ResetReservationStatusWhenUnassigned(affectedReservationIds);

            WriteAudit(organizationId, userId, nameof(DiningTable), tableId, "delete", $"{code}. Assegnazioni rimosse: {removedAssignments}");
            Save();
            return true;
        }
    }

    public void UpdateTable(Guid tableId, string code, int capacity, string? notes, Guid userId)
    {
        lock (gate)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "La capienza deve essere maggiore di 0.");
            }

            var table = data.Tables.First(item => item.Id == tableId);
            var cleanCode = NormalizeTableCode(CleanRequired(code, "Il codice tavolo e obbligatorio."));
            EnsureUniqueTableCode(table.ShiftId, cleanCode, tableId);
            var before = $"{table.Code} ({table.Capacity}) {table.Notes}";
            table.Code = cleanCode;
            table.Capacity = capacity;
            table.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            WriteAudit(OrganizationIdForShift(table.ShiftId), userId, nameof(DiningTable), table.Id, "update", BeforeAfter(before, $"{table.Code} ({table.Capacity}) {table.Notes}"));
            Save();
        }
    }

    public Reservation AddReservation(Guid shiftId, string bookerName, DateOnly date, int partySize, TimeOnly? expectedAt, string? mobilePhone, Guid userId, string? notes = null)
    {
        lock (gate)
        {
            if (partySize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), "Il numero di persone deve essere maggiore di 0.");
            }

            var reservation = new Reservation
            {
                ShiftId = shiftId,
                BookerName = NormalizeBookerName(bookerName),
                Date = date,
                PartySize = partySize,
                ExpectedAt = expectedAt,
                MobilePhone = string.IsNullOrWhiteSpace(mobilePhone) ? null : mobilePhone.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = userId
            };
            data.Reservations.Add(reservation);
            WriteAudit(OrganizationIdForShift(shiftId), userId, nameof(Reservation), reservation.Id, "create", ReservationDetails(reservation));
            Save();
            return reservation;
        }
    }

    public void UpdateReservation(Guid reservationId, string bookerName, int partySize, TimeOnly? expectedAt, string? mobilePhone, Guid userId, string? notes = null)
    {
        lock (gate)
        {
            if (partySize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), "Il numero di persone deve essere maggiore di 0.");
            }

            var reservation = data.Reservations.First(item => item.Id == reservationId);
            var before = ReservationDetails(reservation);
            reservation.BookerName = NormalizeBookerName(bookerName);
            reservation.PartySize = partySize;
            reservation.ExpectedAt = expectedAt;
            reservation.MobilePhone = string.IsNullOrWhiteSpace(mobilePhone) ? null : mobilePhone.Trim();
            reservation.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            reservation.Status = ReservationStatus.Entered;
            RemoveReservationFromAssignments(reservation.Id);
            WriteAudit(OrganizationIdForShift(reservation.ShiftId), userId, nameof(Reservation), reservationId, "update", BeforeAfter(before, ReservationDetails(reservation)));
            Save();
        }
    }

    public void MarkArrived(Guid reservationId, Guid userId)
    {
        SetReservationStatus(reservationId, ReservationStatus.Arrived, userId, "mark-arrived");
    }

    public void MarkNoShow(Guid reservationId, Guid userId)
    {
        SetReservationStatus(reservationId, ReservationStatus.NoShow, userId, "mark-noshow", releaseAssignments: true);
    }

    public void CancelReservation(Guid reservationId, Guid userId)
    {
        SetReservationStatus(reservationId, ReservationStatus.Cancelled, userId, "cancel", releaseAssignments: true);
    }

    public void ReplaceAssignments(Guid shiftId, IReadOnlyList<Domain.TableAssignment> newAssignments, Guid userId)
    {
        if (!ReplaceAssignments(shiftId, newAssignments, userId, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool ReplaceAssignments(Guid shiftId, IReadOnlyList<Domain.TableAssignment> newAssignments, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            if (!data.Shifts.Any(item => item.Id == shiftId))
            {
                error = "Turno non trovato.";
                return false;
            }

            foreach (var candidate in newAssignments)
            {
                if (!AssignmentIsValidForShift(shiftId, candidate, out error))
                {
                    return false;
                }
            }

            var beforeCount = data.Assignments.Count(assignment => assignment.ShiftId == shiftId);
            data.Assignments.RemoveAll(assignment => assignment.ShiftId == shiftId);
            data.Assignments.AddRange(newAssignments.Select(assignment => new Domain.TableAssignment
            {
                ShiftId = shiftId,
                TableIds = assignment.TableIds.Distinct().ToList(),
                ReservationIds = assignment.ReservationIds.Distinct().ToList(),
                Source = assignment.Source
            }));

            foreach (var reservation in data.Reservations.Where(item => item.ShiftId == shiftId && item.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow))
            {
                reservation.Status = newAssignments.Any(assignment => assignment.ReservationIds.Contains(reservation.Id))
                    ? ReservationStatus.Assigned
                    : ReservationStatus.Entered;
            }

            WriteAudit(OrganizationIdForShift(shiftId), userId, nameof(Shift), shiftId, "assign-tables", BeforeAfter($"{beforeCount} assegnazioni", $"{newAssignments.Count} assegnazioni"));
            Save();
            return true;
        }
    }

    public bool AssignReservationManually(Guid reservationId, IReadOnlyList<Guid> tableIds, Guid userId, out string error)
    {
        lock (gate)
        {
            error = string.Empty;
            var reservation = data.Reservations.First(item => item.Id == reservationId);
            if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.NoShow)
            {
                error = "Non puoi assegnare una prenotazione annullata o non presentata.";
                return false;
            }

            var requestedTableIds = tableIds.Distinct().ToArray();
            var selectedTables = data.Tables.Where(table => requestedTableIds.Contains(table.Id)).ToArray();
            if (selectedTables.Length == 0)
            {
                error = "Selezionare almeno un tavolo.";
                return false;
            }

            if (selectedTables.Length != requestedTableIds.Length || selectedTables.Any(table => table.ShiftId != reservation.ShiftId))
            {
                error = "I tavoli selezionati non appartengono al turno della prenotazione.";
                return false;
            }

            var capacity = selectedTables.Sum(table => table.Capacity);
            if (reservation.PartySize > capacity)
            {
                error = "La prenotazione supera la capienza dei tavoli selezionati.";
                return false;
            }

            var orderedTableIds = selectedTables.OrderBy(table => table.Code).Select(table => table.Id).ToList();
            var assignment = data.Assignments.FirstOrDefault(item =>
                item.ShiftId == reservation.ShiftId &&
                item.TableIds.OrderBy(id => id).SequenceEqual(orderedTableIds.OrderBy(id => id)));

            var currentPeople = assignment is null
                ? 0
                : data.Reservations
                .Where(item => assignment.ReservationIds.Contains(item.Id))
                .Where(item => item.Id != reservationId)
                .Sum(item => item.PartySize);

            if (currentPeople + reservation.PartySize > capacity)
            {
                error = "La capienza dei tavoli selezionati non basta per le prenotazioni gia presenti.";
                return false;
            }

            RemoveReservationFromAssignments(reservationId);
            assignment = data.Assignments.FirstOrDefault(item =>
                item.ShiftId == reservation.ShiftId &&
                item.TableIds.OrderBy(id => id).SequenceEqual(orderedTableIds.OrderBy(id => id)));

            if (assignment is null)
            {
                assignment = new Domain.TableAssignment
                {
                    ShiftId = reservation.ShiftId,
                    TableIds = orderedTableIds,
                    Source = AssignmentSource.Manual
                };
                data.Assignments.Add(assignment);
            }

            assignment.ReservationIds.Add(reservationId);
            assignment.Source = AssignmentSource.Manual;
            reservation.Status = ReservationStatus.Assigned;
            WriteAudit(OrganizationIdForShift(reservation.ShiftId), userId, nameof(Domain.TableAssignment), assignment.Id, "manual-assign", $"{reservation.BookerName}. Tavoli: {string.Join(", ", selectedTables.Select(table => table.Code))}");
            Save();
            return true;
        }
    }

    private void SetReservationStatus(Guid reservationId, ReservationStatus status, Guid userId, string action, bool releaseAssignments = false)
    {
        lock (gate)
        {
            var reservation = data.Reservations.First(item => item.Id == reservationId);
            var before = reservation.Status;
            reservation.Status = status;
            if (releaseAssignments)
            {
                RemoveReservationFromAssignments(reservationId);
            }

            WriteAudit(OrganizationIdForShift(reservation.ShiftId), userId, nameof(Reservation), reservation.Id, action, BeforeAfter(before.ToString(), reservation.Status.ToString()));
            Save();
        }
    }

    private void RemoveReservationFromAssignments(Guid reservationId)
    {
        foreach (var assignment in data.Assignments)
        {
            assignment.ReservationIds.Remove(reservationId);
        }

        data.Assignments.RemoveAll(assignment => assignment.ReservationIds.Count == 0);
    }

    private void ResetReservationStatusWhenUnassigned(IReadOnlyList<Guid> reservationIds)
    {
        foreach (var reservation in data.Reservations.Where(item => reservationIds.Contains(item.Id)))
        {
            if (!data.Assignments.Any(assignment => assignment.ReservationIds.Contains(reservation.Id)) &&
                reservation.Status == ReservationStatus.Assigned)
            {
                reservation.Status = ReservationStatus.Entered;
            }
        }
    }

    private void DeleteEventGraph(Guid eventId, CascadeDeleteSummary summary)
    {
        foreach (var dateId in data.Dates.Where(item => item.EventId == eventId).Select(item => item.Id).ToArray())
        {
            DeleteDateGraph(dateId, summary);
        }

        summary.Events += data.Events.RemoveAll(item => item.Id == eventId);
    }

    private void DeleteDateGraph(Guid eventDateId, CascadeDeleteSummary summary)
    {
        foreach (var shiftId in data.Shifts.Where(item => item.EventDateId == eventDateId).Select(item => item.Id).ToArray())
        {
            DeleteShiftGraph(shiftId, summary);
        }

        summary.Dates += data.Dates.RemoveAll(item => item.Id == eventDateId);
    }

    private void DeleteShiftGraph(Guid shiftId, CascadeDeleteSummary summary)
    {
        var reservationIds = data.Reservations
            .Where(item => item.ShiftId == shiftId)
            .Select(item => item.Id)
            .ToHashSet();

        summary.Assignments += data.Assignments.RemoveAll(item =>
            item.ShiftId == shiftId ||
            item.ReservationIds.Any(reservationIds.Contains));
        summary.Reservations += data.Reservations.RemoveAll(item => item.ShiftId == shiftId);
        summary.Tables += data.Tables.RemoveAll(item => item.ShiftId == shiftId);
        summary.Shifts += data.Shifts.RemoveAll(item => item.Id == shiftId);
    }

    private static bool ShiftIsPast(EventDate date, Shift shift, IReadOnlyList<Shift> shiftsOnDate, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (date.Date < today)
        {
            return true;
        }

        if (date.Date > today || shift.StartsAt is null)
        {
            return false;
        }

        var currentTime = TimeOnly.FromDateTime(now);
        return shiftsOnDate.Any(item =>
            item.Id != shift.Id &&
            item.StartsAt.HasValue &&
            item.StartsAt.Value > shift.StartsAt.Value &&
            item.StartsAt.Value <= currentTime);
    }

    private void WriteAudit(Guid organizationId, Guid userId, string entityName, Guid entityId, string action, string? details)
    {
        data.AuditLogs.Add(new AuditLogEntry
        {
            OrganizationId = organizationId,
            UserId = userId,
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            Details = details
        });
    }

    private bool AssignmentIsValidForShift(Guid shiftId, Domain.TableAssignment assignment, out string error)
    {
        error = string.Empty;
        if (assignment.ShiftId != shiftId)
        {
            error = "Un'assegnazione punta a un turno diverso.";
            return false;
        }

        var tableIds = assignment.TableIds.Distinct().ToArray();
        var selectedTables = data.Tables.Where(table => tableIds.Contains(table.Id)).ToArray();
        if (selectedTables.Length != tableIds.Length || selectedTables.Any(table => table.ShiftId != shiftId))
        {
            error = "Un'assegnazione contiene tavoli non validi per il turno.";
            return false;
        }

        var reservationIds = assignment.ReservationIds.Distinct().ToArray();
        var reservations = data.Reservations.Where(reservation => reservationIds.Contains(reservation.Id)).ToArray();
        if (reservations.Length != reservationIds.Length ||
            reservations.Any(reservation => reservation.ShiftId != shiftId || reservation.Status is ReservationStatus.Cancelled or ReservationStatus.NoShow))
        {
            error = "Un'assegnazione contiene prenotazioni non valide per il turno.";
            return false;
        }

        if (reservations.Sum(reservation => reservation.PartySize) > selectedTables.Sum(table => table.Capacity))
        {
            error = "Un'assegnazione supera la capienza dei tavoli.";
            return false;
        }

        return true;
    }

    private static string ReservationDetails(Reservation reservation)
    {
        var notes = string.IsNullOrWhiteSpace(reservation.Notes) ? string.Empty : $"Note: {reservation.Notes}";
        return $"{reservation.BookerName} ({reservation.PartySize}) {reservation.ExpectedAt?.ToString("HH:mm")} {reservation.MobilePhone} {reservation.Status} {notes}".Trim();
    }

    private static string BeforeAfter(string before, string after)
    {
        return $"Prima: {before} | Dopo: {after}";
    }

    private static string CleanRequired(string value, string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(error);
        }

        return value.Trim();
    }

    private static string NormalizeBookerName(string bookerName)
    {
        return CleanRequired(bookerName, "Il nome della prenotazione e obbligatorio.").ToUpperInvariant();
    }

    private static IReadOnlyList<GuidedEventShift> CleanGuidedShifts(IReadOnlyList<GuidedEventShift> shifts)
    {
        if (shifts.Count == 0)
        {
            throw new InvalidOperationException("Inserisci almeno un turno predefinito.");
        }

        var cleanShifts = new List<GuidedEventShift>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shift in shifts)
        {
            var cleanName = CleanRequired(shift.Name, "Il nome turno e obbligatorio.");
            if (!names.Add(cleanName))
            {
                throw new InvalidOperationException("I turni predefiniti devono avere nomi diversi.");
            }

            cleanShifts.Add(new GuidedEventShift(cleanName, shift.StartsAt));
        }

        return cleanShifts;
    }

    private void EnsureUniqueOrganizationName(string name, Guid? excludedId = null)
    {
        if (data.Organizations.Any(item =>
            item.Id != excludedId &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste gia un'organizzazione con questo nome.");
        }
    }

    private void EnsureUniqueUserName(string userName)
    {
        if (data.Users.Any(item => item.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste gia un utente con questo nome.");
        }
    }

    private void EnsureUniqueEventName(Guid organizationId, string name, Guid? excludedId = null)
    {
        if (data.Events.Any(item =>
            item.Id != excludedId &&
            item.OrganizationId == organizationId &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste gia un evento con questo nome nell'organizzazione.");
        }
    }

    private void EnsureUniqueEventDate(Guid eventId, DateOnly date, Guid? excludedId = null)
    {
        if (data.Dates.Any(item =>
            item.Id != excludedId &&
            item.EventId == eventId &&
            item.Date == date))
        {
            throw new InvalidOperationException("Esiste gia questa data per l'evento selezionato.");
        }
    }

    private void EnsureUniqueShiftName(Guid eventDateId, string name, Guid? excludedId = null)
    {
        if (data.Shifts.Any(item =>
            item.Id != excludedId &&
            item.EventDateId == eventDateId &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste gia un turno con questo nome nella data selezionata.");
        }
    }

    private void EnsureUniqueTableCode(Guid shiftId, string code, Guid? excludedId = null)
    {
        if (data.Tables.Any(item =>
            item.Id != excludedId &&
            item.ShiftId == shiftId &&
            item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste gia un tavolo con questo codice nel turno selezionato.");
        }
    }

    private Guid OrganizationIdForEvent(Guid eventId)
    {
        return data.Events.First(item => item.Id == eventId).OrganizationId;
    }

    private Guid OrganizationIdForEventDate(Guid eventDateId)
    {
        var date = data.Dates.First(item => item.Id == eventDateId);
        return OrganizationIdForEvent(date.EventId);
    }

    private Guid OrganizationIdForShift(Guid shiftId)
    {
        var shift = data.Shifts.First(item => item.Id == shiftId);
        return OrganizationIdForEventDate(shift.EventDateId);
    }

    private bool TryOrganizationIdForEvent(Guid eventId, out Guid organizationId)
    {
        var fairEvent = data.Events.FirstOrDefault(item => item.Id == eventId);
        organizationId = fairEvent?.OrganizationId ?? Guid.Empty;
        return fairEvent is not null;
    }

    private bool TryOrganizationIdForEventDate(Guid eventDateId, out Guid organizationId)
    {
        var date = data.Dates.FirstOrDefault(item => item.Id == eventDateId);
        if (date is null)
        {
            organizationId = Guid.Empty;
            return false;
        }

        return TryOrganizationIdForEvent(date.EventId, out organizationId);
    }

    private bool TryOrganizationIdForShift(Guid shiftId, out Guid organizationId)
    {
        var shift = data.Shifts.FirstOrDefault(item => item.Id == shiftId);
        if (shift is null)
        {
            organizationId = Guid.Empty;
            return false;
        }

        return TryOrganizationIdForEventDate(shift.EventDateId, out organizationId);
    }

    private bool TryOrganizationIdForTable(Guid tableId, out Guid organizationId)
    {
        var table = data.Tables.FirstOrDefault(item => item.Id == tableId);
        if (table is null)
        {
            organizationId = Guid.Empty;
            return false;
        }

        return TryOrganizationIdForShift(table.ShiftId, out organizationId);
    }

    private bool TryOrganizationIdForReservation(Guid reservationId, out Guid organizationId)
    {
        var reservation = data.Reservations.FirstOrDefault(item => item.Id == reservationId);
        if (reservation is null)
        {
            organizationId = Guid.Empty;
            return false;
        }

        return TryOrganizationIdForShift(reservation.ShiftId, out organizationId);
    }

    private void LoadOrSeed()
    {
        if (File.Exists(dataFilePath))
        {
            var json = File.ReadAllText(dataFilePath);
            data = JsonSerializer.Deserialize<AppData>(json, JsonOptions()) ?? new AppData();
        }

        if (data.Organizations.Count == 0)
        {
            Seed();
            Save();
        }
        else if (NormalizeLoadedData())
        {
            Save();
        }
    }

    private bool NormalizeLoadedData()
    {
        var changed = false;
        foreach (var table in data.Tables)
        {
            var normalizedCode = NormalizeTableCode(table.Code);
            if (normalizedCode != table.Code &&
                !data.Tables.Any(other =>
                    other.Id != table.Id &&
                    other.ShiftId == table.ShiftId &&
                    other.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase)))
            {
                table.Code = normalizedCode;
                changed = true;
            }
        }

        foreach (var reservation in data.Reservations.Where(item => !string.IsNullOrWhiteSpace(item.BookerName)))
        {
            var normalizedName = reservation.BookerName.Trim().ToUpperInvariant();
            if (normalizedName != reservation.BookerName)
            {
                reservation.BookerName = normalizedName;
                changed = true;
            }
        }

        return changed;
    }

    private static string NormalizeTableCode(string code)
    {
        var cleanCode = code.Trim();
        return cleanCode.Length > 1 &&
            cleanCode[0] is 'T' or 't' &&
            int.TryParse(cleanCode[1..], out _)
                ? cleanCode[1..]
                : cleanCode;
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(data, JsonOptions());
        File.WriteAllText(dataFilePath, json);
    }

    private void Seed()
    {
        var organization = new Organization { Name = "Demo Sagra" };
        var secondOrganization = new Organization { Name = "Pro Loco Demo" };
        data.Organizations.AddRange([organization, secondOrganization]);

        data.Users.AddRange([
            new ApplicationUser { OrganizationId = organization.Id, UserName = "admin", DisplayName = "Admin", PasswordHash = passwordService.Hash("admin"), Role = UserRole.Admin },
            new ApplicationUser { OrganizationId = organization.Id, UserName = "prenotazioni", DisplayName = "Prenotazioni", PasswordHash = passwordService.Hash("prenotazioni"), Role = UserRole.BookingOperator },
            new ApplicationUser { OrganizationId = organization.Id, UserName = "accoglienza", DisplayName = "Accoglienza", PasswordHash = passwordService.Hash("accoglienza"), Role = UserRole.ReceptionOperator }
        ]);

        var fairEvent = new FairEvent { OrganizationId = organization.Id, Name = "Sagra Demo" };
        data.Events.Add(fairEvent);

        var date = new EventDate { EventId = fairEvent.Id, Date = DateOnly.FromDateTime(DateTime.Today) };
        data.Dates.Add(date);

        var lunch = new Shift { EventDateId = date.Id, Name = "Pranzo", StartsAt = new TimeOnly(12, 30) };
        var dinner = new Shift { EventDateId = date.Id, Name = "Cena", StartsAt = new TimeOnly(19, 30) };
        data.Shifts.AddRange([lunch, dinner]);

        for (var index = 1; index <= 20; index++)
        {
            data.Tables.Add(new DiningTable { ShiftId = dinner.Id, Code = $"{index:00}", Capacity = 8 });
        }

        data.Reservations.AddRange([
            new Reservation { ShiftId = dinner.Id, BookerName = "ROSSI", Date = date.Date, PartySize = 4, MobilePhone = "3330000001" },
            new Reservation { ShiftId = dinner.Id, BookerName = "BIANCHI", Date = date.Date, PartySize = 2 },
            new Reservation { ShiftId = dinner.Id, BookerName = "VERDI", Date = date.Date, PartySize = 12 },
            new Reservation { ShiftId = dinner.Id, BookerName = "NERI", Date = date.Date, PartySize = 3 },
            new Reservation { ShiftId = dinner.Id, BookerName = "GIALLI", Date = date.Date, PartySize = 3 }
        ]);
    }

    private IReadOnlyList<T> Snapshot<T>(List<T> list)
    {
        lock (gate)
        {
            return list.ToArray();
        }
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions { WriteIndented = true };
    }

    private sealed class AppData
    {
        public List<Organization> Organizations { get; set; } = [];
        public List<ApplicationUser> Users { get; set; } = [];
        public List<FairEvent> Events { get; set; } = [];
        public List<EventDate> Dates { get; set; } = [];
        public List<Shift> Shifts { get; set; } = [];
        public List<DiningTable> Tables { get; set; } = [];
        public List<Reservation> Reservations { get; set; } = [];
        public List<Domain.TableAssignment> Assignments { get; set; } = [];
        public List<AuditLogEntry> AuditLogs { get; set; } = [];
    }

    private sealed class CascadeDeleteSummary
    {
        public int Organizations { get; set; }
        public int Events { get; set; }
        public int Dates { get; set; }
        public int Shifts { get; set; }
        public int Tables { get; set; }
        public int Reservations { get; set; }
        public int Assignments { get; set; }
        public int Users { get; set; }

        public override string ToString()
        {
            return $"Rimossi: organizzazioni {Organizations}, eventi {Events}, date {Dates}, turni {Shifts}, tavoli {Tables}, prenotazioni {Reservations}, assegnazioni {Assignments}, utenti {Users}.";
        }
    }
}
