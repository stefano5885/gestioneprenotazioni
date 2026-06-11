using GestionePrenotazioni.Web.Domain;

namespace GestionePrenotazioni.Web.Services.TableAssignment;

public sealed record TableAssignmentRequest(
    IReadOnlyList<DiningTable> Tables,
    IReadOnlyList<Reservation> Reservations);
