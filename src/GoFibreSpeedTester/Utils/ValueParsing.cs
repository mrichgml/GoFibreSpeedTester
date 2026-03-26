using System.Globalization;
using System.Text.RegularExpressions;

namespace GoFibreSpeedTester.Utils;

public static partial class ValueParsing
{
  public static double? ParseNullableDouble(string? text)
  {
    if (string.IsNullOrWhiteSpace(text)) return null;

    // Extract first decimal-ish number (handles "123.45", "123,45", "123")
    var m = FirstNumber().Match(text);
    if (!m.Success) return null;

    var raw = m.Value.Replace(',', '.');
    return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
  }

  public static double? ParseNullablePercent(string? text)
  {
    var v = ParseNullableDouble(text);
    return v;
  }

  [GeneratedRegex(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled)]
  private static partial Regex FirstNumber();
}

