namespace GestionePrenotazioni.Web.Domain;

public sealed class AuditLogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid OrganizationId { get; init; }
    public required Guid UserId { get; init; }
    public required string EntityName { get; init; }
    public required string EntityId { get; init; }
    public required string Action { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
