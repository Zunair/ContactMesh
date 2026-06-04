using System.Text.RegularExpressions;

namespace ContactMesh.Core.Merge;

public sealed class PhoneNormalizer
{
    private static readonly Regex ExtensionPattern = new(@"(ext\.?|x|p)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string NormalizeForComparison(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var withoutExtension = ExtensionPattern.Replace(phoneNumber, string.Empty);
        var digits = new string(withoutExtension.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
        {
            digits = digits[1..];
        }

        return digits;
    }

    public string FormatForDisplay(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var normalized = this.NormalizeForComparison(phoneNumber);
        if (normalized.Length == 10)
        {
            return $"{normalized[..3]}-{normalized[3..6]}-{normalized[6..]}";
        }

        return phoneNumber.Trim();
    }
}
