using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using FinanceTracker.Core;

namespace FinanceTracker.Desktop;

public sealed class UserSettings
{
    public bool DarkTheme
    {
        get; set;
    }
    public static UserSettings Load()
    {
        try
        {
            return File.Exists(FinancePaths.SettingsFile)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FinancePaths.SettingsFile)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Не удалось прочитать настройки темы. Используется светлая тема. Данные операций не затронуты.", "Настройки");
            return new();
        }
    }
    public void Save()
    {
        Directory.CreateDirectory(FinancePaths.DirectoryPath);
        var temp = FinancePaths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FinancePaths.SettingsFile, overwrite: true);
    }
}

public static class Theme
{
    public static void Apply(bool dark)
    {
        string[] keys = ["WindowBrush", "PanelBrush", "TextBrush", "MutedBrush", "BorderBrush", "RowAltBrush", "IncomeBrush", "ExpenseBrush"];
        string[] light = ["#F3F5F9", "#FFFFFF", "#18243A", "#64748B", "#CFD8E5", "#F7F9FC", "#15803D", "#C24135"];
        string[] night = ["#111827", "#1F2937", "#F1F5F9", "#A5B4CB", "#475569", "#273449", "#4ADE80", "#FB8B7D"];
        var colors = dark ? night : light;
        for (var i = 0; i < keys.Length; i++)
            Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
