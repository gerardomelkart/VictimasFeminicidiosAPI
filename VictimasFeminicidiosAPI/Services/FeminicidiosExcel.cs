using System.Globalization;
using ClosedXML.Excel;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Services;

public sealed class FeminicidiosExcel
{
    private static readonly System.Reflection.PropertyInfo[] Properties = FeminicidioRegistro.Columnas
        .Select(name => typeof(FeminicidioRegistro).GetProperty(name)!).ToArray();

    public byte[] Generar(IReadOnlyList<FeminicidioRegistro> registros, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Hoja1");
        for (var col = 0; col < Properties.Length; col++) sheet.Cell(1, col + 1).Value = Properties[col].Name;
        for (var index = 0; index < registros.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            for (var col = 0; col < Properties.Length; col++)
            {
                var property = Properties[col];
                var value = property.GetValue(registros[index]);
                if (value is null) continue;
                var cell = sheet.Cell(index + 2, col + 1);
                if (property.Name is "FechaNacimiento" or "FechaInicioCarpeta" or "FechaHechos")
                {
                    cell.Value = DateTime.ParseExact((string)value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    cell.Style.DateFormat.Format = "yyyy-mm-dd";
                }
                else if (property.Name is "HoraInicioCarpeta" or "HoraHechos")
                {
                    if ((string)value == "") continue;
                    cell.Value = TimeSpan.ParseExact((string)value, @"hh\:mm\:ss", CultureInfo.InvariantCulture);
                    cell.Style.NumberFormat.Format = "hh:mm:ss";
                }
                else if (value is DateTime timestamp)
                {
                    cell.Value = timestamp;
                    cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                }
                else if (value is int number) cell.Value = number;
                else cell.Value = (string)value; // Texto literal, nunca FormulaA1: protege CURP y evita fórmulas.
            }
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, registros.Count + 1), Properties.Length).SetAutoFilter();
        sheet.Columns(1, Properties.Length).Width = 22;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

