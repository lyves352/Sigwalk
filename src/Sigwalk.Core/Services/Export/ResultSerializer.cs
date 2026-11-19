using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>扫描结果的 JSON 文档格式，用于保存和加载（schema v1）。</summary>
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
    private const int MaxSupportedSchemaVersion = 2;

    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    /// <summary>保存工作快照：继续使用 v1 扁平格式，兼容旧版本读取。</summary>
    public void Save(string filePath, IEnumerable<FileScanResult> results)
    {
        var items = results.ToList();
        var document = new ResultDocument
        {
            SchemaVersion = 1,
            ToolVersion = typeof(ResultSerializer).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ExportedAt = DateTime.UtcNow,
            Summary = ScanStatistics.From(items),
            Items = items,
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>加载保存文件（v1）或导出的分组文件（v2）。</summary>
    public ResultDocument Load(string filePath)
    {
        // 兼容手工编辑或第三方工具写入的 UTF-8 BOM。
        var json = File.ReadAllText(filePath).TrimStart((char)0xFEFF);
        try
        {
            var version = ReadSchemaVersion(json);
            if (version > MaxSupportedSchemaVersion)
            {
                throw new InvalidDataException(
                    Loc.F("Error.ResultVersion", version, MaxSupportedSchemaVersion));
            }

            if (version >= 2)
            {
                return LoadExportDocument(json);
            }

            var document = JsonSerializer.Deserialize<ResultDocument>(json, JsonOptions);
            if (document is null)
            {
                throw new InvalidDataException(Loc.T("Error.ResultEmpty"));
            }

            document.Items ??= new List<FileScanResult>();
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(Loc.F("Error.ResultFormatInvalid", ex.Message), ex);
        }
    }

    private static ResultDocument LoadExportDocument(string json)
    {
        var export = JsonSerializer.Deserialize<ExportDocument>(json, JsonOptions);
        if (export is null)
        {
            throw new InvalidDataException(Loc.T("Error.ResultEmpty"));
        }

        var items = (export.Groups ?? new List<ExportGroup>())
            .SelectMany(group => group.Files ?? new List<FileScanResult>())
            .ToList();

        return new ResultDocument
        {
            SchemaVersion = export.SchemaVersion,
            ToolVersion = export.ToolVersion,
            ExportedAt = export.ExportedAt,
            Summary = ScanStatistics.From(items),
            Items = items,
        };
    }

    private static int ReadSchemaVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(Loc.T("Error.ResultEmpty"));
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("SchemaVersion", StringComparison.OrdinalIgnoreCase) &&
                property.Value.TryGetInt32(out var version))
            {
                return version;
            }
        }

        return 1;
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
