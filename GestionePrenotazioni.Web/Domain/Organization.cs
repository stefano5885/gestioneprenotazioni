namespace GestionePrenotazioni.Web.Domain;

public sealed class Organization
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
