namespace GestionePrenotazioni.Web.Domain;

public sealed class DiningTable
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ShiftId { get; set; }
    public required string Code { get; set; }
    public int Capacity { get; set; } = 8;
    public string? Notes { get; set; }
}
