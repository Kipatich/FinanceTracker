namespace FinanceTracker.Core;

// Правила приложения находятся здесь, а не в обработчиках кнопок WPF.
public class FinanceService
{
    private readonly ITransactionRepository _repository;
    private FinanceData _data;

    // Окно подписывается на событие и обновляет таблицу после изменения.
    public event EventHandler? DataChanged;

    public FinanceService(ITransactionRepository repository)
    {
        _repository = repository;
        _data = repository.Load();
        Validation.Data(_data);
    }

    private void NotifyDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        FinanceData loadedData = _repository.Load();
        Validation.Data(loadedData);
        _data = loadedData;
        NotifyDataChanged();
    }

    private FinancialTransaction CreateTransaction(TransactionKind kind, decimal amount,
        DateTime date, string category, string? note, Guid? id = null)
    {
        Guid transactionId = id ?? Guid.NewGuid();
        string cleanCategory = (category ?? "").Trim();
        string cleanNote = (note ?? "").Trim();
        FinancialTransaction transaction;

        if (kind == TransactionKind.Income)
        {
            transaction = new IncomeTransaction
            {
                Id = transactionId,
                Amount = amount,
                Date = date.Date,
                Category = cleanCategory,
                Note = cleanNote
            };
        }
        else if (kind == TransactionKind.Expense)
        {
            transaction = new ExpenseTransaction
            {
                Id = transactionId,
                Amount = amount,
                Date = date.Date,
                Category = cleanCategory,
                Note = cleanNote
            };
        }
        else
        {
            throw new ArgumentException("Неизвестный вид операции.");
        }
        Validation.Transaction(transaction);
        return transaction;
    }

    public FinancialTransaction Add(TransactionKind kind, decimal amount,
        DateTime date, string category, string? note = null)
    {
        FinancialTransaction transaction = CreateTransaction(kind, amount, date, category, note);
        _data.Transactions.Add(transaction);
        try
        {
            _repository.Save(_data);
        }
        catch
        {
            // Если запись не удалась, убираем несохранённую операцию из списка.
            _data.Transactions.Remove(transaction);
            throw;
        }
        NotifyDataChanged();
        return transaction;
    }

    private int FindTransactionIndex(Guid id)
    {
        for (int index = 0; index < _data.Transactions.Count; index++)
        {
            if (_data.Transactions[index].Id == id)
            {
                return index;
            }
        }
        throw new KeyNotFoundException("Операция не найдена.");
    }

    public void Update(Guid id, TransactionKind kind, decimal amount,
        DateTime date, string category, string? note = null)
    {
        int index = FindTransactionIndex(id);
        FinancialTransaction previous = _data.Transactions[index];
        _data.Transactions[index] = CreateTransaction(kind, amount, date, category, note, id);
        try
        {
            _repository.Save(_data);
        }
        catch
        {
            _data.Transactions[index] = previous;
            throw;
        }
        NotifyDataChanged();
    }

    public void Delete(Guid id)
    {
        int index = FindTransactionIndex(id);
        FinancialTransaction previous = _data.Transactions[index];
        _data.Transactions.RemoveAt(index);
        try
        {
            _repository.Save(_data);
        }
        catch
        {
            _data.Transactions.Insert(index, previous);
            throw;
        }
        NotifyDataChanged();
    }

    public IReadOnlyList<FinancialTransaction> GetTransactions(TransactionFilter? filter = null)
    {
        if (filter == null)
        {
            filter = new TransactionFilter();
        }
        List<FinancialTransaction> result = new List<FinancialTransaction>();
        foreach (FinancialTransaction transaction in _data.Transactions)
        {
            if (filter.Year.HasValue && transaction.Date.Year != filter.Year.Value)
                continue;
            if (filter.Month.HasValue && transaction.Date.Month != filter.Month.Value)
                continue;
            if (filter.Kind.HasValue && transaction.Kind != filter.Kind.Value)
                continue;
            if (!string.IsNullOrWhiteSpace(filter.Category) &&
                !transaction.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string text = filter.Search.Trim();
                bool foundInCategory = transaction.Category.Contains(text, StringComparison.OrdinalIgnoreCase);
                bool foundInNote = transaction.Note.Contains(text, StringComparison.OrdinalIgnoreCase);
                if (!foundInCategory && !foundInNote)
                    continue;
            }
            result.Add(transaction);
        }
        // Свежие операции первыми; ID задаёт устойчивый порядок одинаковых дат.
        return result.OrderByDescending(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Id).ToList();
    }

    public IReadOnlyList<string> GetCategories()
    {
        List<string> categories = new List<string>();
        foreach (FinancialTransaction transaction in _data.Transactions)
        {
            if (!categories.Contains(transaction.Category, StringComparer.OrdinalIgnoreCase))
            {
                categories.Add(transaction.Category);
            }
        }
        categories.Sort(StringComparer.CurrentCultureIgnoreCase);
        return categories;
    }

    public MonthlySummary GetSummary(int year, int month)
    {
        string monthKey = Validation.MonthKey(year, month);
        decimal income = 0;
        decimal expenses = 0;
        foreach (FinancialTransaction transaction in _data.Transactions)
        {
            if (transaction.Date.Year != year || transaction.Date.Month != month)
                continue;
            if (transaction.Kind == TransactionKind.Income)
                income += transaction.Amount;
            else
                expenses += transaction.Amount;
        }
        decimal? budget = null;
        if (_data.MonthlyBudgets.ContainsKey(monthKey))
        {
            budget = _data.MonthlyBudgets[monthKey];
        }
        return new MonthlySummary(income, expenses, budget);
    }

    public void SetBudget(int year, int month, decimal? amount)
    {
        string monthKey = Validation.MonthKey(year, month);
        if (amount.HasValue)
            Validation.Amount(amount.Value, allowZero: true);

        bool hadBudget = _data.MonthlyBudgets.TryGetValue(monthKey, out decimal previous);
        if (amount.HasValue)
            _data.MonthlyBudgets[monthKey] = amount.Value;
        else
            _data.MonthlyBudgets.Remove(monthKey);

        try
        {
            _repository.Save(_data);
        }
        catch
        {
            if (hadBudget)
                _data.MonthlyBudgets[monthKey] = previous;
            else
                _data.MonthlyBudgets.Remove(monthKey);
            throw;
        }
        NotifyDataChanged();
    }

    public void AddDemoData(int year, int month)
    {
        string monthKey = Validation.MonthKey(year, month);
        if (_data.Transactions.Count != 0 || _data.MonthlyBudgets.Count != 0)
        {
            throw new InvalidOperationException("Демо можно добавить только в пустой файл данных.");
        }
        _data.Transactions.Add(CreateTransaction(TransactionKind.Income, 50000m, new DateTime(year, month, 1), "Зарплата", "Подработка"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Income, 8000m, new DateTime(year, month, 5), "Стипендия", "За текущий месяц"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 4500m, new DateTime(year, month, 2), "Продукты", "Покупки на неделю"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 2400m, new DateTime(year, month, 3), "Транспорт", "Проездной"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 12000m, new DateTime(year, month, 4), "Жильё", "Аренда"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 1700m, new DateTime(year, month, 6), "Развлечения", "Кино с друзьями"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 2800m, new DateTime(year, month, 7), "Продукты", "Супермаркет"));
        _data.Transactions.Add(CreateTransaction(TransactionKind.Expense, 900m, new DateTime(year, month, 8), "Связь", "Интернет и телефон"));
        _data.MonthlyBudgets[monthKey] = 30000m;
        try
        {
            _repository.Save(_data);
        }
        catch
        {
            _data.Transactions.Clear();
            _data.MonthlyBudgets.Clear();
            throw;
        }
        NotifyDataChanged();
    }
}
