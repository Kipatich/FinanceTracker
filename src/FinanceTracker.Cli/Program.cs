using System.Globalization;
using FinanceTracker.Core;

return Run(args);

static int Run(string[] args)
{
    try
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            Console.WriteLine(CliHelp.Text);
            return 0;
        }
        var command = args[0].ToLowerInvariant();
        var allowed = command switch
        {
            "list" => new[] { "month", "type", "category", "search", "data" },
            "add" => new[] { "type", "amount", "date", "category", "note", "data" },
            "edit" => new[] { "id", "type", "amount", "date", "category", "note", "data" },
            "delete" => new[] { "id", "data" },
            "summary" => new[] { "month", "data" },
            "budget" => new[] { "month", "amount", "data" },
            "export" => new[] { "file", "month", "type", "category", "search", "data" },
            "demo" => new[] { "month", "data" },
            _ => throw new ArgumentException("Неизвестная команда. Выполните help.")
        };
        // Разбираем пары --имя значение. Неизвестные и повторные параметры отклоняем.
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--") || i + 1 >= args.Length)
                throw new ArgumentException("Каждый параметр имеет вид --имя значение.");
            var key = args[i][2..];
            if (!allowed.Contains(key, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Неизвестный параметр: --{key}.");
            if (!options.TryAdd(key, args[i + 1]))
                throw new ArgumentException($"Повторяющийся параметр: --{key}.");
        }
        // Вспомогательные методы читают параметры из словаря.
        string Required(string name)
        {
            if (options.TryGetValue(name, out string? value))
                return value;
            throw new ArgumentException($"Обязательный параметр: --{name}.");
        }

        string Optional(string name, string fallback)
        {
            if (options.TryGetValue(name, out string? value))
                return value;
            return fallback;
        }

        DateTime Month()
        {
            return Validation.ParseMonth(Optional("month", DateTime.Today.ToString("yyyy-MM")));
        }

        DateTime Date()
        {
            return DateTime.ParseExact(Optional("date", DateTime.Today.ToString("yyyy-MM-dd")),
                "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        TransactionKind Kind(string text)
        {
            if (text.Equals("income", StringComparison.OrdinalIgnoreCase))
                return TransactionKind.Income;
            if (text.Equals("expense", StringComparison.OrdinalIgnoreCase))
                return TransactionKind.Expense;
            throw new ArgumentException("Вид операции: income или expense.");
        }

        TransactionFilter Filter()
        {
            DateTime? month = null;
            if (options.ContainsKey("month"))
                month = Month();

            TransactionKind? kind = null;
            if (options.ContainsKey("type"))
                kind = Kind(options["type"]);

            return new TransactionFilter(month?.Year, month?.Month, kind,
                options.GetValueOrDefault("category"), options.GetValueOrDefault("search"));
        }

        // Дальше вызываем тот же сервис, который используется в окне WPF.
        var path = Optional("data", FinancePaths.DataFile);
        var service = new FinanceService(new JsonTransactionRepository(path));
        switch (command)
        {
            case "add":
                var created = service.Add(Kind(Required("type")), Validation.ParseAmount(Required("amount")),
                    Date(), Required("category"), Optional("note", ""));
                Console.WriteLine($"Добавлено: {created.Id}");
                break;
            case "edit":
                service.Update(Guid.Parse(Required("id")), Kind(Required("type")),
                    Validation.ParseAmount(Required("amount")), Date(), Required("category"), Optional("note", ""));
                Console.WriteLine("Операция обновлена.");
                break;
            case "delete":
                service.Delete(Guid.Parse(Required("id")));
                Console.WriteLine("Операция удалена.");
                break;
            case "list":
                var items = service.GetTransactions(Filter());
                Console.WriteLine("ID | Дата | Тип | Сумма | Категория | Комментарий");
                foreach (var item in items)
                    Console.WriteLine($"{item.Id} | {item.Date:yyyy-MM-dd} | {item.TypeLabel} | {item.Amount:0.00} | {item.Category} | {item.Note.Replace('\n', ' ').Replace('\r', ' ')}");
                Console.WriteLine($"Операций: {items.Count}");
                break;
            case "summary":
                var month = Month();
                var summary = service.GetSummary(month.Year, month.Month);
                Console.WriteLine($"Месяц: {month:yyyy-MM}\nДоходы: {summary.Income:0.00}\nРасходы: {summary.Expenses:0.00}\nОстаток: {summary.Balance:0.00}");
                Console.WriteLine(summary.Budget.HasValue
                    ? $"Бюджет: {summary.Budget:0.00}\nОсталось бюджета: {summary.BudgetRemaining:0.00}"
                    : "Бюджет не задан.");
                break;
            case "budget":
                var budgetMonth = Month();
                var amount = Required("amount");
                service.SetBudget(budgetMonth.Year, budgetMonth.Month,
                    amount.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : Validation.ParseAmount(amount));
                Console.WriteLine("Бюджет сохранён.");
                break;
            case "export":
                CsvExporter.Export(Required("file"), service.GetTransactions(Filter()));
                Console.WriteLine("CSV сохранён.");
                break;
            case "demo":
                var demoMonth = Month();
                service.AddDemoData(demoMonth.Year, demoMonth.Month);
                Console.WriteLine("Добавлены 8 демонстрационных операций и бюджет 30 000.");
                break;
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Ошибка: {ex.Message}");
        return 1;
    }
}

static class CliHelp
{
    public const string Text = """
FinanceTracker — учёт личных финансов. Суммы в рублях, даты yyyy-MM-dd.

Команды:
  list    [--month yyyy-MM] [--type income|expense] [--category "Категория"] [--search "текст"]
  add     --type income|expense --amount 1200.50 --category "Категория" [--date yyyy-MM-dd] [--note "текст"]
  edit    --id GUID --type income|expense --amount 1200.50 --category "Категория" [--date yyyy-MM-dd] [--note "текст"]
  delete  --id GUID
  summary [--month yyyy-MM]
  budget  --amount 30000|none [--month yyyy-MM]
  export  --file "operations.csv" [фильтры команды list]
  demo    [--month yyyy-MM]  (только пустой файл)
  help

Для любой команды: --data "путь/finance.json" (иначе данные общие с WPF).
list и export без --month используют все месяцы.
summary, budget, demo без --month используют текущий месяц.
edit полностью заменяет поля операции; без --date берёт сегодняшнюю дату,
без --note очищает комментарий. Используйте полный GUID из list.
Код завершения: 0 — успех, 1 — ошибка.
""";

}
