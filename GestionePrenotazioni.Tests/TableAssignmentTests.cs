using GestionePrenotazioni.Web.Domain;
using GestionePrenotazioni.Web.Services.TableAssignment;

namespace GestionePrenotazioni.Tests;

public sealed class TableAssignmentTests
{
    private readonly GreedyTableAssignmentService service = new();

    [Fact]
    public void AssignsOneReservationPerTableWhenTablesAreEnough()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 3),
            Reservations(shiftId, 2, 2, 2)));

        Assert.Equal(3, result.Assignments.Count);
        Assert.All(result.Assignments, assignment => Assert.Single(assignment.ReservationIds));
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void CompactsThreeReservationsOfTwoWhenTablesAreLimited()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 1),
            Reservations(shiftId, 2, 2, 2)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(3, assignment.ReservationIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void LargeReservationUsesTablesRoundedUpToEightSeatBlocks()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 3),
            Reservations(shiftId, 17)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(3, assignment.TableIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void LargeReservationCanAddSmallCompanionWhenAtLeastFourSeatsRemainAndTablesAreLimited()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 3),
            Reservations(shiftId, 18, 2)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(2, assignment.ReservationIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void DoesNotAddSmallCompanionToLargeReservationWhenFewerThanFourSeatsRemain()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 2),
            Reservations(shiftId, 14, 2)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Single(assignment.ReservationIds);
        Assert.Single(result.UnassignedReservations);
    }

    [Fact]
    public void CompactsFourAndTwoWhenTablesAreLimited()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 1),
            Reservations(shiftId, 4, 2)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(2, assignment.ReservationIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void CompactsTwoReservationsOfThreeWhenTablesAreLimited()
    {
        var shiftId = Guid.NewGuid();
        var result = service.Assign(new TableAssignmentRequest(
            Tables(shiftId, 1),
            Reservations(shiftId, 3, 3)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(2, assignment.ReservationIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    [Fact]
    public void UsesMultipleTablesWhenConfiguredCapacityIsLowerThanEight()
    {
        var shiftId = Guid.NewGuid();
        var tables = new[]
        {
            new DiningTable { ShiftId = shiftId, Code = "T01", Capacity = 4 },
            new DiningTable { ShiftId = shiftId, Code = "T02", Capacity = 4 }
        };

        var result = service.Assign(new TableAssignmentRequest(
            tables,
            Reservations(shiftId, 6)));

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(2, assignment.TableIds.Count);
        Assert.Empty(result.UnassignedReservations);
    }

    private static IReadOnlyList<DiningTable> Tables(Guid shiftId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new DiningTable
            {
                ShiftId = shiftId,
                Code = $"T{index:00}",
                Capacity = 8
            })
            .ToArray();
    }

    private static IReadOnlyList<Reservation> Reservations(Guid shiftId, params int[] partySizes)
    {
        return partySizes.Select((partySize, index) => new Reservation
        {
            ShiftId = shiftId,
            BookerName = $"Prenotazione {index + 1}",
            Date = new DateOnly(2026, 6, 1),
            PartySize = partySize
        }).ToArray();
    }
}
