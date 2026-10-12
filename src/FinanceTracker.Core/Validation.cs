using System.Globalization;

namespace FinanceTracker.Core;

public static class Validation
{
    public static decimal ParseAmount(string text)
    {
        // Без разделителей тысяч: 1234.56 или 1234,56.
        if (!decimal.TryParse(text.Trim().Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Введите число без разделителей тысяч: 1234,56.");
        return value;
    }

    public static void Amount(decimal value, bool allowZero = false)
    {
        if (value < 0 || (!allowZero && value == 0) || value > 1_000_000_000m)
            throw new ArgumentException(allowZero
                ? "Сумма должна быть от 0 до 1 000 000 000."
                : "Сумма должна быть больше 0 и не больше 1 000 000 000.");
        if (decimal.Round(value, 2) != value)
            throw new ArgumentException("Допускается не более двух знаков после запятой.");
    }

    public static string MonthKey(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            throw new ArgumentException("Некорректный год или месяц.");
        return $"{year:D4}-{month:D2}";
    }

    public static DateTime ParseMonth(string text)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ArgumentException("Месяц указывается в формате yyyy-MM, например 2026-10.");
        return date;
    }

    public static void Transaction(FinancialTransaction transaction)
    {
        if (transaction.Id == Guid.Empty)
            throw new ArgumentException("Пустой идентификатор операции.");
        if (transaction.Kind is not (TransactionKind.Income or TransactionKind.Expense))
            throw new ArgumentException("Неизвестный вид операции.");
        Amount(transaction.Amount);
        if (string.IsNullOrWhiteSpace(transaction.Category) || transaction.Category.Length > 60)
            throw new ArgumentException("Категория должна содержать от 1 до 60 символов.");
        if (transaction.Note is null || transaction.Note.Length > 500)
            throw new ArgumentException("Комментарий должен содержать не больше 500 символов.");
    }

    public static void Data(FinanceData data)
    {
        if (data.Version != 1)
            throw new InvalidDataException("Версия файла не поддерживается.");
        if (data.Transactions is null || data.MonthlyBudgets is null)
            throw new InvalidDataException("В файле отсутствуют операции или бюджеты.");
        var ids = new HashSet<Guid>();
        foreach (var item in data.Transactions)
        {
            if (item is null)
                throw new InvalidDataException("Пустая операция в файле.");
            Transaction(item);
            if (!ids.Add(item.Id))
                throw new InvalidDataException("Повторяющийся идентификатор операции.");
        }
        foreach (var pair in data.MonthlyBudgets)
        {
            ParseMonth(pair.Key);
            Amount(pair.Value, allowZero: true);
        }
    }
}
