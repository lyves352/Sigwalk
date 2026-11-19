using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services;

/// <summary>用户配置读写。配置损坏时回退默认值。</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly object _sync = new();

    public SettingsService(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Sigwalk")
            : directory;
    }

    public string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppSettings Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                // 兼容手工编辑/其他工具写入的 UTF-8 BOM。
                var json = File.ReadAllText(SettingsPath).TrimStart((char)0xFEFF);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is null)
                {
                    return new AppSettings();
                }

                settings.HiddenColumns ??= new List<string>();
                return settings;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
    }
}
