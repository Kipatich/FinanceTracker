using System.Globalization;
using System.Text;

namespace FinanceTracker.Core;

public static class CsvExporter
{
    private static string Cell(string value, bool protectFormula = false)
    {
        // Текст пользователя не должен превращаться в формулу при открытии в Excel.
        var first = value.TrimStart();
        if (protectFormula && first.Length > 0 && "=+-@".Contains(first[0]))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static void Export(string path, IEnumerable<FinancialTransaction> transactions)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(true));
        writer.NewLine = "\r\n";
        writer.WriteLine("Id;Дата;Тип;Сумма;Категория;Комментарий");
        foreach (var item in transactions)
            writer.WriteLine(string.Join(';', Cell(item.Id.ToString()),
                Cell(item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Cell(item.TypeLabel), Cell(item.Amount.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU"))),
                Cell(item.Category, true), Cell(item.Note, true)));
    }
}
