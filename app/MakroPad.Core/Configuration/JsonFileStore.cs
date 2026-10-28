using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakroPad.Core.Configuration;

public sealed record LoadResult<T>(T Value, string? Error);

/// <summary>Reads and atomically writes one JSON document.</summary>
public sealed class JsonFileStore<T> where T : class
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Func<T> createDefault;

    public JsonFileStore(string path, Func<T> createDefault)
    {
        Path = path;
        this.createDefault = createDefault;
    }

    public string Path { get; }

    /// <summary>
    /// Loads the file. A missing file is created with defaults. A broken file is left untouched and
    /// the defaults are returned together with the error, so a typo never wipes the user's work.
    /// </summary>
    public LoadResult<T> Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                var created = createDefault();
                Save(created);
                return new LoadResult<T>(created, null);
            }

            var json = File.ReadAllText(Path);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return value is null
                ? new LoadResult<T>(createDefault(), "Datei ist leer")
                : new LoadResult<T>(value, null);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" in Zeile {line + 1}, Zeichen {(ex.BytePositionInLine ?? 0) + 1}" : "";
            var path = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "" : $" (bei {ex.Path})";
            return new LoadResult<T>(createDefault(), $"Fehler im JSON{where}{path}. Häufige Ursachen: fehlendes Komma, fehlendes Anführungszeichen oder ein einzelner \\ in einem Pfad (schreibe \\\\ oder /).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LoadResult<T>(createDefault(), ex.Message);
        }
    }

    public void Save(T value)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tempPath, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
