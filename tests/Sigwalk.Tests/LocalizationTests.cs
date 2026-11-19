using System.Text.RegularExpressions;
using Sigwalk.Core.Localization;
using Sigwalk.Core.Models;
using Sigwalk.Core.Utilities;

namespace Sigwalk.Tests;

[Collection("Localization")]
public class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled);

    [Fact]
    public void ChineseAndEnglishTablesHaveSameKeysAndPlaceholders()
    {
        var chinese = LocalizationService.LoadTable(AppLanguage.Chinese);
        var english = LocalizationService.LoadTable(AppLanguage.English);

        Assert.NotEmpty(chinese);
        Assert.Equal(
            chinese.Keys.OrderBy(key => key),
            english.Keys.OrderBy(key => key));

        foreach (var (key, value) in chinese)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"中文 key 为空：{key}");
            Assert.False(string.IsNullOrWhiteSpace(english[key]), $"英文 key 为空：{key}");
            var chinesePlaceholders = Placeholder.Matches(value)
                .Select(match => match.Groups[1].Value)
                .OrderBy(index => index);
            var englishPlaceholders = Placeholder.Matches(english[key])
                .Select(match => match.Groups[1].Value)
                .OrderBy(index => index);
            Assert.Equal(chinesePlaceholders, englishPlaceholders);
        }
    }

    [Fact]
    public void MissingKeyFallsBackToKeyAndStatusTextFollowsLanguage()
    {
        var original = Loc.Language;
        try
        {
            Loc.SetLanguage(AppLanguage.Chinese);
            Assert.Equal("不存在的键", Loc.T("不存在的键"));
            Assert.Equal("签名有效", StatusText.DescribeSignatureStatus(SignatureStatus.Valid));

            Loc.SetLanguage(AppLanguage.English);
            Assert.Equal("Valid signature", StatusText.DescribeSignatureStatus(SignatureStatus.Valid));
        }
        finally
        {
            Loc.SetLanguage(original);
        }
    }
}

[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationCollection
{
}
