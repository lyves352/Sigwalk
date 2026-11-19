namespace Sigwalk.Core.Models;

/// <summary>扫描结果统计。签名状态计数只统计已完成且签名检测成功的文件。</summary>
public sealed class ScanStatistics
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Scanning { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public int Valid { get; init; }
    public int Unsigned { get; init; }
    public int Invalid { get; init; }
    public int Unknown { get; init; }

    public static ScanStatistics From(IEnumerable<FileScanResult> results)
    {
        int total = 0, pending = 0, scanning = 0, completed = 0, failed = 0;
        int valid = 0, unsigned = 0, invalid = 0, unknown = 0;

        foreach (var item in results)
        {
            total++;
            switch (item.ScanStatus)
            {
                case ScanStatus.Pending:
                    pending++;
                    continue;
                case ScanStatus.Scanning:
                    scanning++;
                    continue;
                case ScanStatus.Failed:
                    failed++;
                    continue;
            }

            completed++;
            if (!item.SignatureChecked)
            {
                continue;
            }

            switch (item.SignatureStatus)
            {
                case SignatureStatus.Valid:
                    valid++;
                    break;
                case SignatureStatus.Unsigned:
                    unsigned++;
                    break;
                case SignatureStatus.Invalid:
                    invalid++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        return new ScanStatistics
        {
            Total = total,
            Pending = pending,
            Scanning = scanning,
            Completed = completed,
            Failed = failed,
            Valid = valid,
            Unsigned = unsigned,
            Invalid = invalid,
            Unknown = unknown,
        };
    }
}
