using System.Text;
using System.Text.Json;

namespace Sigwalk.Core.Services.Export;

/// <summary>导出 JSON（schema v2）：概况 + 分组明细。</summary>
public static class ExportJsonWriter
{
    public static void Write(string filePath, ExportDocument document)
    {
        var json = JsonSerializer.Serialize(document, ResultSerializer.JsonOptions);
        File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
