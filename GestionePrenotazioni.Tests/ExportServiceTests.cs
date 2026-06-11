using GestionePrenotazioni.Web.Services;

namespace GestionePrenotazioni.Tests;

public sealed class ExportServiceTests
{
    private readonly ExportService service = new();

    [Fact]
    public void ExcelExportUsesConfiguredColumnOrder()
    {
        var bytes = service.BuildExcel(
            [new ReservationExportRow("Rossi", "01/06/2026", "19:30", "333", "Vicino ingresso", 4, "Inserita", "T01")],
            ["people", "name"]);

        var content = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("<th>PAX</th>", content);
        Assert.Contains("<th>Nome</th>", content);
        Assert.True(content.IndexOf("<th>PAX</th>", StringComparison.Ordinal) < content.IndexOf("<th>Nome</th>", StringComparison.Ordinal));
    }

    [Fact]
    public void PdfExportProducesPdfHeader()
    {
        var bytes = service.BuildPdf(
            new ReservationExportContext("PRO LOCO", "GRILLMUSIC 2026", "12/06/2026", "CENA", "19:30", 1, 4),
            [new ReservationExportRow("Rossi", "01/06/2026", "19:30", "333", "Vicino ingresso", 4, "Inserita", "T01")],
            ExportService.DefaultColumns);

        var header = System.Text.Encoding.ASCII.GetString(bytes[..8]);
        Assert.StartsWith("%PDF-1.4", header, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfExportUsesRequestedColumnOrder()
    {
        var bytes = service.BuildPdf(
            new ReservationExportContext("PRO LOCO", "GRILLMUSIC 2026", "12/06/2026", "CENA", "19:30", 1, 4),
            [new ReservationExportRow("Rossi", "01/06/2026", "19:30", "333", "Vicino ingresso", 4, "Inserita", "03")],
            ["tables", "name", "people"]);

        var content = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("%PDF-1.4", content);
        Assert.Contains("/MediaBox [0 0 595 842]", content);
        Assert.Contains("Rossi", content);
        Assert.Contains("N. TAVOLO", content);
        Assert.Contains("PAX", content);
        Assert.Contains("Pagina 1 di 1", content);
        Assert.Contains("Data/ora stampa:", content);
    }

    [Fact]
    public void ExcelExportLeavesEmptyNotesAndTablesBlank()
    {
        var bytes = service.BuildExcel(
            [new ReservationExportRow("Rossi", "01/06/2026", "19:30", "333", string.Empty, 4, "Inserita", string.Empty)],
            ["name", "notes", "tables"]);

        var content = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("Da assegnare", content);
        Assert.DoesNotContain("<td>-</td>", content);
        Assert.Contains("<td></td>", content);
    }
}
