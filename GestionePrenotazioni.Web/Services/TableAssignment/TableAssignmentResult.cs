using GestionePrenotazioni.Web.Domain;

namespace GestionePrenotazioni.Web.Services.TableAssignment;

public sealed record TableAssignmentResult(
    IReadOnlyList<Domain.TableAssignment> Assignments,
    IReadOnlyList<Reservation> UnassignedReservations,
    int UsedSeats,
    int AvailableSeats);
