using System.Globalization;
using System.Windows;
using FinanceTracker.Core;

namespace FinanceTracker.Desktop;

public partial class TransactionWindow : Window
{
    private readonly FinanceService _service;
    private readonly FinancialTransaction? _original;

    public TransactionWindow(FinanceService service, FinancialTransaction? original, DateTime date)
    {
        _service = service;
        _original = original;
        InitializeComponent();
        Language = System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU");
        Heading.Text = Title = original == null ? "Новая операция" : "Изменить операцию";
        KindInput.SelectedIndex = original?.Kind == TransactionKind.Income ? 1 : 0;
        DateInput.SelectedDate = date;
        CategoryInput.ItemsSource = new[] { "Продукты", "Транспорт", "Жильё", "Развлечения", "Связь", "Здоровье", "Учёба", "Зарплата", "Стипендия", "Другое" }
            .Concat(service.GetCategories()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        CategoryInput.Text = original?.Category ?? "Продукты";
        AmountInput.Text = original?.Amount.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU")) ?? "";
        NoteInput.Text = original?.Note ?? "";
        Loaded += (_, _) => { AmountInput.Focus(); AmountInput.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!DateTime.TryParse(DateInput.Text, CultureInfo.GetCultureInfo("ru-RU"),
                    DateTimeStyles.None, out var date))
                throw new ArgumentException("Выберите корректную дату.");
            var amount = Validation.ParseAmount(AmountInput.Text);
            var kind = KindInput.SelectedIndex == 1 ? TransactionKind.Income : TransactionKind.Expense;
            // Новая запись и редактирование используют одни и те же правила Core.
            if (_original == null)
            {
                _service.Add(kind, amount, date, CategoryInput.Text, NoteInput.Text);
            }
            else
            {
                _service.Update(_original.Id, kind, amount, date, CategoryInput.Text, NoteInput.Text);
            }
            DialogResult = true;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
