using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using GestionePrenotazioni.Web.Domain;
using GestionePrenotazioni.Web.Services;
using GestionePrenotazioni.Web.Services.TableAssignment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionePrenotazioni.Web.Pages;

public sealed class IndexModel(AppStore store, ITableAssignmentService assignmentService, ExportService exportService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? SelectedOrganizationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedEventId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedDateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedShiftId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchText { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FocusTarget { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ConfigSection { get; set; }

    [BindProperty]
    public ReservationInputModel ReservationInput { get; set; } = new();

    [BindProperty]
    public TableInputModel TableInput { get; set; } = new();

    [BindProperty]
    public ShiftInputModel ShiftInput { get; set; } = new();

    [BindProperty]
    public EventInputModel EventInput { get; set; } = new();

    [BindProperty]
    public DateInputModel DateInput { get; set; } = new();

    [BindProperty]
    public EventWizardInputModel EventWizardInput { get; set; } = new();

    [BindProperty]
    public OrganizationInputModel OrganizationInput { get; set; } = new();

    [BindProperty]
    public UserInputModel UserInput { get; set; } = new();

    [TempData]
    public string? FlashMessage { get; set; }

    [TempData]
    public string? FlashKind { get; set; }

    [TempData]
    public string? FlashDescription { get; set; }

    public IReadOnlyList<SelectListItem> OrganizationOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> EventOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> DateOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ShiftOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> RoleOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ReservationTimeOptions { get; private set; } = [];
    public string OrganizationName { get; private set; } = string.Empty;
    public ApplicationUser CurrentUser { get; private set; } = null!;
    public bool CanManageSetup => CurrentUser.Role == UserRole.Admin;
    public bool CanManageReservations => CurrentUser.Role is UserRole.Admin or UserRole.BookingOperator;
    public bool CanReception => CurrentUser.Role is UserRole.Admin or UserRole.ReceptionOperator;
    public bool IsReceptionPage => CurrentPageIs("/Reception");
    public bool IsBookingsPage => CurrentPageIs("/Bookings");
    public bool IsCompactWorkflowPage => IsReceptionPage || IsBookingsPage;
    public string ActiveConfigSection => NormalizeConfigSection(ConfigSection);
    public Shift? SelectedShift { get; private set; }
    public FairEvent? SelectedEvent { get; private set; }
    public EventDate? SelectedDate { get; private set; }
    public IReadOnlyList<DiningTable> Tables { get; private set; } = [];
    public IReadOnlyList<Reservation> ActiveReservations { get; private set; } = [];
    public IReadOnlyList<ReservationRow> ReservationRows { get; private set; } = [];
    public IReadOnlyList<ReservationRow> UnassignedReservationRows { get; private set; } = [];
    public IReadOnlyList<AssignmentRow> AssignmentRows { get; private set; } = [];
    public IReadOnlyList<OrganizationRow> OrganizationRows { get; private set; } = [];
    public IReadOnlyList<EventRow> EventRows { get; private set; } = [];
    public IReadOnlyList<DateRow> DateRows { get; private set; } = [];
    public IReadOnlyList<ShiftRow> ShiftRows { get; private set; } = [];
    public IReadOnlyList<TableRow> TableRows { get; private set; } = [];
    public IReadOnlyList<UserRow> UserRows { get; private set; } = [];
    public IReadOnlyList<AuditRow> AuditRows { get; private set; } = [];
    public int BookedSeats => ActiveReservations.Sum(reservation => reservation.PartySize);
    public int TotalSeats => Tables.Sum(table => table.Capacity);
    public int FreeSeats => Math.Max(0, TotalSeats - BookedSeats);
    public int EstimatedOccupiedTables { get; private set; }
    public int EstimatedFreeTables => Math.Max(0, Tables.Count - EstimatedOccupiedTables);
    public bool HasAssignments => AssignmentRows.Count > 0;
    public string AssignmentSummary { get; private set; } = "Nessuna assegnazione eseguita.";
    public string DefaultColumnOrder => string.Join(",", ExportService.DefaultColumns);

    public IActionResult OnGet()
    {
        LoadPageState();
        if (!CanAccessCurrentPage())
        {
            return Forbid();
        }

        return Page();
    }

    public IActionResult OnGetReservationList()
    {
        LoadPageState();
        if (CurrentPageIs("/Reception"))
        {
            return CanReception
                ? Partial("_ReceptionReservationList", this)
                : Forbid();
        }

        if (CurrentPageIs("/Bookings"))
        {
            return CanManageReservations
                ? Partial("_BookingReservationList", this)
                : Forbid();
        }

        return Forbid();
    }

    public IActionResult OnGetSimilarReservation(string? bookerName)
    {
        LoadPageState();
        if (!CanManageReservations)
        {
            return Forbid();
        }

        var match = FindSimilarReservation(bookerName);
        return new JsonResult(new
        {
            hasMatch = match is not null,
            message = match is null ? string.Empty : DuplicateReservationMessage(match)
        });
    }

    public IActionResult OnPostOrganization()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!string.IsNullOrWhiteSpace(OrganizationInput.Name))
        {
            TryUserOperation(() =>
            {
                var organization = store.AddOrganization(OrganizationInput.Name, CurrentUserId());
                SelectedOrganizationId = organization.Id;
                SelectedEventId = null;
                SelectedDateId = null;
                SelectedShiftId = null;
            }, "Organizzazione creata.");
        }

        return RedirectToConfigSection("organizations", "config-organization-name");
    }

    public IActionResult OnPostRenameOrganization(Guid organizationId, string name)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            TryUserOperation(() => store.RenameOrganization(organizationId, name, CurrentUserId()), "Organizzazione aggiornata.");
        }

        return RedirectToConfigSection("organizations");
    }

    public IActionResult OnPostDeleteOrganization(Guid organizationId)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (store.DeleteOrganization(organizationId, CurrentUserId(), out var error))
        {
            if (SelectedOrganizationId == organizationId)
            {
                SelectedOrganizationId = null;
                SelectedEventId = null;
                SelectedDateId = null;
                SelectedShiftId = null;
            }

            FlashMessage = "Organizzazione cancellata.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToConfigSection("organizations");
    }

    public IActionResult OnPostUser()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedOrganizationId.HasValue &&
            !string.IsNullOrWhiteSpace(UserInput.UserName) &&
            !string.IsNullOrWhiteSpace(UserInput.Password))
        {
            TryUserOperation(
                () => store.AddUser(SelectedOrganizationId.Value, UserInput.UserName, UserInput.Password, UserInput.Role, UserInput.DisplayName, CurrentUserId()),
                "Utente creato.");
        }

        return RedirectToConfigSection("users", "config-user-name");
    }

    public IActionResult OnPostSetUserActive(Guid targetUserId, bool isActive)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        store.SetUserActive(targetUserId, isActive, CurrentUserId());
        FlashMessage = isActive ? "Utente riattivato." : "Utente disattivato.";
        return RedirectToConfigSection("users");
    }

    public IActionResult OnPostEvent()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedOrganizationId.HasValue && !string.IsNullOrWhiteSpace(EventInput.Name))
        {
            TryUserOperation(() =>
            {
                var fairEvent = store.AddEvent(SelectedOrganizationId.Value, EventInput.Name, CurrentUserId());
                SelectedEventId = fairEvent.Id;
                SelectedDateId = null;
                SelectedShiftId = null;
            }, "Evento creato.");
        }

        return RedirectToConfigSection("events", "config-event-name");
    }

    public IActionResult OnPostRenameEvent(Guid eventId, string name)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            TryUserOperation(() => store.RenameEvent(eventId, name, CurrentUserId()), "Evento aggiornato.");
        }

        return RedirectToConfigSection("events");
    }

    public IActionResult OnPostToggleEvent(Guid eventId, bool isArchived)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        store.SetEventArchived(eventId, isArchived, CurrentUserId());
        FlashMessage = isArchived ? "Evento archiviato." : "Evento ripristinato.";
        return RedirectToConfigSection("events");
    }

    public IActionResult OnPostDeleteEvent(Guid eventId)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (store.DeleteEvent(eventId, CurrentUserId(), out var error))
        {
            if (SelectedEventId == eventId)
            {
                SelectedEventId = null;
                SelectedDateId = null;
                SelectedShiftId = null;
            }

            FlashMessage = "Evento cancellato.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToConfigSection("events");
    }

    public IActionResult OnPostGuidedEventSetup()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!SelectedEventId.HasValue)
        {
            FlashMessage = "Seleziona un evento prima di usare la configurazione guidata.";
            return RedirectToConfigSection("schedule", "config-wizard-start", "wizard");
        }

        try
        {
            var shiftTemplates = ParseGuidedShiftTemplates(EventWizardInput.ShiftTemplates);
            var result = store.ConfigureEventSchedule(
                SelectedEventId.Value,
                EventWizardInput.StartsOn,
                EventWizardInput.EndsOn,
                shiftTemplates,
                EventWizardInput.TablesPerShift,
                EventWizardInput.TableCapacity,
                CurrentUserId());

            SelectedDateId = result.FirstDateId ?? SelectedDateId;
            SelectedShiftId = result.FirstShiftId ?? SelectedShiftId;
            FlashMessage = $"Configurazione completata: {result.CreatedDates} date create, {result.CreatedShifts} turni creati, {result.CreatedTables} tavoli aggiunti. Esistenti: {result.ReusedDates} date, {result.ReusedShifts} turni.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException)
        {
            FlashMessage = exception.Message;
        }

        return RedirectToConfigSection("schedule", "config-wizard-start", "wizard");
    }

    public IActionResult OnPostDate()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedEventId.HasValue)
        {
            TryUserOperation(() =>
            {
                var date = store.AddEventDate(SelectedEventId.Value, DateInput.Date, CurrentUserId());
                SelectedDateId = date.Id;
                SelectedShiftId = null;
            }, "Data creata.");
        }

        return RedirectToConfigSection("schedule", "config-date-value", "dates");
    }

    public IActionResult OnPostUpdateDate(Guid eventDateId, DateOnly date)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        TryUserOperation(() => store.UpdateEventDate(eventDateId, date, CurrentUserId()), "Data aggiornata.");
        return RedirectToConfigSection("schedule", fragment: "dates");
    }

    public IActionResult OnPostDeleteDate(Guid eventDateId)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (store.DeleteEventDate(eventDateId, CurrentUserId(), out var error))
        {
            if (SelectedDateId == eventDateId)
            {
                SelectedDateId = null;
                SelectedShiftId = null;
            }

            FlashMessage = "Data cancellata.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToConfigSection("schedule", fragment: "dates");
    }

    public IActionResult OnPostShift()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedDateId.HasValue && !string.IsNullOrWhiteSpace(ShiftInput.Name))
        {
            TryUserOperation(() =>
            {
                var shift = store.AddShift(SelectedDateId.Value, ShiftInput.Name, ShiftInput.StartsAt, CurrentUserId());
                SelectedShiftId = shift.Id;
            }, "Turno creato.");
        }

        return RedirectToConfigSection("schedule", "config-shift-name", "shifts");
    }

    public IActionResult OnPostUpdateShift(Guid shiftId, string name, TimeOnly? startsAt)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            TryUserOperation(() => store.UpdateShift(shiftId, name, startsAt, CurrentUserId()), "Turno aggiornato.");
        }

        return RedirectToConfigSection("schedule", fragment: "shifts");
    }

    public IActionResult OnPostSetShiftClosed(Guid shiftId, bool isClosed)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        store.SetShiftClosed(shiftId, isClosed, CurrentUserId());
        FlashMessage = isClosed ? "Turno archiviato." : "Turno ripristinato.";
        return RedirectToConfigSection("schedule", fragment: "shifts");
    }

    public IActionResult OnPostArchivePastShifts()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!SelectedEventId.HasValue)
        {
            FlashMessage = "Seleziona un evento prima di archiviare i turni passati.";
            return RedirectToConfigSection("schedule", fragment: "shifts");
        }

        var count = store.ArchivePastShifts(SelectedEventId.Value, DateTime.Now, CurrentUserId());
        FlashMessage = count == 1
            ? "Archiviato 1 turno passato."
            : $"Archiviati {count} turni passati.";
        return RedirectToConfigSection("schedule", fragment: "shifts");
    }

    public IActionResult OnPostDeleteShift(Guid shiftId)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (store.DeleteShift(shiftId, CurrentUserId(), out var error))
        {
            if (SelectedShiftId == shiftId)
            {
                SelectedShiftId = null;
            }

            FlashMessage = "Turno cancellato.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToConfigSection("schedule", fragment: "shifts");
    }

    public IActionResult OnPostTables()
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedShift is not null && TableInput.Count > 0 && TableInput.Capacity > 0)
        {
            TryUserOperation(() => store.AddTables(SelectedShift.Id, TableInput.Count, TableInput.Capacity, CurrentUserId()), "Tavoli aggiunti.");
        }

        return RedirectToConfigSection("schedule", "config-table-count", "tables");
    }

    public IActionResult OnPostUpdateTable(Guid tableId, string code, int capacity, string? notes)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (!string.IsNullOrWhiteSpace(code) && capacity > 0)
        {
            TryUserOperation(() => store.UpdateTable(tableId, code, capacity, notes, CurrentUserId()), "Tavolo aggiornato.");
        }

        return RedirectToConfigSection("schedule", fragment: "tables");
    }

    public IActionResult OnPostDeleteTable(Guid tableId)
    {
        if (DenyUnlessSetup() is { } denied)
        {
            return denied;
        }

        if (store.DeleteTable(tableId, CurrentUserId(), out var error))
        {
            FlashMessage = "Tavolo cancellato. Le assegnazioni collegate sono state rimosse.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToConfigSection("schedule", fragment: "tables");
    }

    public IActionResult OnPostReservation()
    {
        if (DenyUnlessReservationManagement() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedShift is null || !store.UserCanAccessShift(CurrentUser, SelectedShift.Id))
        {
            return RedirectToPageWithSelection("reservation-name", "new-booking");
        }

        if (string.IsNullOrWhiteSpace(ReservationInput.BookerName))
        {
            SetFlash("Il nome della prenotazione e obbligatorio.", "warning");
            return RedirectToPageWithSelection("reservation-name", "new-booking");
        }

        if (!ReservationInput.PartySize.HasValue || ReservationInput.PartySize.Value <= 0)
        {
            SetFlash("Il numero di persone e obbligatorio e deve essere maggiore di 0.", "warning");
            return RedirectToPageWithSelection("reservation-name", "new-booking");
        }

        if (!IsReservationTimeInSelectedShiftRange(ReservationInput.ExpectedAt))
        {
            SetFlash("L'ora deve essere compresa tra l'inizio turno e le 23:45, a intervalli di 15 minuti.", "warning");
            return RedirectToPageWithSelection("reservation-name", "new-booking");
        }

        var date = store.Dates.First(item => item.Id == SelectedShift.EventDateId);
        try
        {
            var reservation = store.AddReservation(
                SelectedShift.Id,
                ReservationInput.BookerName,
                date.Date,
                ReservationInput.PartySize.Value,
                ReservationInput.ExpectedAt,
                ReservationInput.MobilePhone,
                CurrentUserId(),
                ReservationInput.Notes);
            SetFlash("Prenotazione aggiunta.", "success", ReservationFlashDescription(reservation));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            SetFlash(exception.Message, "danger");
        }

        return RedirectToPageWithSelection("reservation-name", "new-booking");
    }

    public IActionResult OnPostUpdateReservation(Guid reservationId, string bookerName, int partySize, TimeOnly? expectedAt, string? mobilePhone, string? notes)
    {
        if (DenyUnlessReservationManagement() is { } denied)
        {
            return denied;
        }

        if (!store.UserCanAccessReservation(CurrentUser, reservationId))
        {
            return Forbid();
        }

        if (!IsReservationTimeInSelectedShiftRange(expectedAt))
        {
            SetFlash("L'ora deve essere compresa tra l'inizio turno e le 23:45, a intervalli di 15 minuti.", "warning");
            return RedirectToPageWithSelection(null, "booking-list");
        }

        if (string.IsNullOrWhiteSpace(bookerName))
        {
            SetFlash("Il nome della prenotazione e obbligatorio.", "warning");
        }
        else if (partySize <= 0)
        {
            SetFlash("Il numero di persone e obbligatorio e deve essere maggiore di 0.", "warning");
        }
        else
        {
            try
            {
                store.UpdateReservation(reservationId, bookerName, partySize, expectedAt, mobilePhone, CurrentUserId(), notes);
                var reservation = store.Reservations.First(item => item.Id == reservationId);
                SetFlash("Prenotazione aggiornata. Ricalcola o modifica manualmente l'assegnazione.", "success", ReservationFlashDescription(reservation));
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                SetFlash(exception.Message, "danger");
            }
        }

        return RedirectToPageWithSelection(null, "booking-list");
    }

    public IActionResult OnPostAssign(bool confirmReassign = false)
    {
        if (DenyUnlessReservationManagement() is { } denied)
        {
            return denied;
        }

        LoadPageState();
        if (SelectedShift is null)
        {
            FlashMessage = "Seleziona un turno prima di assegnare i tavoli.";
        }
        else if (Tables.Count == 0)
        {
            FlashMessage = "Non ci sono tavoli configurati per questo turno. Vai in Configurazione > Tavoli e aggiungili.";
        }
        else if (ActiveReservations.Count == 0)
        {
            FlashMessage = "Non ci sono prenotazioni attive da assegnare in questo turno.";
        }
        else if (HasAssignments && !confirmReassign)
        {
            FlashMessage = "Esistono gia assegnazioni per questo turno. Conferma il ricalcolo prima di sostituirle.";
        }
        else
        {
            var result = assignmentService.Assign(new TableAssignmentRequest(Tables, ActiveReservations));
            if (!store.ReplaceAssignments(SelectedShift.Id, result.Assignments, CurrentUserId(), out var error))
            {
                FlashMessage = error;
            }
            else
            {
                FlashMessage = result.Assignments.Count == 0
                ? "Nessuna prenotazione assegnata: controlla capienza e tavoli disponibili."
                : result.UnassignedReservations.Count == 0
                ? "Assegnazione completata."
                : $"Assegnazione completata con {result.UnassignedReservations.Count} prenotazioni non assegnate.";
            }
        }

        return RedirectToPageWithSelection(null, "assignments");
    }

    public IActionResult OnPostManualAssign(Guid reservationId, Guid[] targetTableIds)
    {
        if (DenyUnlessReservationManagement() is { } denied)
        {
            return denied;
        }

        if (!store.UserCanAccessReservation(CurrentUser, reservationId) ||
            targetTableIds.Any(tableId => !store.UserCanAccessTable(CurrentUser, tableId)))
        {
            return Forbid();
        }

        if (store.AssignReservationManually(reservationId, targetTableIds, CurrentUserId(), out var error))
        {
            FlashMessage = "Assegnazione manuale aggiornata.";
        }
        else
        {
            FlashMessage = error;
        }

        return RedirectToPageWithSelection(null, CurrentPageIs("/Assignments") ? "manual-assignments" : "booking-list");
    }

    public IActionResult OnPostArrived(Guid reservationId)
    {
        if (DenyUnlessReception() is { } denied)
        {
            return denied;
        }

        if (!store.UserCanAccessReservation(CurrentUser, reservationId))
        {
            return Forbid();
        }

        store.MarkArrived(reservationId, CurrentUserId());
        var reservation = store.Reservations.First(item => item.Id == reservationId);
        SetFlash("Prenotazione segnata presente.", "success", ReservationFlashDescription(reservation));
        return RedirectToPageWithSelection("reception-search", "reception-list");
    }

    public IActionResult OnPostNoShow(Guid reservationId)
    {
        if (DenyUnlessReception() is { } denied)
        {
            return denied;
        }

        if (!store.UserCanAccessReservation(CurrentUser, reservationId))
        {
            return Forbid();
        }

        store.MarkNoShow(reservationId, CurrentUserId());
        var reservation = store.Reservations.First(item => item.Id == reservationId);
        SetFlash("Prenotazione segnata non presentata.", "danger", ReservationFlashDescription(reservation));
        return RedirectToPageWithSelection("reception-search", "reception-list");
    }

    public IActionResult OnPostCancel(Guid reservationId)
    {
        var denied = CurrentPageIs("/Reception")
            ? DenyUnlessReception()
            : DenyUnlessReservationManagement();
        if (denied is not null)
        {
            return denied;
        }

        if (!store.UserCanAccessReservation(CurrentUser, reservationId))
        {
            return Forbid();
        }

        store.CancelReservation(reservationId, CurrentUserId());
        var reservation = store.Reservations.First(item => item.Id == reservationId);
        SetFlash("Prenotazione annullata.", "warning", ReservationFlashDescription(reservation));
        return RedirectToPageWithSelection(null, CurrentPageIs("/Reception") ? "reception-list" : "booking-list");
    }

    public IActionResult OnGetExportExcel(string? columnOrder, string? sort)
    {
        LoadPageState();
        if (!CanManageReservations && !CanReception)
        {
            return Forbid();
        }

        var rows = BuildExportRows(sort);
        var bytes = exportService.BuildExcel(rows, ParseColumns(columnOrder));
        return File(bytes, "application/vnd.ms-excel", BuildExportFileName("xls"));
    }

    public IActionResult OnGetExportPdf(string? columnOrder, string? sort)
    {
        LoadPageState();
        if (!CanManageReservations && !CanReception)
        {
            return Forbid();
        }

        var rows = BuildExportRows(sort);
        var bytes = exportService.BuildPdf(BuildExportContext(rows), rows, ParseColumns(columnOrder));
        return File(bytes, "application/pdf", BuildExportFileName("pdf"));
    }

    private void LoadPageState()
    {
        CurrentUser = CurrentApplicationUser();
        var organizations = CurrentUser.Role == UserRole.Admin
            ? store.Organizations
            : store.Organizations.Where(item => item.Id == CurrentUser.OrganizationId).ToArray();
        var useCurrentReceptionContext = ShouldUseCurrentReceptionContext();

        if (useCurrentReceptionContext)
        {
            var currentContext = FindCurrentReceptionContext(organizations, DateTime.Now);
            if (currentContext is not null)
            {
                SelectedOrganizationId = currentContext.OrganizationId;
                SelectedEventId = currentContext.EventId;
                SelectedDateId = currentContext.EventDateId;
                SelectedShiftId = currentContext.ShiftId;
            }
        }

        SelectedOrganizationId = SelectedOrganizationId.HasValue && organizations.Any(item => item.Id == SelectedOrganizationId.Value)
            ? SelectedOrganizationId
            : organizations.FirstOrDefault()?.Id;

        var organization = organizations.FirstOrDefault(item => item.Id == SelectedOrganizationId);
        OrganizationName = organization?.Name ?? string.Empty;

        var events = store.Events.Where(item => item.OrganizationId == SelectedOrganizationId).OrderBy(item => item.IsArchived).ThenBy(item => item.Name).ToArray();
        SelectedEventId = SelectedEventId.HasValue && events.Any(item => item.Id == SelectedEventId.Value)
            ? SelectedEventId
            : events.FirstOrDefault(item => !item.IsArchived && EventHasActiveReservations(item.Id))?.Id ??
              events.FirstOrDefault(item => !item.IsArchived && EventHasTables(item.Id))?.Id ??
              events.FirstOrDefault(item => !item.IsArchived)?.Id ??
              events.FirstOrDefault()?.Id;
        SelectedEvent = events.FirstOrDefault(item => item.Id == SelectedEventId);

        var dates = store.Dates.Where(item => item.EventId == SelectedEventId).OrderBy(item => item.Date).ToArray();
        SelectedDateId = SelectedDateId.HasValue && dates.Any(item => item.Id == SelectedDateId.Value)
            ? SelectedDateId
            : dates.FirstOrDefault(item => DateHasActiveReservations(item.Id))?.Id ??
              dates.FirstOrDefault(item => DateHasTables(item.Id))?.Id ??
              dates.FirstOrDefault()?.Id;
        SelectedDate = dates.FirstOrDefault(item => item.Id == SelectedDateId);

        var shifts = store.Shifts.Where(item => item.EventDateId == SelectedDateId).OrderBy(item => item.IsClosed).ThenBy(item => item.StartsAt).ToArray();
        SelectedShiftId = SelectedShiftId.HasValue && shifts.Any(item => item.Id == SelectedShiftId.Value)
            ? SelectedShiftId
            : shifts.FirstOrDefault(shift => !shift.IsClosed && ShiftHasActiveReservations(shift.Id))?.Id ??
              shifts.FirstOrDefault(shift => !shift.IsClosed && ShiftHasTables(shift.Id))?.Id ??
              shifts.FirstOrDefault(shift => !shift.IsClosed)?.Id ??
              shifts.FirstOrDefault()?.Id;
        SelectedShift = shifts.FirstOrDefault(item => item.Id == SelectedShiftId);

        Tables = store.Tables.Where(table => table.ShiftId == SelectedShiftId).OrderBy(table => table.Code).ToArray();
        var shiftReservations = store.Reservations
            .Where(reservation => reservation.ShiftId == SelectedShiftId)
            .OrderBy(reservation => reservation.BookerName)
            .ToArray();
        ActiveReservations = shiftReservations
            .Where(reservation => reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow)
            .ToArray();

        var assignments = store.Assignments.Where(assignment => assignment.ShiftId == SelectedShiftId).ToArray();
        AssignmentSummary = assignments.Length == 0
            ? "Nessuna assegnazione eseguita."
            : $"{assignments.Length} assegnazioni attive.";
        EstimatedOccupiedTables = EstimateOccupiedTables(Tables, ActiveReservations, assignments);

        var filteredReservations = shiftReservations.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filteredReservations = filteredReservations.Where(reservation =>
                reservation.BookerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (reservation.MobilePhone?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (Enum.TryParse<ReservationStatus>(StatusFilter, out var filterStatus))
        {
            filteredReservations = filteredReservations.Where(reservation => reservation.Status == filterStatus);
        }
        else if (!CurrentPageIs("/Reception"))
        {
            filteredReservations = filteredReservations.Where(reservation => reservation.Status != ReservationStatus.Cancelled);
        }

        ReservationRows = filteredReservations.Select(reservation =>
        {
            var assignment = assignments.FirstOrDefault(item => item.ReservationIds.Contains(reservation.Id));
            IReadOnlyList<DiningTable> assignedTables = assignment is null
                ? []
                : Tables.Where(table => assignment.TableIds.Contains(table.Id)).ToArray();
            var tableLabel = assignment is null ? "Da assegnare" : FormatTableCodes(assignedTables);
            return new ReservationRow(
                reservation.Id,
                reservation.BookerName,
                reservation.MobilePhone ?? string.Empty,
                reservation.Notes ?? string.Empty,
                reservation.ExpectedAt?.ToString("HH:mm") ?? string.Empty,
                reservation.PartySize,
                StatusLabel(reservation.Status),
                StatusCssClass(reservation.Status),
                tableLabel,
                assignment is null ? null : BuildReservationTableBadge(assignedTables, assignment, shiftReservations));
        }).ToArray();

        AssignmentRows = assignments.Select(assignment =>
        {
            var tableLabel = string.Join(", ", Tables.Where(table => assignment.TableIds.Contains(table.Id)).Select(table => table.Code));
            var assignmentReservations = shiftReservations
                .Where(reservation => assignment.ReservationIds.Contains(reservation.Id))
                .ToArray();
            var reservationLabel = string.Join(" + ", assignmentReservations.Select(reservation => $"{reservation.BookerName} ({reservation.PartySize})"));
            var capacity = Tables.Where(table => assignment.TableIds.Contains(table.Id)).Sum(table => table.Capacity);
            var people = assignmentReservations.Sum(reservation => reservation.PartySize);
            return new AssignmentRow(assignment.Id, tableLabel, reservationLabel, assignment.Source.ToString(), people, capacity);
        }).ToArray();

        var assignedReservationIds = assignments.SelectMany(assignment => assignment.ReservationIds).ToHashSet();
        UnassignedReservationRows = ActiveReservations
            .Where(reservation => !assignedReservationIds.Contains(reservation.Id))
            .Select(reservation => new ReservationRow(
                reservation.Id,
                reservation.BookerName,
                reservation.MobilePhone ?? string.Empty,
                reservation.Notes ?? string.Empty,
                reservation.ExpectedAt?.ToString("HH:mm") ?? string.Empty,
                reservation.PartySize,
                StatusLabel(reservation.Status),
                StatusCssClass(reservation.Status),
                "Da assegnare",
                null))
            .ToArray();

        OrganizationRows = organizations.Select(item => new OrganizationRow(item.Id, item.Name, item.Id == CurrentUser.OrganizationId)).ToArray();
        EventRows = events.Select(item => new EventRow(item.Id, item.Name, item.IsArchived)).ToArray();
        DateRows = dates.Select(item => new DateRow(item.Id, item.Date)).ToArray();
        ShiftRows = shifts.Select(item => new ShiftRow(item.Id, item.Name, item.StartsAt?.ToString("HH:mm") ?? string.Empty, item.IsClosed)).ToArray();
        TableRows = BuildTableRows(Tables, assignments, shiftReservations);
        UserRows = store.Users
            .Where(item => item.OrganizationId == SelectedOrganizationId)
            .OrderBy(item => item.UserName)
            .Select(item => new UserRow(item.Id, item.UserName, item.DisplayName ?? string.Empty, item.Role.ToString(), item.IsActive))
            .ToArray();
        var usersById = store.Users.ToDictionary(user => user.Id, user => user.UserName);
        AuditRows = store.AuditLogs
            .Where(item => item.OrganizationId == SelectedOrganizationId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(80)
            .Select(item => new AuditRow(
                item.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm"),
                usersById.GetValueOrDefault(item.UserId, item.UserId == Guid.Empty ? "sistema" : "utente rimosso"),
                item.EntityName,
                item.EntityId,
                item.Action,
                item.Details ?? string.Empty))
            .ToArray();

        OrganizationOptions = organizations.Select(item => new SelectListItem(item.Name, item.Id.ToString(), item.Id == SelectedOrganizationId)).ToArray();
        EventOptions = events.Select(item => new SelectListItem(item.IsArchived ? $"{item.Name} (archiviato)" : item.Name, item.Id.ToString(), item.Id == SelectedEventId)).ToArray();
        DateOptions = dates.Select(item => new SelectListItem(item.Date.ToString("dd/MM/yyyy"), item.Id.ToString(), item.Id == SelectedDateId)).ToArray();
        ShiftOptions = shifts.Select(item => new SelectListItem(item.IsClosed ? $"{item.Name} (archiviato)" : item.Name, item.Id.ToString(), item.Id == SelectedShiftId)).ToArray();
        RoleOptions = Enum.GetValues<UserRole>().Select(role => new SelectListItem(role.ToString(), role.ToString())).ToArray();
        StatusOptions =
        [
            new SelectListItem("Tutti gli stati", string.Empty, string.IsNullOrWhiteSpace(StatusFilter)),
            .. Enum.GetValues<ReservationStatus>()
                .Select(status => new SelectListItem(StatusLabel(status), status.ToString(), StatusFilter == status.ToString()))
        ];
        ReservationTimeOptions = BuildReservationTimeOptions(SelectedShift);
    }

    private static ReservationTableBadge BuildReservationTableBadge(
        IReadOnlyList<DiningTable> assignedTables,
        GestionePrenotazioni.Web.Domain.TableAssignment assignment,
        IReadOnlyList<Reservation> shiftReservations)
    {
        var capacity = assignedTables.Sum(table => table.Capacity);
        var occupiedSeats = shiftReservations
            .Where(reservation => assignment.ReservationIds.Contains(reservation.Id))
            .Sum(reservation => reservation.PartySize);
        return new ReservationTableBadge(
            FormatTableBadgeLabel(assignedTables),
            capacity,
            Math.Max(0, capacity - occupiedSeats));
    }

    private static IReadOnlyList<TableRow> BuildTableRows(
        IReadOnlyList<DiningTable> tables,
        IReadOnlyList<GestionePrenotazioni.Web.Domain.TableAssignment> assignments,
        IReadOnlyList<Reservation> shiftReservations)
    {
        return tables.Select(table =>
        {
            var assignment = assignments.FirstOrDefault(item => item.TableIds.Contains(table.Id));
            var occupancyLabel = assignment is null
                ? $"liberi {table.Capacity} su {table.Capacity}"
                : TableOccupancyLabel(assignment, tables, shiftReservations);
            return new TableRow(table.Id, table.Code, table.Capacity, occupancyLabel, table.Notes ?? string.Empty);
        }).ToArray();
    }

    private static string TableOccupancyLabel(
        GestionePrenotazioni.Web.Domain.TableAssignment assignment,
        IReadOnlyList<DiningTable> tables,
        IReadOnlyList<Reservation> shiftReservations)
    {
        var assignedTables = tables.Where(item => assignment.TableIds.Contains(item.Id)).ToArray();
        var capacity = assignedTables.Sum(item => item.Capacity);
        var occupiedSeats = shiftReservations
            .Where(reservation => assignment.ReservationIds.Contains(reservation.Id))
            .Sum(reservation => reservation.PartySize);
        var freeSeats = Math.Max(0, capacity - occupiedSeats);
        return assignedTables.Length == 1
            ? $"liberi {freeSeats} su {capacity}"
            : $"gruppo {FormatTableCodes(assignedTables)}: liberi {freeSeats} su {capacity}";
    }

    private static string FormatTableBadgeLabel(IReadOnlyList<DiningTable> assignedTables)
    {
        return assignedTables.Count == 1
            ? $"Tavolo {FormatTableCodes(assignedTables)}"
            : $"Tavoli {FormatTableCodes(assignedTables)}";
    }

    private static string FormatTableCodes(IReadOnlyList<DiningTable> tables)
    {
        return tables.Count == 0
            ? "non trovato"
            : string.Join(", ", tables.Select(table => table.Code));
    }

    private int EstimateOccupiedTables(
        IReadOnlyList<DiningTable> tables,
        IReadOnlyList<Reservation> activeReservations,
        IReadOnlyList<GestionePrenotazioni.Web.Domain.TableAssignment> assignments)
    {
        if (tables.Count == 0 || activeReservations.Count == 0)
        {
            return 0;
        }

        var assignedTableIds = assignments.SelectMany(assignment => assignment.TableIds).ToHashSet();
        var assignedReservationIds = assignments.SelectMany(assignment => assignment.ReservationIds).ToHashSet();
        var unassignedReservations = activeReservations
            .Where(reservation => !assignedReservationIds.Contains(reservation.Id))
            .ToArray();
        if (unassignedReservations.Length == 0)
        {
            return Math.Min(tables.Count, assignedTableIds.Count);
        }

        var availableTables = tables.Where(table => !assignedTableIds.Contains(table.Id)).ToArray();
        if (availableTables.Length == 0)
        {
            return assignedTableIds.Count;
        }

        var estimate = assignmentService.Assign(new TableAssignmentRequest(availableTables, unassignedReservations));
        var estimatedTableIds = estimate.Assignments.SelectMany(assignment => assignment.TableIds).Distinct().Count();
        return Math.Min(tables.Count, assignedTableIds.Count + estimatedTableIds);
    }

    private IReadOnlyList<ReservationExportRow> BuildExportRows(string? sort)
    {
        var assignments = store.Assignments
            .Where(assignment => assignment.ShiftId == SelectedShiftId)
            .ToArray();
        var tableLabelsByReservation = assignments
            .SelectMany(assignment =>
            {
                var assignedTables = Tables.Where(table => assignment.TableIds.Contains(table.Id)).ToArray();
                var tableLabel = FormatTableCodes(assignedTables);
                return assignment.ReservationIds.Select(reservationId => new { reservationId, tableLabel });
            })
            .GroupBy(item => item.reservationId)
            .ToDictionary(group => group.Key, group => group.First().tableLabel);

        var rows = ActiveReservations
            .Select(reservation =>
        {
            return new ReservationExportRow(
                reservation.BookerName,
                reservation.Date.ToString("dd/MM/yyyy"),
                reservation.ExpectedAt?.ToString("HH:mm") ?? string.Empty,
                reservation.MobilePhone ?? string.Empty,
                reservation.Notes ?? string.Empty,
                reservation.PartySize,
                StatusLabel(reservation.Status),
                tableLabelsByReservation.GetValueOrDefault(reservation.Id, string.Empty));
        });

        rows = sort switch
        {
            "people" => rows.OrderByDescending(row => row.PartySize).ThenBy(row => row.BookerName),
            "tables" => rows.OrderBy(row => row.Tables == "Da assegnare" ? "ZZZ" : row.Tables).ThenBy(row => row.BookerName),
            "status" => rows.OrderBy(row => row.Status).ThenBy(row => row.BookerName),
            _ => rows.OrderBy(row => row.BookerName)
        };

        return rows.ToArray();
    }

    private ReservationExportContext BuildExportContext(IReadOnlyList<ReservationExportRow> rows)
    {
        return new ReservationExportContext(
            OrganizationName,
            SelectedEvent?.Name ?? "Evento non selezionato",
            SelectedDate?.Date.ToString("dd/MM/yyyy") ?? "Data non selezionata",
            SelectedShift?.Name ?? "Turno non selezionato",
            SelectedShift?.StartsAt?.ToString("HH:mm") ?? string.Empty,
            rows.Count,
            rows.Sum(row => row.PartySize));
    }

    private string BuildExportFileName(string extension)
    {
        var eventName = SelectedEvent?.Name ?? "evento";
        var date = SelectedDate?.Date.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
        var shiftName = SelectedShift?.Name ?? "turno";
        var stamp = DateTime.Now.ToString("HHmmss");
        return $"{Slug(eventName)}_{date}_{Slug(shiftName)}_{stamp}.{extension}";
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_') is { Length: > 0 } slug ? slug : "prenotazioni";
    }

    private IReadOnlyList<string> ParseColumns(string? columnOrder)
    {
        if (string.IsNullOrWhiteSpace(columnOrder))
        {
            return ExportService.DefaultColumns;
        }

        return columnOrder.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private IActionResult? DenyUnlessSetup()
    {
        LoadPageState();
        return CanManageSetup ? null : Forbid();
    }

    private IActionResult? DenyUnlessReservationManagement()
    {
        LoadPageState();
        return CanManageReservations ? null : Forbid();
    }

    private IActionResult? DenyUnlessReception()
    {
        LoadPageState();
        return CanReception ? null : Forbid();
    }

    private bool CanAccessCurrentPage()
    {
        return PageContext.ActionDescriptor.ViewEnginePath switch
        {
            "/Bookings" or "/Assignments" => CanManageReservations,
            "/Reception" => CanReception,
            "/Config" => CanManageSetup,
            "/Exports" => CanManageReservations || CanReception,
            _ => true
        };
    }

    private bool TryUserOperation(Action action, string successMessage)
    {
        try
        {
            action();
            SetFlash(successMessage, "success");
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            SetFlash(exception.Message, "danger");
            return false;
        }
    }

    private void SetFlash(string message, string kind = "info", string? description = null)
    {
        FlashMessage = message;
        FlashKind = kind;
        FlashDescription = description;
    }

    private Reservation? FindSimilarReservation(string? bookerName)
    {
        if (SelectedShift is null || string.IsNullOrWhiteSpace(bookerName))
        {
            return null;
        }

        var requestedName = NormalizeNameForMatch(bookerName);
        if (requestedName.Length < 3)
        {
            return null;
        }

        return store.Reservations
            .Where(reservation =>
                reservation.ShiftId == SelectedShift.Id &&
                reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow)
            .OrderBy(reservation => reservation.ExpectedAt ?? TimeOnly.MaxValue)
            .ThenBy(reservation => reservation.BookerName)
            .FirstOrDefault(reservation => NamesAreSimilar(requestedName, NormalizeNameForMatch(reservation.BookerName)));
    }

    private static bool NamesAreSimilar(string requestedName, string existingName)
    {
        if (string.IsNullOrWhiteSpace(requestedName) || string.IsNullOrWhiteSpace(existingName))
        {
            return false;
        }

        if (existingName.Contains(requestedName, StringComparison.Ordinal) ||
            requestedName.Contains(existingName, StringComparison.Ordinal))
        {
            return true;
        }

        var requestedTokens = requestedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length >= 3);
        var existingTokens = existingName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length >= 3).ToArray();
        return requestedTokens.Any(requestedToken => existingTokens.Any(existingToken =>
            requestedToken.Equals(existingToken, StringComparison.Ordinal) ||
            (requestedToken.Length >= 4 && existingToken.Contains(requestedToken, StringComparison.Ordinal)) ||
            (existingToken.Length >= 4 && requestedToken.Contains(existingToken, StringComparison.Ordinal))));
    }

    private static string NormalizeNameForMatch(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string DuplicateReservationMessage(Reservation reservation)
    {
        var time = reservation.ExpectedAt.HasValue
            ? $"alle ore {reservation.ExpectedAt.Value:HH:mm}"
            : "senza ora indicata";
        return $"Esiste gia una prenotazione per {reservation.BookerName} per {reservation.PartySize} PAX {time}. Confermi l'inserimento o vuoi modificare?";
    }

    private static string ReservationFlashDescription(Reservation reservation)
    {
        var time = reservation.ExpectedAt?.ToString("HH:mm") ?? "non indicata";
        return $"Prenotazione {reservation.BookerName} {reservation.PartySize} PAX ore {time}";
    }

    public bool IsConfigSection(string section)
    {
        return ActiveConfigSection.Equals(section, StringComparison.OrdinalIgnoreCase);
    }

    private RedirectToPageResult RedirectToConfigSection(string section, string? focusTarget = null, string? fragment = null)
    {
        ConfigSection = NormalizeConfigSection(section);
        return RedirectToPageWithSelection(focusTarget, fragment ?? ConfigSection);
    }

    private RedirectToPageResult RedirectToPageWithSelection(string? focusTarget = null, string? fragment = null)
    {
        return RedirectToPage(PageContext.ActionDescriptor.ViewEnginePath, null, new
        {
            SelectedOrganizationId,
            SelectedEventId,
            SelectedDateId,
            SelectedShiftId,
            SearchText,
            StatusFilter,
            ConfigSection,
            FocusTarget = focusTarget
        }, fragment);
    }

    private static string NormalizeConfigSection(string? section)
    {
        var normalized = section?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "dates" or "shifts" or "tables" or "schedule" => "schedule",
            "organizations" or "users" or "events" or "audit" => normalized,
            _ => "organizations"
        };
    }

    private bool CurrentPageIs(string path)
    {
        return PageContext.ActionDescriptor.ViewEnginePath.Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldUseCurrentReceptionContext()
    {
        return IsReceptionPage &&
            HttpContext.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            !Request.Query.ContainsKey(nameof(SelectedOrganizationId)) &&
            !Request.Query.ContainsKey(nameof(SelectedEventId)) &&
            !Request.Query.ContainsKey(nameof(SelectedDateId)) &&
            !Request.Query.ContainsKey(nameof(SelectedShiftId));
    }

    private ReceptionContext? FindCurrentReceptionContext(IEnumerable<Organization> organizations, DateTime now)
    {
        var organizationIds = organizations.Select(item => item.Id).ToHashSet();
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        return store.Events
            .Where(item => organizationIds.Contains(item.OrganizationId) && !item.IsArchived)
            .SelectMany(fairEvent => store.Dates
                .Where(date => date.EventId == fairEvent.Id && date.Date == today)
                .SelectMany(date => store.Shifts
                    .Where(shift => shift.EventDateId == date.Id && !shift.IsClosed)
                    .Select(shift => new
                    {
                        fairEvent.OrganizationId,
                        EventId = fairEvent.Id,
                        EventDateId = date.Id,
                        ShiftId = shift.Id,
                        HasReservations = ShiftHasActiveReservations(shift.Id),
                        HasTables = ShiftHasTables(shift.Id),
                        TimeDistance = TimeDistanceMinutes(shift.StartsAt, currentTime),
                        EventName = fairEvent.Name,
                        ShiftName = shift.Name
                    })))
            .OrderByDescending(item => item.HasReservations)
            .ThenByDescending(item => item.HasTables)
            .ThenBy(item => item.TimeDistance)
            .ThenBy(item => item.EventName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ShiftName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ReceptionContext(item.OrganizationId, item.EventId, item.EventDateId, item.ShiftId))
            .FirstOrDefault();
    }

    private static int TimeDistanceMinutes(TimeOnly? shiftTime, TimeOnly currentTime)
    {
        if (!shiftTime.HasValue)
        {
            return int.MaxValue;
        }

        return Math.Abs((int)(shiftTime.Value.ToTimeSpan() - currentTime.ToTimeSpan()).TotalMinutes);
    }

    private bool IsReservationTimeInSelectedShiftRange(TimeOnly? time)
    {
        if (!time.HasValue)
        {
            return true;
        }

        var startsAt = SelectedShift?.StartsAt ?? TimeOnly.MinValue;
        var latest = new TimeOnly(23, 45);
        if (time.Value < startsAt || time.Value > latest)
        {
            return false;
        }

        var difference = time.Value.ToTimeSpan() - startsAt.ToTimeSpan();
        return difference.Ticks % TimeSpan.FromMinutes(15).Ticks == 0;
    }

    private static IReadOnlyList<SelectListItem> BuildReservationTimeOptions(Shift? shift)
    {
        var options = new List<SelectListItem>
        {
            new("Nessuna ora", string.Empty)
        };
        var current = (shift?.StartsAt ?? TimeOnly.MinValue).ToTimeSpan();
        var latest = new TimeOnly(23, 45).ToTimeSpan();
        while (current <= latest)
        {
            var option = TimeOnly.FromTimeSpan(current);
            var value = option.ToString("HH:mm");
            options.Add(new SelectListItem(value, value));
            current += TimeSpan.FromMinutes(15);
        }

        return options;
    }

    private bool EventHasActiveReservations(Guid eventId)
    {
        var dateIds = store.Dates.Where(item => item.EventId == eventId).Select(item => item.Id).ToHashSet();
        var shiftIds = store.Shifts.Where(item => dateIds.Contains(item.EventDateId)).Select(item => item.Id).ToHashSet();
        return store.Reservations.Any(item => shiftIds.Contains(item.ShiftId) && item.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow);
    }

    private bool EventHasTables(Guid eventId)
    {
        var dateIds = store.Dates.Where(item => item.EventId == eventId).Select(item => item.Id).ToHashSet();
        var shiftIds = store.Shifts.Where(item => dateIds.Contains(item.EventDateId)).Select(item => item.Id).ToHashSet();
        return store.Tables.Any(item => shiftIds.Contains(item.ShiftId));
    }

    private bool DateHasActiveReservations(Guid eventDateId)
    {
        var shiftIds = store.Shifts.Where(item => item.EventDateId == eventDateId).Select(item => item.Id).ToHashSet();
        return store.Reservations.Any(item => shiftIds.Contains(item.ShiftId) && item.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow);
    }

    private bool DateHasTables(Guid eventDateId)
    {
        var shiftIds = store.Shifts.Where(item => item.EventDateId == eventDateId).Select(item => item.Id).ToHashSet();
        return store.Tables.Any(item => shiftIds.Contains(item.ShiftId));
    }

    private bool ShiftHasActiveReservations(Guid shiftId)
    {
        return store.Reservations.Any(item => item.ShiftId == shiftId && item.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow);
    }

    private bool ShiftHasTables(Guid shiftId)
    {
        return store.Tables.Any(item => item.ShiftId == shiftId);
    }

    private ApplicationUser CurrentApplicationUser()
    {
        var id = CurrentUserId();
        return store.Users.FirstOrDefault(user => user.Id == id) ?? store.Users.First();
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : store.Users.First().Id;
    }

    private static IReadOnlyList<GuidedEventShift> ParseGuidedShiftTemplates(string? value)
    {
        var lines = (value ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var shifts = new List<GuidedEventShift>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var cleanLine = line.Trim();
            if (cleanLine.Length == 0)
            {
                continue;
            }

            var name = cleanLine;
            TimeOnly? startsAt = null;
            var separatorIndex = cleanLine.LastIndexOfAny([',', ';']);
            if (separatorIndex >= 0)
            {
                name = cleanLine[..separatorIndex].Trim();
                var timePart = cleanLine[(separatorIndex + 1)..].Trim();
                if (timePart.Length > 0)
                {
                    startsAt = ParseGuidedShiftTime(timePart);
                }
            }
            else
            {
                var parts = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 1 && TryParseGuidedShiftTime(parts[^1], out var parsedTime))
                {
                    startsAt = parsedTime;
                    name = cleanLine[..cleanLine.LastIndexOf(parts[^1], StringComparison.Ordinal)].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Ogni turno predefinito deve avere un nome.");
            }

            if (!names.Add(name))
            {
                throw new InvalidOperationException("I turni predefiniti devono avere nomi diversi.");
            }

            shifts.Add(new GuidedEventShift(name, startsAt));
        }

        if (shifts.Count == 0)
        {
            throw new InvalidOperationException("Inserisci almeno un turno predefinito.");
        }

        return shifts;
    }

    private static TimeOnly ParseGuidedShiftTime(string value)
    {
        return TryParseGuidedShiftTime(value, out var time)
            ? time
            : throw new FormatException($"Ora turno non valida: {value}.");
    }

    private static bool TryParseGuidedShiftTime(string value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
            value,
            ["H:mm", "HH:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    private static string StatusLabel(ReservationStatus status)
    {
        return status switch
        {
            ReservationStatus.Entered => "Prenotazione inserita",
            ReservationStatus.Assigned => "Tavolo assegnato",
            ReservationStatus.Arrived => "Presente",
            ReservationStatus.Cancelled => "Annullata",
            ReservationStatus.NoShow => "Non presentata",
            _ => status.ToString()
        };
    }

    private static string StatusCssClass(ReservationStatus status)
    {
        return status switch
        {
            ReservationStatus.Entered => "status-entered",
            ReservationStatus.Assigned => "status-assigned",
            ReservationStatus.Arrived => "status-arrived",
            ReservationStatus.Cancelled => "status-cancelled",
            ReservationStatus.NoShow => "status-noshow",
            _ => "status-entered"
        };
    }

    public sealed class ReservationInputModel
    {
        [Required]
        public string BookerName { get; set; } = string.Empty;

        [Required]
        [Range(1, 1000)]
        public int? PartySize { get; set; }

        public TimeOnly? ExpectedAt { get; set; }
        public string? MobilePhone { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class TableInputModel
    {
        public int Count { get; set; } = 1;
        public int Capacity { get; set; } = 8;
    }

    public sealed class ShiftInputModel
    {
        public string Name { get; set; } = string.Empty;
        public TimeOnly? StartsAt { get; set; }
    }

    public sealed class EventInputModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DateInputModel
    {
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    public sealed class EventWizardInputModel
    {
        public DateOnly StartsOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly EndsOn { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(6));
        public string ShiftTemplates { get; set; } = "Pranzo 12:00\r\nCena 19:30";

        [Range(1, 500)]
        public int TablesPerShift { get; set; } = 30;

        [Range(1, 100)]
        public int TableCapacity { get; set; } = 8;
    }

    public sealed class OrganizationInputModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UserInputModel
    {
        public string UserName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.BookingOperator;
    }

    public sealed record ReservationRow(Guid Id, string BookerName, string MobilePhone, string Notes, string ExpectedAt, int PartySize, string StatusLabel, string StatusCssClass, string TableLabel, ReservationTableBadge? TableBadge);
    public sealed record ReservationTableBadge(string Label, int Capacity, int FreeSeats);
    public sealed record AssignmentRow(Guid Id, string TableLabel, string ReservationLabel, string Source, int People, int Capacity);
    public sealed record OrganizationRow(Guid Id, string Name, bool IsCurrentUserOrganization);
    public sealed record EventRow(Guid Id, string Name, bool IsArchived);
    public sealed record DateRow(Guid Id, DateOnly Date);
    private sealed record ReceptionContext(Guid OrganizationId, Guid EventId, Guid EventDateId, Guid ShiftId);

    public sealed record ShiftRow(Guid Id, string Name, string StartsAt, bool IsClosed);
    public sealed record TableRow(Guid Id, string Code, int Capacity, string OccupancyLabel, string Notes);
    public sealed record UserRow(Guid Id, string UserName, string DisplayName, string Role, bool IsActive);
    public sealed record AuditRow(string CreatedAt, string UserName, string EntityName, string EntityId, string Action, string Details);
}
