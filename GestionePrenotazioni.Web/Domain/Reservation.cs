namespace GestionePrenotazioni.Web.Domain;

public sealed class Reservation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ShiftId { get; set; }
    public required string BookerName { get; set; }
    public required DateOnly Date { get; set; }
    public TimeOnly? ExpectedAt { get; set; }
    public string? MobilePhone { get; set; }
    public string? Notes { get; set; }
    public required int PartySize { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Entered;
}
