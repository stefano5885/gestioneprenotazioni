using GestionePrenotazioni.Web.Domain;
using GestionePrenotazioni.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GestionePrenotazioni.Tests;

public sealed class AppStoreTests
{
    [Fact]
    public void DeletingShiftCascadesTablesReservationsAndAssignments()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var table = store.Tables.First(item => item.ShiftId == shift.Id);
            var reservation = store.Reservations.First(item => item.ShiftId == shift.Id && item.PartySize <= table.Capacity);

            Assert.True(store.AssignReservationManually(reservation.Id, [table.Id], user.Id, out var error), error);

            Assert.True(store.DeleteShift(shift.Id, user.Id, out error), error);

            Assert.DoesNotContain(store.Shifts, item => item.Id == shift.Id);
            Assert.DoesNotContain(store.Tables, item => item.ShiftId == shift.Id);
            Assert.DoesNotContain(store.Reservations, item => item.ShiftId == shift.Id);
            Assert.DoesNotContain(store.Assignments, item => item.ShiftId == shift.Id);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void FailedManualReassignmentKeepsPreviousAssignment()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Count(table => table.ShiftId == item.Id) >= 2);
            var date = store.Dates.First(item => item.Id == shift.EventDateId);
            var tables = store.Tables.Where(item => item.ShiftId == shift.Id).OrderBy(item => item.Code).Take(2).ToArray();
            var movingReservation = store.AddReservation(shift.Id, "Da spostare", date.Date, 4, null, null, user.Id);
            var fullTableReservation = store.AddReservation(shift.Id, "Tavolo pieno", date.Date, tables[1].Capacity, null, null, user.Id);

            Assert.True(store.AssignReservationManually(movingReservation.Id, [tables[0].Id], user.Id, out var error), error);
            Assert.True(store.AssignReservationManually(fullTableReservation.Id, [tables[1].Id], user.Id, out error), error);

            Assert.False(store.AssignReservationManually(movingReservation.Id, [tables[1].Id], user.Id, out error));

            var assignment = store.Assignments.Single(item => item.ReservationIds.Contains(movingReservation.Id));
            Assert.Contains(tables[0].Id, assignment.TableIds);
            Assert.DoesNotContain(tables[1].Id, assignment.TableIds);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void ManualAssignmentRejectsTablesFromAnotherShift()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var otherShift = store.Shifts.First(item => item.Id != shift.Id);
            store.AddTables(otherShift.Id, 1, 8, user.Id);
            var otherShiftTable = store.Tables.First(item => item.ShiftId == otherShift.Id);
            var reservation = store.Reservations.First(item => item.ShiftId == shift.Id);

            Assert.False(store.AssignReservationManually(reservation.Id, [otherShiftTable.Id], user.Id, out var error));
            Assert.Contains("turno", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(store.Assignments, item => item.ReservationIds.Contains(reservation.Id));
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void MarkNoShowKeepsReservationAndFreesTables()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var table = store.Tables.First(item => item.ShiftId == shift.Id);
            var reservation = store.Reservations.First(item => item.ShiftId == shift.Id && item.PartySize <= table.Capacity);

            Assert.True(store.AssignReservationManually(reservation.Id, [table.Id], user.Id, out var error), error);

            store.MarkNoShow(reservation.Id, user.Id);

            var updatedReservation = store.Reservations.Single(item => item.Id == reservation.Id);
            Assert.Equal(ReservationStatus.NoShow, updatedReservation.Status);
            Assert.DoesNotContain(store.Assignments, item => item.ReservationIds.Contains(reservation.Id));
            Assert.DoesNotContain(store.Assignments, item => item.TableIds.Contains(table.Id));

            Assert.True(store.ReplaceAssignments(shift.Id, [], user.Id, out error), error);
            Assert.Equal(ReservationStatus.NoShow, store.Reservations.Single(item => item.Id == reservation.Id).Status);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void ReplaceAssignmentsRejectsCapacityOverflow()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var date = store.Dates.First(item => item.Id == shift.EventDateId);
            var table = store.Tables.First(item => item.ShiftId == shift.Id);
            var reservation = store.AddReservation(shift.Id, "Troppi posti", date.Date, table.Capacity + 1, null, null, user.Id);
            var assignment = new TableAssignment
            {
                ShiftId = shift.Id,
                TableIds = [table.Id],
                ReservationIds = [reservation.Id],
                Source = AssignmentSource.Automatic
            };

            Assert.False(store.ReplaceAssignments(shift.Id, [assignment], user.Id, out var error));
            Assert.Contains("capienza", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(store.Assignments, item => item.ShiftId == shift.Id);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void StoreRejectsInvalidPartySizes()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First();
            var date = store.Dates.First(item => item.Id == shift.EventDateId);
            var reservation = store.AddReservation(shift.Id, "Valida", date.Date, 1, null, null, user.Id);

            Assert.Throws<ArgumentOutOfRangeException>(() => store.AddReservation(shift.Id, "Zero", date.Date, 0, null, null, user.Id));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.UpdateReservation(reservation.Id, "Zero", 0, null, null, user.Id));
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void AddReservationStoresCreatorAndCreationTime()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First();
            var date = store.Dates.First(item => item.Id == shift.EventDateId);
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);

            var reservation = store.AddReservation(shift.Id, "Creatore", date.Date, 2, null, null, user.Id);

            Assert.Equal(user.Id, reservation.CreatedByUserId);
            Assert.True(reservation.CreatedAt >= before);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void ManualAssignmentRejectsNoShowReservations()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var table = store.Tables.First(item => item.ShiftId == shift.Id);
            var reservation = store.Reservations.First(item => item.ShiftId == shift.Id && item.PartySize <= table.Capacity);

            store.MarkNoShow(reservation.Id, user.Id);

            Assert.False(store.AssignReservationManually(reservation.Id, [table.Id], user.Id, out var error));
            Assert.Contains("non presentata", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void LoginAttemptsAreAudited()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.ValidateUser("admin", "admin");
            Assert.NotNull(user);

            store.RecordLoginAttempt("admin", succeeded: true, user);
            store.RecordLoginAttempt("admin", succeeded: false, user: null);

            Assert.Contains(store.AuditLogs, item => item.Action == "login-success");
            Assert.Contains(store.AuditLogs, item => item.Action == "login-failed");
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void AccessChecksSeparateOrganizations()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var admin = store.Users.First(item => item.Role == UserRole.Admin);
            var bookingOperator = store.Users.First(item => item.Role == UserRole.BookingOperator);
            var otherOrganization = store.Organizations.First(item => item.Id != bookingOperator.OrganizationId);
            var otherEvent = store.AddEvent(otherOrganization.Id, "Altro evento", admin.Id);
            var otherDate = store.AddEventDate(otherEvent.Id, new DateOnly(2026, 7, 1), admin.Id);
            var otherShift = store.AddShift(otherDate.Id, "Cena", new TimeOnly(19, 30), admin.Id);
            var reservation = store.AddReservation(otherShift.Id, "Altra org", otherDate.Date, 2, null, null, admin.Id);

            Assert.False(store.UserCanAccessReservation(bookingOperator, reservation.Id));
            Assert.True(store.UserCanAccessReservation(admin, reservation.Id));
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void ConfigurationRejectsDuplicates()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var organization = store.Organizations.First();
            var fairEvent = store.Events.First(item => item.OrganizationId == organization.Id);
            var shift = store.Shifts.First(item => store.Tables.Any(table => table.ShiftId == item.Id));
            var date = store.Dates.First(item => item.Id == shift.EventDateId);
            var table = store.Tables.First(item => item.ShiftId == shift.Id);
            var otherTable = store.Tables.First(item => item.ShiftId == shift.Id && item.Id != table.Id);

            Assert.Throws<InvalidOperationException>(() => store.AddOrganization(organization.Name, user.Id));
            Assert.Throws<InvalidOperationException>(() => store.AddUser(organization.Id, "admin", "nuova-password", UserRole.BookingOperator, null, user.Id));
            Assert.Throws<InvalidOperationException>(() => store.AddEvent(organization.Id, fairEvent.Name, user.Id));
            Assert.Throws<InvalidOperationException>(() => store.AddEventDate(fairEvent.Id, date.Date, user.Id));
            Assert.Throws<InvalidOperationException>(() => store.AddShift(date.Id, shift.Name, null, user.Id));
            Assert.Throws<InvalidOperationException>(() => store.UpdateTable(otherTable.Id, table.Code, otherTable.Capacity, null, user.Id));
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void UsersCanBeDeletedExceptCurrentUserAndLastActiveAdmin()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var admin = store.Users.First(item => item.Role == UserRole.Admin);
            var organization = store.Organizations.First(item => item.Id == admin.OrganizationId);
            var extraUser = store.AddUser(organization.Id, "temporaneo", "password", UserRole.BookingOperator, null, admin.Id);

            Assert.False(store.DeleteUser(admin.Id, admin.Id, out var error));
            Assert.Contains("accesso", error, StringComparison.OrdinalIgnoreCase);

            Assert.True(store.DeleteUser(extraUser.Id, admin.Id, out error), error);
            Assert.DoesNotContain(store.Users, item => item.Id == extraUser.Id);

            var otherAdmin = store.AddUser(organization.Id, "altroadmin", "password", UserRole.Admin, null, admin.Id);
            Assert.True(store.DeleteUser(admin.Id, otherAdmin.Id, out error), error);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void OnlyOneDefaultEventIsAllowedPerOrganization()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var admin = store.Users.First(item => item.Role == UserRole.Admin);
            var organization = store.Organizations.First(item => item.Id == admin.OrganizationId);
            var firstEvent = store.Events.First(item => item.OrganizationId == organization.Id);
            var secondEvent = store.AddEvent(organization.Id, "Secondo evento", admin.Id);

            store.SetDefaultEvent(firstEvent.Id, admin.Id);
            store.SetDefaultEvent(secondEvent.Id, admin.Id);

            Assert.False(store.Events.Single(item => item.Id == firstEvent.Id).IsDefault);
            Assert.True(store.Events.Single(item => item.Id == secondEvent.Id).IsDefault);

            store.SetEventArchived(secondEvent.Id, isArchived: true, admin.Id);
            Assert.False(store.Events.Single(item => item.Id == secondEvent.Id).IsDefault);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void GuidedEventSetupCreatesScheduleWithoutDuplicatingExistingItems()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var organization = store.Organizations.First();
            var fairEvent = store.AddEvent(organization.Id, "Evento guidato", user.Id);
            var shifts = new[]
            {
                new GuidedEventShift("Pranzo", new TimeOnly(12, 0)),
                new GuidedEventShift("Cena", new TimeOnly(19, 30))
            };

            var result = store.ConfigureEventSchedule(
                fairEvent.Id,
                new DateOnly(2026, 6, 10),
                new DateOnly(2026, 6, 12),
                shifts,
                tablesPerShift: 3,
                tableCapacity: 8,
                user.Id);

            Assert.Equal(3, result.CreatedDates);
            Assert.Equal(6, result.CreatedShifts);
            Assert.Equal(18, result.CreatedTables);
            Assert.Equal(3, store.Dates.Count(item => item.EventId == fairEvent.Id));
            var dateIds = store.Dates.Where(item => item.EventId == fairEvent.Id).Select(item => item.Id).ToHashSet();
            var shiftIds = store.Shifts.Where(item => dateIds.Contains(item.EventDateId)).Select(item => item.Id).ToHashSet();
            Assert.Equal(6, shiftIds.Count);
            Assert.Equal(18, store.Tables.Count(item => shiftIds.Contains(item.ShiftId)));

            var secondResult = store.ConfigureEventSchedule(
                fairEvent.Id,
                new DateOnly(2026, 6, 10),
                new DateOnly(2026, 6, 12),
                shifts,
                tablesPerShift: 3,
                tableCapacity: 8,
                user.Id);

            Assert.Equal(0, secondResult.CreatedDates);
            Assert.Equal(0, secondResult.CreatedShifts);
            Assert.Equal(0, secondResult.CreatedTables);
            Assert.Equal(3, secondResult.ReusedDates);
            Assert.Equal(6, secondResult.ReusedShifts);
            Assert.Equal(18, store.Tables.Count(item => shiftIds.Contains(item.ShiftId)));
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    [Fact]
    public void ArchivePastShiftsClosesOnlyPastShifts()
    {
        var dataPath = CreateDataPath();
        try
        {
            var store = CreateStore(dataPath);
            var user = store.Users.First(item => item.Role == UserRole.Admin);
            var organization = store.Organizations.First();
            var fairEvent = store.AddEvent(organization.Id, "Evento archivio", user.Id);
            var yesterday = store.AddEventDate(fairEvent.Id, new DateOnly(2026, 6, 1), user.Id);
            var today = store.AddEventDate(fairEvent.Id, new DateOnly(2026, 6, 2), user.Id);
            var yesterdayShift = store.AddShift(yesterday.Id, "Cena passata", new TimeOnly(19, 30), user.Id);
            var lunch = store.AddShift(today.Id, "Pranzo", new TimeOnly(12, 30), user.Id);
            var dinner = store.AddShift(today.Id, "Cena", new TimeOnly(19, 30), user.Id);

            var count = store.ArchivePastShifts(fairEvent.Id, new DateTime(2026, 6, 2, 20, 0, 0), user.Id);

            Assert.Equal(2, count);
            Assert.True(store.Shifts.First(item => item.Id == yesterdayShift.Id).IsClosed);
            Assert.True(store.Shifts.First(item => item.Id == lunch.Id).IsClosed);
            Assert.False(store.Shifts.First(item => item.Id == dinner.Id).IsClosed);
        }
        finally
        {
            DeleteDataPath(dataPath);
        }
    }

    private static AppStore CreateStore(string dataPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppData:Path"] = dataPath })
            .Build();
        var environment = new TestWebHostEnvironment(dataPath);
        return new AppStore(environment, configuration, new PasswordService());
    }

    private static string CreateDataPath()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), "GestionePrenotazioni.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        return dataPath;
    }

    private static void DeleteDataPath(string dataPath)
    {
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "GestionePrenotazioni.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
    }
}
