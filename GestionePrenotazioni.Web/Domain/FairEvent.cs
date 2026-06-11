namespace GestionePrenotazioni.Web.Domain;

public sealed class FairEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
}
