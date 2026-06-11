using System.Text;
using System.Globalization;
using GestionePrenotazioni.Web.Domain;

namespace GestionePrenotazioni.Web.Services;

public sealed class ExportService
{
    public static readonly string[] DefaultColumns = ["name", "time", "people", "notes", "tables"];
    public static readonly string[] AvailableColumns = ["name", "time", "people", "notes", "tables", "phone", "status", "date"];

    public byte[] BuildExcel(IReadOnlyList<ReservationExportRow> rows, IReadOnlyList<string> columns)
    {
        var selectedColumns = NormalizeColumns(columns);
        var builder = new StringBuilder();
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>td.table-number{font-weight:700;}</style></head><body><table>");
        builder.AppendLine("<tr>");
        foreach (var column in selectedColumns)
        {
            builder.Append("<th>").Append(Escape(Header(column))).AppendLine("</th>");
        }
        builder.AppendLine("</tr>");

        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            foreach (var column in selectedColumns)
            {
                var cssClass = column.Equals("tables", StringComparison.OrdinalIgnoreCase) ? " class=\"table-number\"" : string.Empty;
                builder.Append("<td").Append(cssClass).Append(">").Append(Escape(Value(row, column))).AppendLine("</td>");
            }
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public byte[] BuildPdf(ReservationExportContext context, IReadOnlyList<ReservationExportRow> rows, IReadOnlyList<string> columns)
    {
        var selectedColumns = NormalizeColumns(columns);
        var pages = BuildPdfPages(context, rows, selectedColumns);
        var pageObjectIds = Enumerable.Range(0, pages.Count).Select(index => 5 + index * 2).ToArray();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };

        foreach (var (content, index) in pages.Select((content, index) => (content, index)))
        {
            var pageObjectId = 5 + index * 2;
            var contentObjectId = pageObjectId + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        return BuildPdfDocument(objects);
    }

    public IReadOnlyList<string> NormalizeColumns(IReadOnlyList<string>? columns)
    {
        var valid = AvailableColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = (columns ?? DefaultColumns)
            .Where(column => valid.Contains(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return selected.Length == 0 ? DefaultColumns : selected;
    }

    public string Header(string column)
    {
        return column.ToLowerInvariant() switch
        {
            "name" => "Nome",
            "date" => "Data",
            "time" => "Ora",
            "phone" => "Cellulare",
            "notes" => "Note",
            "people" => "PAX",
            "status" => "Stato",
            "tables" => "N. tavolo",
            _ => column
        };
    }

    private static string Value(ReservationExportRow row, string column)
    {
        return column.ToLowerInvariant() switch
        {
            "name" => row.BookerName,
            "date" => row.Date,
            "time" => row.ExpectedAt,
            "phone" => row.MobilePhone,
            "notes" => row.Notes,
            "people" => row.PartySize.ToString(),
            "status" => row.Status,
            "tables" => row.Tables,
            _ => string.Empty
        };
    }

    private IReadOnlyList<string> BuildPdfPages(ReservationExportContext context, IReadOnlyList<ReservationExportRow> rows, IReadOnlyList<string> columns)
    {
        const double pageWidth = 595;
        const double pageHeight = 842;
        const double margin = 34;
        const double tableBottom = 34;
        const double headerHeight = 24;
        const double lineHeight = 9.8;
        const double cellPadding = 4;

        var printedAt = DateTime.Now;
        var pages = new List<StringBuilder>();
        var builder = StartPdfPage(context, columns, pageWidth, pageHeight, margin, headerHeight, printedAt);
        var y = 652d;
        var widths = ColumnWidths(columns, pageWidth - margin * 2);

        foreach (var row in rows)
        {
            var wrappedCells = columns
                .Select((column, index) => Wrap(Value(row, column), widths[index] - cellPadding * 2, PdfCellFontSize(column)))
                .ToArray();
            var rowHeight = Math.Max(24, wrappedCells.Max(lines => lines.Count) * lineHeight + cellPadding * 2);
            if (y - rowHeight < tableBottom)
            {
                pages.Add(builder);
                builder = StartPdfPage(context, columns, pageWidth, pageHeight, margin, headerHeight, printedAt);
                y = 652d;
            }

            var x = margin;
            AddFill(builder, x, y - rowHeight, pageWidth - margin * 2, rowHeight, "1 1 1");
            AddStroke(builder, x, y - rowHeight, pageWidth - margin * 2, rowHeight, "0.86 0.89 0.86");
            for (var index = 0; index < columns.Count; index++)
            {
                if (index > 0)
                {
                    AddLine(builder, x, y, x, y - rowHeight, "0.88 0.91 0.88");
                }

                var textY = y - cellPadding - 8;
                foreach (var line in wrappedCells[index].Take(4))
                {
                    var column = columns[index];
                    AddText(builder, x + cellPadding, textY, line, PdfCellFontSize(column), bold: PdfCellIsBold(column));
                    textY -= lineHeight;
                }

                x += widths[index];
            }

            y -= rowHeight;
        }

        pages.Add(builder);
        var pageCount = pages.Count;
        return pages
            .Select((page, index) =>
            {
                AddFooter(page, pageWidth, margin, index + 1, pageCount, printedAt);
                return page.ToString();
            })
            .ToArray();
    }

    private StringBuilder StartPdfPage(ReservationExportContext context, IReadOnlyList<string> columns, double pageWidth, double pageHeight, double margin, double headerHeight, DateTime printedAt)
    {
        var builder = new StringBuilder();
        AddFill(builder, 0, 0, pageWidth, pageHeight, "1 1 1");
        AddText(builder, margin, 798, "LISTA PRENOTAZIONI", 18, bold: true);
        AddText(builder, margin, 776, context.EventName, 13, bold: true);
        AddText(builder, margin, 758, $"{context.OrganizationName} - {context.DateLabel} - Turno {context.ShiftName}", 10, bold: false);
        if (!string.IsNullOrWhiteSpace(context.ShiftTime))
        {
            AddText(builder, margin, 742, $"Ora turno: {context.ShiftTime}", 9, bold: false);
        }

        var statsX = margin;
        AddFill(builder, statsX, 688, pageWidth - margin * 2, 38, "0.90 0.95 0.93");
        AddStroke(builder, statsX, 688, pageWidth - margin * 2, 38, "0.75 0.84 0.80");
        AddText(builder, statsX + 12, 711, $"Prenotazioni: {context.ReservationCount}", 10.5, bold: true);
        AddText(builder, statsX + 180, 711, $"Persone prenotate: {context.BookedPeople}", 10.5, bold: true);
        AddText(builder, statsX + 12, 696, $"Data/ora stampa: {printedAt:dd/MM/yyyy HH:mm}", 8.5, bold: false);

        var tableWidth = pageWidth - margin * 2;
        var widths = ColumnWidths(columns, tableWidth);
        var y = 676d;
        AddFill(builder, margin, y - headerHeight, tableWidth, headerHeight, "0.89 0.94 0.91");
        AddStroke(builder, margin, y - headerHeight, tableWidth, headerHeight, "0.75 0.84 0.80");
        var x = margin;
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                AddLine(builder, x, y, x, y - headerHeight, "0.75 0.84 0.80");
            }

            AddText(builder, x + 5, y - 15, Header(columns[index]).ToUpperInvariant(), 8, bold: true);
            x += widths[index];
        }

        return builder;
    }

    private static double[] ColumnWidths(IReadOnlyList<string> columns, double totalWidth)
    {
        var weights = columns.Select(column => column.ToLowerInvariant() switch
        {
            "name" => 2.4,
            "time" => 0.8,
            "people" => 0.55,
            "notes" => 3.2,
            "tables" => 1.2,
            "phone" => 1.3,
            "status" => 1.3,
            "date" => 1.1,
            _ => 1
        }).ToArray();
        var totalWeight = weights.Sum();
        return weights.Select(weight => totalWidth * weight / totalWeight).ToArray();
    }

    private static double PdfCellFontSize(string column)
    {
        return column.ToLowerInvariant() switch
        {
            "name" => 9.4,
            "tables" => 9.8,
            _ => 8.2
        };
    }

    private static bool PdfCellIsBold(string column)
    {
        return column.Equals("tables", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Wrap(string? value, double width, double fontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [string.Empty];
        }

        var text = value.Trim();
        var maxChars = Math.Max(5, (int)Math.Floor(width / (fontSize * 0.52)));
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            if (word.Length > maxChars)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                for (var index = 0; index < word.Length; index += maxChars)
                {
                    lines.Add(word[index..Math.Min(index + maxChars, word.Length)]);
                }
                continue;
            }

            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (candidate.Length > maxChars)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines.Count == 0 ? ["-"] : lines;
    }

    private static void AddText(StringBuilder builder, double x, double y, string text, double size, bool bold)
    {
        builder.AppendLine("0.06 0.10 0.09 rg");
        builder.AppendLine("BT");
        builder.AppendLine($"/{(bold ? "F2" : "F1")} {Format(size)} Tf");
        builder.AppendLine($"{Format(x)} {Format(y)} Td");
        builder.AppendLine($"{EscapePdfText(text)} Tj");
        builder.AppendLine("ET");
    }

    private static void AddFill(StringBuilder builder, double x, double y, double width, double height, string color)
    {
        builder.AppendLine($"{color} rg");
        builder.AppendLine($"{Format(x)} {Format(y)} {Format(width)} {Format(height)} re f");
    }

    private static void AddStroke(StringBuilder builder, double x, double y, double width, double height, string color)
    {
        builder.AppendLine($"{color} RG");
        builder.AppendLine("0.7 w");
        builder.AppendLine($"{Format(x)} {Format(y)} {Format(width)} {Format(height)} re S");
    }

    private static void AddLine(StringBuilder builder, double x1, double y1, double x2, double y2, string color)
    {
        builder.AppendLine($"{color} RG");
        builder.AppendLine("0.5 w");
        builder.AppendLine($"{Format(x1)} {Format(y1)} m {Format(x2)} {Format(y2)} l S");
    }

    private static void AddFooter(StringBuilder builder, double pageWidth, double margin, int pageNumber, int pageCount, DateTime printedAt)
    {
        AddText(builder, margin, 18, $"Data/ora stampa: {printedAt:dd/MM/yyyy HH:mm}", 7, bold: false);
        AddText(builder, pageWidth - margin - 75, 18, $"Pagina {pageNumber} di {pageCount}", 7, bold: false);
    }

    private static string Format(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string EscapePdfText(string? value)
    {
        var text = NormalizeForPdf(value ?? string.Empty);
        var builder = new StringBuilder("(");
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '(' => "\\(",
                ')' => "\\)",
                '\r' => string.Empty,
                '\n' => " ",
                _ when character < 32 || character > 126 => " ",
                _ => character
            });
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static string NormalizeForPdf(string value)
    {
        return value
            .Replace("à", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("è", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("ì", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ò", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ù", "u", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildPdfDocument(IReadOnlyList<string> objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefOffset = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            writer.WriteLine($"{offset:0000000000} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset);
        writer.WriteLine("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string Escape(string? value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

}

public sealed record ReservationExportContext(
    string OrganizationName,
    string EventName,
    string DateLabel,
    string ShiftName,
    string ShiftTime,
    int ReservationCount,
    int BookedPeople);

public sealed record ReservationExportRow(
    string BookerName,
    string Date,
    string ExpectedAt,
    string MobilePhone,
    string Notes,
    int PartySize,
    string Status,
    string Tables);
