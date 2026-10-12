using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FinanceTracker.Core;
using Validation = FinanceTracker.Core.Validation;
using Microsoft.Win32;

namespace FinanceTracker.Desktop;

public partial class MainWindow : Window
{
    private readonly FinanceService _service;
    private readonly UserSettings _settings;
    private bool _ready;
    private bool _refreshing;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public MainWindow(FinanceService service, UserSettings settings, string path)
    {
        _service = service;
        _settings = settings;
        InitializeComponent();
        Language = System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU");
        MonthInput.Text = _month.ToString("yyyy-MM");
        DataPathText.Text = "Данные: " + path;
        DataPathText.ToolTip = path;
        ThemeButton.Content = settings.DarkTheme ? "Светлая тема" : "Тёмная тема";
        _service.DataChanged += Service_DataChanged;
        _ready = true;
        RefreshAll();
    }

    private void Service_DataChanged(object? sender, EventArgs e)
    {
        RefreshAll();
    }

    private string Money(decimal value) => value.ToString("N2", Ru) + " ₽";
    // Собираем значения из полей окна в один объект фильтра.
    private TransactionFilter CurrentFilter()
    {
        TransactionKind? kind = null;
        if (KindFilter.SelectedIndex == 1)
            kind = TransactionKind.Income;
        else if (KindFilter.SelectedIndex == 2)
            kind = TransactionKind.Expense;

        string? category = null;
        if (CategoryFilter.SelectedIndex > 0)
            category = CategoryFilter.SelectedItem as string;

        return new TransactionFilter(_month.Year, _month.Month, kind, category, SearchInput.Text);
    }

    // Сводка всегда за весь месяц, даже когда таблица отфильтрована.
    private void RefreshAll()
    {
        _refreshing = true;
        try
        {
            var category = CategoryFilter.SelectedItem as string;
            CategoryFilter.ItemsSource = new[] { "Все категории" }.Concat(_service.GetCategories()).ToArray();
            CategoryFilter.SelectedItem = category;
            if (CategoryFilter.SelectedIndex < 0)
                CategoryFilter.SelectedIndex = 0;
            var summary = _service.GetSummary(_month.Year, _month.Month);
            IncomeText.Text = Money(summary.Income);
            ExpenseText.Text = Money(summary.Expenses);
            BalanceText.Text = Money(summary.Balance);
            BudgetInput.Text = summary.Budget?.ToString("0.00", Ru) ?? "";
            if (!summary.Budget.HasValue)
            {
                BudgetStatus.Text = "Лимит расходов не задан";
            }
            else if (summary.IsOverBudget)
            {
                BudgetStatus.Text = "Бюджет превышен на " + Money(-summary.BudgetRemaining!.Value);
            }
            else
            {
                BudgetStatus.Text = "Осталось бюджета: " + Money(summary.BudgetRemaining!.Value);
            }
            BudgetStatus.SetResourceReference(TextBlock.ForegroundProperty, summary.IsOverBudget ? "ExpenseBrush" : "MutedBrush");
            BudgetProgress.Value = summary.Budget == 0 && summary.Expenses > 0 ? 100 : summary.BudgetPercent;
            RefreshList();
        }
        finally { _refreshing = false; }
    }

    private void RefreshList()
    {
        var selectedId = (TransactionsGrid.SelectedItem as FinancialTransaction)?.Id;
        var items = _service.GetTransactions(CurrentFilter());
        TransactionsGrid.ItemsSource = items;
        if (selectedId.HasValue)
            TransactionsGrid.SelectedItem = items.FirstOrDefault(x => x.Id == selectedId);
        EmptyMessage.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyMessage.Text = _service.GetTransactions().Count == 0
            ? "Операций пока нет. Добавьте запись или загрузите демо-данные."
            : "За этот месяц и с этими фильтрами операций нет.";
        StatusText.Text = $"Показано: {items.Count} · Месяц: {_month:yyyy-MM} · Сводка и бюджет — за весь месяц, независимо от фильтров";
    }

