using System.Windows;
using FinanceTracker.Core;

namespace FinanceTracker.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var path = FinancePaths.DataFile;
            if (e.Args.Length != 0)
            {
                if (e.Args.Length != 2 || e.Args[0] != "--data")
                    throw new ArgumentException("WPF: FinanceTracker.Desktop.exe [--data \"путь к finance.json\"]. Основные консольные операции — в FinanceTracker.Cli.exe.");
                path = e.Args[1];
            }
            var settings = UserSettings.Load();
            Theme.Apply(settings.DarkTheme);
            var service = new FinanceService(new JsonTransactionRepository(path));
            var window = new MainWindow(service, settings, System.IO.Path.GetFullPath(path));
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть приложение.\n\n{ex.Message}\n\nФайл данных не будет перезаписан.",
                "FinanceTracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
