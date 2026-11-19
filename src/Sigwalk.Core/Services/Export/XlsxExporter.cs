using System.IO.Compression;
using System.Text;
using System.Xml;
using Sigwalk.Core.Models;

namespace Sigwalk.Core.Services.Export;

/// <summary>
/// 生成最小可用的 XLSX（Office Open XML）。
/// 不依赖第三方库：全量使用 inlineStr，写入 Zip 包内的必要部件。
/// </summary>
public sealed class XlsxExporter
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    public void Write(string filePath, IReadOnlyList<FileScanResult> results)
    {
        // Excel 单工作表最多 1,048,576 行（含表头），超出时明确报错而不是生成损坏文件。
        if (results.Count > 1_048_575)
        {
            throw new InvalidOperationException(
                $"结果数量 {results.Count} 超过 Excel 单工作表上限 1,048,575 行，请改用 CSV 或 JSON 导出。");
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteContentTypes(archive);
        WriteRootRelationships(archive);
        WriteWorkbook(archive);
        WriteWorkbookRelationships(archive);
        WriteStyles(archive);
        WriteWorksheet(archive, results);
    }

    private static void WriteContentTypes(ZipArchive archive)
    {
        WriteXml(archive, "[Content_Types].xml", writer =>
        {
            writer.WriteStartElement("Types", ContentTypesNs);
            WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteDefault(writer, "xml", "application/xml");
            WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            WriteOverride(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            writer.WriteEndElement();
        });
    }

    private static void WriteDefault(XmlWriter writer, string extension, string contentType)
    {
        writer.WriteStartElement("Default", ContentTypesNs);
        writer.WriteAttributeString("Extension", extension);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteOverride(XmlWriter writer, string partName, string contentType)
    {
        writer.WriteStartElement("Override", ContentTypesNs);
        writer.WriteAttributeString("PartName", partName);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRootRelationships(ZipArchive archive)
    {
        WriteXml(archive, "_rels/.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelNs);
            WriteRelationship(
                writer,
                "rId1",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
                "xl/workbook.xml");
            writer.WriteEndElement();
        });
    }

    private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship", PackageRelNs);
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static void WriteWorkbook(ZipArchive archive)
    {
        WriteXml(archive, "xl/workbook.xml", writer =>
        {
            writer.WriteStartElement("workbook", MainNs);
            writer.WriteAttributeString("xmlns", "r", null, RelNs);
            writer.WriteStartElement("sheets", MainNs);
            writer.WriteStartElement("sheet", MainNs);
            writer.WriteAttributeString("name", "扫描结果");
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("id", RelNs, "rId1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        });
    }

    private static void WriteWorkbookRelationships(ZipArchive archive)
    {
        WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelNs);
            WriteRelationship(
                writer,
                "rId1",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet",
                "worksheets/sheet1.xml");
            WriteRelationship(
                writer,
                "rId2",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles",
                "styles.xml");
            writer.WriteEndElement();
        });
    }

    private static void WriteStyles(ZipArchive archive)
    {
        WriteXml(archive, "xl/styles.xml", writer =>
        {
            writer.WriteStartElement("styleSheet", MainNs);

            writer.WriteStartElement("fonts", MainNs);
            writer.WriteAttributeString("count", "2");
            WriteFont(writer, bold: false);
            WriteFont(writer, bold: true);
            writer.WriteEndElement();

            writer.WriteStartElement("fills", MainNs);
            writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("fill", MainNs);
            writer.WriteStartElement("patternFill", MainNs);
            writer.WriteAttributeString("patternType", "none");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("fill", MainNs);
            writer.WriteStartElement("patternFill", MainNs);
            writer.WriteAttributeString("patternType", "gray125");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("borders", MainNs);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("border", MainNs);
            writer.WriteElementString("left", MainNs, string.Empty);
            writer.WriteElementString("right", MainNs, string.Empty);
            writer.WriteElementString("top", MainNs, string.Empty);
            writer.WriteElementString("bottom", MainNs, string.Empty);
            writer.WriteElementString("diagonal", MainNs, string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cellStyleXfs", MainNs);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("xf", MainNs);
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "0");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cellXfs", MainNs);
            writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("xf", MainNs);
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "0");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteEndElement();
            writer.WriteStartElement("xf", MainNs);
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "1");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("applyFont", "1");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cellStyles", MainNs);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("cellStyle", MainNs);
            writer.WriteAttributeString("name", "Normal");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("builtinId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
        });
    }

    private static void WriteFont(XmlWriter writer, bool bold)
    {
        writer.WriteStartElement("font", MainNs);
        if (bold)
        {
            writer.WriteElementString("b", MainNs, string.Empty);
        }

        writer.WriteStartElement("sz", MainNs);
        writer.WriteAttributeString("val", "11");
        writer.WriteEndElement();
        writer.WriteStartElement("name", MainNs);
        writer.WriteAttributeString("val", "Calibri");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorksheet(ZipArchive archive, IReadOnlyList<FileScanResult> results)
    {
        WriteXml(archive, "xl/worksheets/sheet1.xml", writer =>
        {
            writer.WriteStartElement("worksheet", MainNs);

            writer.WriteStartElement("sheetViews", MainNs);
            writer.WriteStartElement("sheetView", MainNs);
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane", MainNs);
            writer.WriteAttributeString("ySplit", "1");
            writer.WriteAttributeString("topLeftCell", "A2");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cols", MainNs);
            for (var column = 0; column < ExportColumns.All.Count; column++)
            {
                writer.WriteStartElement("col", MainNs);
                writer.WriteAttributeString("min", (column + 1).ToString());
                writer.WriteAttributeString("max", (column + 1).ToString());
                writer.WriteAttributeString("width", ExportColumns.All[column].Width.ToString("0"));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("sheetData", MainNs);

            writer.WriteStartElement("row", MainNs);
            writer.WriteAttributeString("r", "1");
            for (var column = 0; column < ExportColumns.All.Count; column++)
            {
                WriteCell(writer, 1, column, ExportColumns.All[column].Header, header: true);
            }
            writer.WriteEndElement();

            for (var index = 0; index < results.Count; index++)
            {
                var row = index + 2;
                writer.WriteStartElement("row", MainNs);
                writer.WriteAttributeString("r", row.ToString());
                for (var column = 0; column < ExportColumns.All.Count; column++)
                {
                    var value = ExportColumns.All[column].Value(results[index], index);
                    WriteCell(writer, row, column, value, header: false);
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        });
    }

    private static void WriteCell(XmlWriter writer, int row, int column, string? value, bool header)
    {
        writer.WriteStartElement("c", MainNs);
        writer.WriteAttributeString("r", ColumnName(column) + row);
        writer.WriteAttributeString("t", "inlineStr");
        if (header)
        {
            writer.WriteAttributeString("s", "1");
        }

        writer.WriteStartElement("is", MainNs);
        writer.WriteStartElement("t", MainNs);
        writer.WriteAttributeString("xml", "space", null, "preserve");
        writer.WriteString(XmlTextSanitizer.Clean(value));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var value = index + 1;
        while (value > 0)
        {
            value--;
            name = (char)('A' + value % 26) + name;
            value /= 26;
        }

        return name;
    }

    private static void WriteXml(ZipArchive archive, string entryName, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        };

        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }
}
