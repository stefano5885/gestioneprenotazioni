using GestionePrenotazioni.Web.Domain;

namespace GestionePrenotazioni.Web.Services.TableAssignment;

public sealed class GreedyTableAssignmentService : ITableAssignmentService
{
    private const int StandardCapacity = 8;

    public TableAssignmentResult Assign(TableAssignmentRequest request)
    {
        var availableTables = request.Tables
            .OrderBy(table => table.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pending = request.Reservations
            .Where(reservation => reservation.Status is not ReservationStatus.Cancelled)
            .OrderByDescending(reservation => reservation.PartySize)
            .ThenBy(reservation => reservation.BookerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assignments = new List<Domain.TableAssignment>();
        var unassigned = new List<Reservation>();

        foreach (var reservation in pending.ToList())
        {
            if (!pending.Contains(reservation))
            {
                continue;
            }

            if (reservation.PartySize <= 0)
            {
                unassigned.Add(reservation);
                pending.Remove(reservation);
                continue;
            }

            var tables = TakeTablesForReservation(availableTables, reservation.PartySize);
            if (tables.Count == 0)
            {
                unassigned.Add(reservation);
                pending.Remove(reservation);
                continue;
            }

            foreach (var table in tables)
            {
                availableTables.Remove(table);
            }

            pending.Remove(reservation);

            var reservationIds = new List<Guid> { reservation.Id };
            var remainingSeats = tables.Sum(table => table.Capacity) - reservation.PartySize;

            var shouldCompact = availableTables.Count < pending.Sum(candidate => RequiredTablesForStandardCapacity(candidate.PartySize));

            if (shouldCompact && reservation.PartySize > StandardCapacity && remainingSeats >= 4)
            {
                var companion = pending.FirstOrDefault(candidate => candidate.PartySize is <= 2 and > 0 && candidate.PartySize <= remainingSeats);
                if (companion is not null)
                {
                    reservationIds.Add(companion.Id);
                    pending.Remove(companion);
                }
            }
            else if (shouldCompact && reservation.PartySize <= StandardCapacity)
            {
                AddAllowedCompanions(reservation, pending, reservationIds, remainingSeats);
            }

            assignments.Add(new Domain.TableAssignment
            {
                ShiftId = reservation.ShiftId,
                TableIds = tables.Select(table => table.Id).ToList(),
                ReservationIds = reservationIds,
                Source = AssignmentSource.Automatic
            });
        }

        unassigned.AddRange(pending);

        var usedSeats = request.Reservations
            .Where(reservation => assignments.Any(assignment => assignment.ReservationIds.Contains(reservation.Id)))
            .Sum(reservation => reservation.PartySize);

        return new TableAssignmentResult(
            assignments,
            unassigned,
            usedSeats,
            availableTables.Sum(table => table.Capacity));
    }

    private static IReadOnlyList<DiningTable> TakeTablesForReservation(List<DiningTable> availableTables, int partySize)
    {
        var selected = new List<DiningTable>();
        var capacity = 0;

        foreach (var table in availableTables)
        {
            selected.Add(table);
            capacity += table.Capacity;
            if (capacity >= partySize)
            {
                return selected;
            }
        }

        return [];
    }

    private static int RequiredTablesForStandardCapacity(int partySize)
    {
        return (int)Math.Ceiling(partySize / (double)StandardCapacity);
    }

    private static void AddAllowedCompanions(
        Reservation primary,
        List<Reservation> pending,
        List<Guid> reservationIds,
        int remainingSeats)
    {
        var companions = primary.PartySize switch
        {
            2 => pending.Where(candidate => candidate.PartySize == 2).Take(2).ToList(),
            3 => pending.Where(candidate => candidate.PartySize == 3).Take(1).ToList(),
            4 => pending.Where(candidate => candidate.PartySize == 2).Take(1).ToList(),
            _ => []
        };

        foreach (var companion in companions)
        {
            if (companion.PartySize > remainingSeats)
            {
                continue;
            }

            reservationIds.Add(companion.Id);
            pending.Remove(companion);
            remainingSeats -= companion.PartySize;
        }
    }
}
