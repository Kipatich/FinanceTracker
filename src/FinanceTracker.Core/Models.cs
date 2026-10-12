using System.Text.Json.Serialization;

namespace FinanceTracker.Core;

public enum TransactionKind
{
    Income,
    Expense
}

// Эти атрибуты помогают JSON различать доходы и расходы при загрузке.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(IncomeTransaction), "income")]
[JsonDerivedType(typeof(ExpenseTransaction), "expense")]
public abstract class FinancialTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Date { get; init; } = DateTime.Today;
    public decimal Amount
    {
        get; init;
    }
    public string Category { get; init; } = "";
    public string Note { get; init; } = "";

    // init: поля задаём при создании. При редактировании заменяем весь объект.
    // Вычисляемые свойства не нужно записывать в JSON.
    [JsonIgnore]
    public abstract TransactionKind Kind
    {
        get;
    }

    [JsonIgnore]
    public string TypeLabel
    {
        get
        {
            if (Kind == TransactionKind.Income)
            {
                return "Доход";
            }
            return "Расход";
        }
    }

    [JsonIgnore]
    public string ShortId
    {
        get
        {
            return Id.ToString("N").Substring(0, 8);
        }
    }
}

public class IncomeTransaction : FinancialTransaction
{
    public override TransactionKind Kind
    {
        get
        {
            return TransactionKind.Income;
        }
    }
}

public class ExpenseTransaction : FinancialTransaction
{
    public override TransactionKind Kind
    {
        get
        {
            return TransactionKind.Expense;
        }
    }
}

// Всё, что сохраняем в finance.json: операции и лимиты по месяцам.
public class FinanceData
{
    public int Version { get; set; } = 1;
    public List<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
    public Dictionary<string, decimal> MonthlyBudgets { get; set; } = new Dictionary<string, decimal>();
}

// Пустое поле фильтра означает «не ограничивать по этому признаку».
public class TransactionFilter
{
    public int? Year
    {
        get;
    }
    public int? Month
    {
        get;
    }
    public TransactionKind? Kind
    {
        get;
    }
    public string? Category
    {
        get;
    }
    public string? Search
    {
        get;
    }

    public TransactionFilter(int? Year = null, int? Month = null,
        TransactionKind? Kind = null, string? Category = null, string? Search = null)
    {
        this.Year = Year;
        this.Month = Month;
        this.Kind = Kind;
        this.Category = Category;
        this.Search = Search;
    }
}

public class MonthlySummary
{
    public decimal Income
    {
        get;
    }
    public decimal Expenses
    {
        get;
    }
    public decimal? Budget
    {
        get;
    }

    public MonthlySummary(decimal income, decimal expenses, decimal? budget)
    {
        Income = income;
        Expenses = expenses;
        Budget = budget;
    }

    public decimal Balance
    {
        get
        {
            return Income - Expenses;
        }
    }

    public decimal? BudgetRemaining
    {
        get
        {
            return Budget - Expenses;
        }
    }

    public bool IsOverBudget
    {
        get
        {
            return Budget.HasValue && Expenses > Budget.Value;
        }
    }

    public double BudgetPercent
    {
        get
        {
            if (!Budget.HasValue || Budget.Value <= 0)
            {
                return 0;
            }
            return (double)Math.Min(100m, Expenses / Budget.Value * 100m);
        }
    }
}

public static class FinancePaths
{
    public static string DirectoryPath
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "FinanceTracker");
        }
    }
    public static string DataFile
    {
        get
        {
            return Path.Combine(DirectoryPath, "finance.json");
        }
    }
    public static string SettingsFile
    {
        get
        {
            return Path.Combine(DirectoryPath, "settings.json");
        }
    }
}
