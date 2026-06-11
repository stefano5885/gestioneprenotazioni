namespace GestionePrenotazioni.Web.Domain;

public sealed class ApplicationUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid OrganizationId { get; set; }
    public required string UserName { get; set; }
    public string? DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public required UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
