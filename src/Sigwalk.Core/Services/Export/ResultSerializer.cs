using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>扫描结果的 JSON 文档格式，同时用于导出、保存和加载。</summary>
public sealed class ResultDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ToolVersion { get; set; } = "1.0.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public ScanStatistics Summary { get; set; } = new();
    public List<FileScanResult> Items { get; set; } = new();
}

/// <summary>扫描结果 JSON 序列化 / 反序列化。</summary>
public sealed class ResultSerializer
{
    private const int CurrentSchemaVersion = 1;

    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    public void Save(string filePath, IEnumerable<FileScanResult> results)
    {
        var items = results.ToList();
        var document = new ResultDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            ToolVersion = typeof(ResultSerializer).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ExportedAt = DateTime.UtcNow,
            Summary = ScanStatistics.From(items),
            Items = items,
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public ResultDocument Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        ResultDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ResultDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"扫描结果文件格式无效：{ex.Message}", ex);
        }

        if (document is null)
        {
            throw new InvalidDataException("扫描结果文件为空或格式无效。");
        }

        if (document.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"扫描结果文件版本 {document.SchemaVersion} 高于当前支持的版本 {CurrentSchemaVersion}。");
        }

        document.Items ??= new List<FileScanResult>();
        return document;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
