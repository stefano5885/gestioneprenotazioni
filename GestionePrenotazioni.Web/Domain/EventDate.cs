namespace GestionePrenotazioni.Web.Domain;

public sealed class EventDate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid EventId { get; set; }
    public required DateOnly Date { get; set; }
    public bool IsActive { get; set; } = true;
}
