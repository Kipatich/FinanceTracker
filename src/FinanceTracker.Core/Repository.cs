using System.Text.Json;

namespace FinanceTracker.Core;

// Интерфейс позволяет заменить JSON другим хранилищем, не меняя логику.
public interface ITransactionRepository
{
    FinanceData Load();
    void Save(FinanceData data);
}

public class JsonTransactionRepository : ITransactionRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options;

    public JsonTransactionRepository(string path)
    {
        _filePath = Path.GetFullPath(path);
        _options = new JsonSerializerOptions { WriteIndented = true };
    }

    public FinanceData Load()
    {
        // Первый запуск: файла ещё нет, поэтому возвращаем пустые данные.
        if (!File.Exists(_filePath))
        {
            return new FinanceData();
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            FinanceData? data = JsonSerializer.Deserialize<FinanceData>(json, _options);
            if (data == null)
            {
                throw new InvalidDataException("Файл данных пуст.");
            }
            Validation.Data(data);
            return data;
        }
        catch (Exception error) when (error is JsonException || error is ArgumentException)
        {
            throw new InvalidDataException(
                "Файл данных повреждён. Сохраните его копию перед восстановлением.", error);
        }
    }

    public void Save(FinanceData data)
    {
        Validation.Data(data);
        string? folder = Path.GetDirectoryName(_filePath);
        if (folder != null)
        {
            Directory.CreateDirectory(folder);
        }

        // Сначала готовим новый файл. Старый остаётся на месте до конца записи.
        string json = JsonSerializer.Serialize(data, _options);
        string temporaryFile = _filePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryFile, json);
            if (File.Exists(_filePath))
            {
                // .bak содержит предыдущую сохранённую версию, не всю историю.
                File.Copy(_filePath, _filePath + ".bak", overwrite: true);
            }
            File.Move(temporaryFile, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}