    // Единая обработка ошибок действий окна: показать сообщение, не закрывать приложение.
    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось выполнить действие", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private bool ApplyMonth()
    {
        try
        {
            var next = Validation.ParseMonth(MonthInput.Text.Trim());
            var changed = next != _month;
            _month = next;
            MonthInput.Text = _month.ToString("yyyy-MM");
            if (changed)
                RefreshAll();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Месяц", MessageBoxButton.OK, MessageBoxImage.Warning);
            MonthInput.Focus();
            return false;
        }
    }
    private void ChangeMonth(int delta) => Safe(() =>
    {
        if (!ApplyMonth())
            return;
        _month = _month.AddMonths(delta);
        MonthInput.Text = _month.ToString("yyyy-MM");
        RefreshAll();
    });
    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(1);
    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_refreshing)
            RefreshList();
    }
    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_ready && !_refreshing)
            RefreshList();
    }
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        KindFilter.SelectedIndex = 0;
        CategoryFilter.SelectedIndex = 0;
        SearchInput.Clear();
        RefreshList();
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyMonth())
            return;
        var defaultDate = _month.Year == DateTime.Today.Year && _month.Month == DateTime.Today.Month ? DateTime.Today : _month;
        new TransactionWindow(_service, null, defaultDate) { Owner = this }.ShowDialog();
    }
    private FinancialTransaction? Selected()
    {
        if (TransactionsGrid.SelectedItem is FinancialTransaction item)
            return item;
        MessageBox.Show(this, "Выберите операцию в таблице.", "FinanceTracker");
        return null;
    }
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected();
        if (item != null)
            new TransactionWindow(_service, item, item.Date) { Owner = this }.ShowDialog();
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected();
        if (item != null && MessageBox.Show(this, $"Удалить операцию {item.TypeLabel.ToLower()} на {Money(item.Amount)}?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            Safe(() => _service.Delete(item.Id));
    }
    private void Grid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject element &&
            ItemsControl.ContainerFromElement(TransactionsGrid, element) is DataGridRow)
            Edit_Click(sender, e);
    }
    private void Budget_Click(object sender, RoutedEventArgs e)
    {
        // Считываем до обновления месяца, чтобы не потерять введённую сумму.
        var text = BudgetInput.Text.Trim();
        if (ApplyMonth())
            Safe(() => _service.SetBudget(_month.Year, _month.Month,
            text.Length == 0 ? null : Validation.ParseAmount(text)));
    }
    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyMonth())
            return;
        if (MessageBox.Show(this, "Добавить учебные записи и бюджет в выбранный месяц? Только для пустого файла.",
            "Демо-данные", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            Safe(() => _service.AddDemoData(_month.Year, _month.Month));
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyMonth())
            return;
        var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"FinanceTracker-{_month:yyyy-MM}.csv", AddExtension = true, DefaultExt = ".csv" };
        if (dialog.ShowDialog(this) == true)
            Safe(() =>
        {
            CsvExporter.Export(dialog.FileName, _service.GetTransactions(CurrentFilter()));
            MessageBox.Show(this, "Видимый список операций экспортирован в CSV.", "Экспорт");
        });
    }
    private void Reload_Click(object sender, RoutedEventArgs e) => Safe(() => _service.Reload());
    private void Theme_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var old = _settings.DarkTheme;
        _settings.DarkTheme = !old;
        try
        {
            _settings.Save();
        }
        catch { _settings.DarkTheme = old; throw; }
        Theme.Apply(_settings.DarkTheme);
        ThemeButton.Content = _settings.DarkTheme ? "Светлая тема" : "Тёмная тема";
    });
    private void Help_Click(object sender, RoutedEventArgs e) => new HelpWindow { Owner = this }.ShowDialog();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            Help_Click(sender, e);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter && MonthInput.IsKeyboardFocusWithin)
        {
            ApplyMonth();
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    Add_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.E:
                    Edit_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.R:
                    Reload_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.E)
        {
            Export_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Delete && TransactionsGrid.IsKeyboardFocusWithin)
        {
            Delete_Click(sender, e);
            e.Handled = true;
        }
    }
}
