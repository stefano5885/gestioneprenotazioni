namespace GestionePrenotazioni.Web.Domain;

public sealed class TableAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ShiftId { get; set; }
    public List<Guid> TableIds { get; set; } = [];
    public List<Guid> ReservationIds { get; set; } = [];
    public required AssignmentSource Source { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
