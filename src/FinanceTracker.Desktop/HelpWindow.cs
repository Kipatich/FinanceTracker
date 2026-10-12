using System.Windows;
using System.Windows.Controls;

namespace FinanceTracker.Desktop;

public sealed class HelpWindow : Window
{
    public HelpWindow()
    {
        Title = "Справка FinanceTracker";
        Width = 740;
        Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Text = """
FINANCETRACKER — СПРАВКА

Операции
Добавьте доход или расход, укажите положительную сумму (до 2 знаков после запятой), дату и категорию. Категорию можно ввести самостоятельно. Данные сохраняются автоматически после каждого успешного изменения. Для редактирования выберите строку; удаление требует подтверждения.

Месяц и фильтры
Стрелки переключают месяцы. Чтобы ввести месяц вручную, используйте yyyy-MM и Enter. Поиск работает по категории и комментарию. Сводка и бюджет всегда относятся ко всему выбранному месяцу, а CSV — только к видимым операциям. Остаток — доходы минус расходы за месяц, НЕ общий баланс за всё время.

Бюджет
Введите лимит и нажмите «Сохранить». Пустое поле убирает лимит; ноль означает, что расходы не планируются. При превышении приложение показывает предупреждение.

Настройки и демо
Светлая/тёмная тема сохраняется между запусками. Демо добавляет 8 записей и бюджет 30 000 ₽ в выбранный месяц, только если файл полностью пуст. Реальные данные никогда не заменяются демо.

Горячие клавиши
Ctrl+N — добавить; Ctrl+E — редактировать.
Delete в таблице — удалить; Ctrl+Shift+E — экспорт CSV.
Ctrl+R — перечитать файл; F1 — справка.
Двойной щелчок по строке — редактировать; Esc — отмена в форме операции.

Командная строка
FinanceTracker.Desktop.exe --data "demo.json"
FinanceTracker.Cli.exe help
FinanceTracker.Cli.exe demo --month 2026-10 --data "demo.json"
FinanceTracker.Cli.exe list --month 2026-10 --data "demo.json"
FinanceTracker.Cli.exe add --type expense --amount 350 --category "Продукты" --date 2026-10-12 --data "demo.json"
FinanceTracker.Cli.exe summary --month 2026-10 --data "demo.json"
FinanceTracker.Cli.exe budget --month 2026-10 --amount 20000 --data "demo.json"
FinanceTracker.Cli.exe export --file "report.csv" --month 2026-10 --data "demo.json"
Для edit и delete нужен полный GUID из команды list. Все консольные команды работают без запуска окна.

Хранение и безопасность
По умолчанию: %LOCALAPPDATA%\FinanceTracker\finance.json.
Настройки: settings.json в той же папке. Перед ручными изменениями скопируйте файл данных. Он не зашифрован: не храните банковские реквизиты. Повреждённый файл не перезаписывается автоматически. Если другой экземпляр изменил данные, нажмите Ctrl+R. Подробные команды — в CLI.md.
"""
            }
        };
    }
}
