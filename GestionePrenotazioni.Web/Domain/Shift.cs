namespace GestionePrenotazioni.Web.Domain;

public sealed class Shift
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid EventDateId { get; set; }
    public required string Name { get; set; }
    public TimeOnly? StartsAt { get; set; }
    public bool IsClosed { get; set; }
}
