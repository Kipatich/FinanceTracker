using System.Text;
using System.Text.Json;
using FinanceTracker.Core;

var passed = 0;
var failed = 0;
void Test(string name, Action body)
{
    try
    {
        body();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Ожидалось {expected}, получено {actual}");
}
void Throws<T>(Action body) where T : Exception
{
    try
    {
        body();
    }
    catch (T) { return; }
    throw new Exception($"Ожидалось исключение {typeof(T).Name}");
}
FinanceService Fresh() => new(new MemoryRepository());
var date = new DateTime(2026, 10, 12);
var dir = Path.Combine(Path.GetTempPath(), "FinanceTracker-tests-" + Guid.NewGuid());
Directory.CreateDirectory(dir);
try
{
    Test("Пустая сводка", () => Equal(0m, Fresh().GetSummary(2026, 10).Balance));
    Test("Добавление дохода", () => { var s = Fresh(); s.Add(TransactionKind.Income, 100m, date, "Зарплата"); Equal(100m, s.GetSummary(2026, 10).Income); });
    Test("Добавление расхода", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 40m, date, "Еда"); Equal(40m, s.GetSummary(2026, 10).Expenses); });
    Test("Точность decimal", () => { var s = Fresh(); s.Add(TransactionKind.Income, .1m, date, "A"); s.Add(TransactionKind.Income, .2m, date, "A"); Equal(.3m, s.GetSummary(2026, 10).Income); });
    Test("Разность доходов и расходов", () => { var s = Fresh(); s.Add(TransactionKind.Income, 100m, date, "A"); s.Add(TransactionKind.Expense, 35m, date, "A"); Equal(65m, s.GetSummary(2026, 10).Balance); });
    Test("Запрет нуля в операции", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 0m, date, "A")));
    Test("Запрет отрицательной суммы", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, -1m, date, "A")));
    Test("Запрет трёх знаков", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 1.001m, date, "A")));
    Test("Ограничение суммы", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 1_000_000_001m, date, "A")));
    Test("Пустая категория", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 1m, date, "  ")));
    Test("Длинная категория", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 1m, date, new string('A', 61))));
    Test("Длинный комментарий", () => Throws<ArgumentException>(() => Fresh().Add(TransactionKind.Expense, 1m, date, "A", new string('x', 501))));
    Test("Обрезка пробелов", () => Equal("Еда", Fresh().Add(TransactionKind.Expense, 1m, date, " Еда ").Category));
    Test("Событие при добавлении", () => { var s = Fresh(); var n = 0; s.DataChanged += (_, _) => n++; s.Add(TransactionKind.Income, 1m, date, "A"); Equal(1, n); });
    Test("Нет события при ошибке", () => { var s = Fresh(); var n = 0; s.DataChanged += (_, _) => n++; Throws<ArgumentException>(() => s.Add(TransactionKind.Income, 0m, date, "A")); Equal(0, n); });
    Test("Редактирование сохраняет ID", () => { var s = Fresh(); var a = s.Add(TransactionKind.Expense, 1m, date, "A"); s.Update(a.Id, TransactionKind.Income, 9m, date, "B"); Equal(a.Id, s.GetTransactions()[0].Id); Equal(9m, s.GetSummary(2026, 10).Income); });
    Test("Удаление", () => { var s = Fresh(); var a = s.Add(TransactionKind.Expense, 1m, date, "A"); s.Delete(a.Id); Equal(0, s.GetTransactions().Count); });
    Test("Удаление неизвестного ID", () => Throws<KeyNotFoundException>(() => Fresh().Delete(Guid.NewGuid())));
    Test("Изменение неизвестного ID", () => Throws<KeyNotFoundException>(() => Fresh().Update(Guid.NewGuid(), TransactionKind.Income, 1m, date, "A")));
    Test("Фильтр месяца", () => { var s = Fresh(); s.Add(TransactionKind.Income, 1m, date, "A"); s.Add(TransactionKind.Income, 2m, date.AddMonths(1), "A"); Equal(1, s.GetTransactions(new(2026, 10)).Count); });
    Test("Фильтр вида", () => { var s = Fresh(); s.Add(TransactionKind.Income, 1m, date, "A"); s.Add(TransactionKind.Expense, 2m, date, "A"); Equal(1, s.GetTransactions(new(Kind: TransactionKind.Expense)).Count); });
    Test("Фильтр категории без регистра", () => { var s = Fresh(); s.Add(TransactionKind.Income, 1m, date, "Еда"); Equal(1, s.GetTransactions(new(Category: "еда")).Count); });
    Test("Поиск по комментарию", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 1m, date, "Еда", "Кофе"); Equal(1, s.GetTransactions(new(Search: "КОФЕ")).Count); });
    Test("Поиск по категории", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 1m, date, "Продукты"); Equal(1, s.GetTransactions(new(Search: "дук")).Count); });
    Test("Сочетание фильтров", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 1m, date, "Еда", "Кофе"); Equal(0, s.GetTransactions(new(2026, 10, TransactionKind.Income, "Еда", "Кофе")).Count); });
    Test("Сортировка дат", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 1m, date, "A"); var b = s.Add(TransactionKind.Expense, 1m, date.AddDays(1), "A"); Equal(b.Id, s.GetTransactions()[0].Id); });
    Test("Установка бюджета", () => { var s = Fresh(); s.SetBudget(2026, 10, 100m); Equal((decimal?)100m, s.GetSummary(2026, 10).Budget); });
    Test("Превышение бюджета", () => { var s = Fresh(); s.SetBudget(2026, 10, 100m); s.Add(TransactionKind.Expense, 120m, date, "A"); Equal(true, s.GetSummary(2026, 10).IsOverBudget); Equal((decimal?)(-20m), s.GetSummary(2026, 10).BudgetRemaining); });
    Test("Удаление бюджета", () => { var s = Fresh(); s.SetBudget(2026, 10, 100m); s.SetBudget(2026, 10, null); Equal((decimal?)null, s.GetSummary(2026, 10).Budget); });
    Test("Бюджет ноль", () => { var s = Fresh(); s.SetBudget(2026, 10, 0m); s.Add(TransactionKind.Expense, 1m, date, "A"); Equal(true, s.GetSummary(2026, 10).IsOverBudget); });
    Test("Отрицательный бюджет", () => Throws<ArgumentException>(() => Fresh().SetBudget(2026, 10, -1m)));
    Test("Независимость месяцев", () => { var s = Fresh(); s.SetBudget(2026, 10, 100m); Equal((decimal?)null, s.GetSummary(2026, 11).Budget); });
    Test("Некорректный месяц", () => Throws<ArgumentException>(() => Fresh().GetSummary(2026, 13)));
    Test("Демо итоги", () => { var s = Fresh(); s.AddDemoData(2026, 10); Equal(8, s.GetTransactions().Count); var m = s.GetSummary(2026, 10); Equal(58000m, m.Income); Equal(24300m, m.Expenses); Equal(33700m, m.Balance); Equal((decimal?)5700m, m.BudgetRemaining); });
    Test("Демо не повторяется", () => { var s = Fresh(); s.AddDemoData(2026, 10); Throws<InvalidOperationException>(() => s.AddDemoData(2026, 10)); Equal(8, s.GetTransactions().Count); });
    Test("Демо не заменяет записи", () => { var s = Fresh(); s.Add(TransactionKind.Expense, 1m, date, "A"); Throws<InvalidOperationException>(() => s.AddDemoData(2026, 10)); Equal(1, s.GetTransactions().Count); });
    Test("JSON повторная загрузка и наследование", () => { var p = Path.Combine(dir, "roundtrip.json"); var s = new FinanceService(new JsonTransactionRepository(p)); s.AddDemoData(2026, 10); var next = new FinanceService(new JsonTransactionRepository(p)); Equal(8, next.GetTransactions().Count); Equal(true, next.GetTransactions().Any(x => x is IncomeTransaction)); Equal(true, next.GetTransactions().Any(x => x is ExpenseTransaction)); Equal((decimal?)30000m, next.GetSummary(2026, 10).Budget); });
    Test("Повреждённый JSON не стирается", () => { var p = Path.Combine(dir, "bad.json"); File.WriteAllText(p, "not json"); Throws<InvalidDataException>(() => new FinanceService(new JsonTransactionRepository(p))); Equal("not json", File.ReadAllText(p)); });
    Test("Неподдерживаемая версия", () => { var p = Path.Combine(dir, "version.json"); File.WriteAllText(p, "{\"Version\":2,\"Transactions\":[],\"MonthlyBudgets\":{}}"); Throws<InvalidDataException>(() => new FinanceService(new JsonTransactionRepository(p))); });
    Test("Резервная копия предыдущего сохранения", () =>
    {
        string path = Path.Combine(dir, "backup.json");
        var service = new FinanceService(new JsonTransactionRepository(path));
        service.Add(TransactionKind.Income, 100m, date, "A");
        service.Add(TransactionKind.Expense, 30m, date, "B");
        var backup = new FinanceService(new JsonTransactionRepository(path + ".bak"));
        Equal(1, backup.GetTransactions().Count);
        Equal(100m, backup.GetSummary(2026, 10).Income);
    });
    Test("JSON без временного файла после сохранения", () =>
    {
        string path = Path.Combine(dir, "temporary.json");
        var service = new FinanceService(new JsonTransactionRepository(path));
        service.Add(TransactionKind.Income, 1m, date, "A");
        Equal(false, File.Exists(path + ".tmp"));
    });
    Test("Откат редактирования при ошибке записи", () =>
    {
        var repository = new ToggleRepository();
        var service = new FinanceService(repository);
        var item = service.Add(TransactionKind.Income, 10m, date, "A");
        repository.FailSaving = true;
        Throws<IOException>(() => service.Update(item.Id, TransactionKind.Expense, 20m, date, "B"));
        Equal(10m, service.GetSummary(2026, 10).Income);
        Equal("A", service.GetTransactions()[0].Category);
    });
    Test("Откат удаления при ошибке записи", () =>
    {
        var repository = new ToggleRepository();
        var service = new FinanceService(repository);
        var item = service.Add(TransactionKind.Income, 10m, date, "A");
        repository.FailSaving = true;
        Throws<IOException>(() => service.Delete(item.Id));
        Equal(1, service.GetTransactions().Count);
    });
    Test("Откат бюджета при ошибке записи", () =>
    {
        var repository = new ToggleRepository();
        var service = new FinanceService(repository);
        service.SetBudget(2026, 10, 100m);
        repository.FailSaving = true;
        Throws<IOException>(() => service.SetBudget(2026, 10, 200m));
        Equal((decimal?)100m, service.GetSummary(2026, 10).Budget);
    });
    Test("Откат демо при ошибке записи", () =>
    {
        var service = new FinanceService(new FailingRepository());
        Throws<IOException>(() => service.AddDemoData(2026, 10));
        Equal(0, service.GetTransactions().Count);
        Equal((decimal?)null, service.GetSummary(2026, 10).Budget);
    });
    Test("Откат при ошибке сохранения", () => { var s = new FinanceService(new FailingRepository()); Throws<IOException>(() => s.Add(TransactionKind.Income, 1m, date, "A")); Equal(0, s.GetTransactions().Count); });
    Test("CSV кириллица, кавычки и формулы", () => { var p = Path.Combine(dir, "csv.csv"); var s = Fresh(); s.Add(TransactionKind.Expense, 12.50m, date, "=1+1", "Кофе; \"тест\"\nстрока"); CsvExporter.Export(p, s.GetTransactions()); var text = File.ReadAllText(p); Equal(true, text.Contains("' =1+1".Replace(" ", ""))); Equal(true, text.Contains("\"\"тест\"\"")); Equal(true, text.Contains("12,50")); Equal(true, File.ReadAllBytes(p).Take(3).SequenceEqual(new byte[] { 239, 187, 191 })); });
    Test("Число с запятой", () => Equal(12.50m, Validation.ParseAmount("12,50")));
    Test("Число с точкой", () => Equal(12.50m, Validation.ParseAmount("12.50")));
    Test("Некорректное число", () => Throws<ArgumentException>(() => Validation.ParseAmount("abc")));
    Test("Некорректный вид операции", () => Throws<ArgumentException>(() => Fresh().Add((TransactionKind)999, 1m, date, "A")));
    Test("Разные ID", () => { var s = Fresh(); var a = s.Add(TransactionKind.Income, 1m, date, "A"); var b = s.Add(TransactionKind.Income, 1m, date, "A"); Equal(false, a.Id == b.Id); });
    Test("События бюджет/изменение/удаление/обновление", () => { var s = Fresh(); var a = s.Add(TransactionKind.Income, 1m, date, "A"); var n = 0; s.DataChanged += (_, _) => n++; s.SetBudget(2026, 10, 1m); s.Update(a.Id, TransactionKind.Income, 2m, date, "A"); s.Delete(a.Id); s.Reload(); Equal(4, n); });
}
finally { Directory.Delete(dir, recursive: true); }
Console.WriteLine($"\nИтого: {passed} пройдено, {failed} ошибок.");
return failed == 0 ? 0 : 1;

sealed class MemoryRepository : ITransactionRepository
{
    private FinanceData _data = new();
    private static FinanceData Copy(FinanceData d) => new() { Version = d.Version, Transactions = [.. d.Transactions], MonthlyBudgets = new(d.MonthlyBudgets) };
    public FinanceData Load() => Copy(_data);
    public void Save(FinanceData data) => _data = Copy(data);
}
sealed class FailingRepository : ITransactionRepository
{
    public FinanceData Load() => new();
    public void Save(FinanceData data) => throw new IOException("Тестовая ошибка записи.");
}

sealed class ToggleRepository : ITransactionRepository
{
    private readonly MemoryRepository _storage = new MemoryRepository();
    public bool FailSaving
    {
        get; set;
    }
    public FinanceData Load()
    {
        return _storage.Load();
    }
    public void Save(FinanceData data)
    {
        if (FailSaving)
            throw new IOException("Тестовая ошибка записи.");
        _storage.Save(data);
    }
}
