using System;
using System.Globalization;
using System.Linq;

namespace ImplResourcePack.Updates;

/// <summary>
/// The one version parser for the launcher, the updater and the mod. Its
/// <see cref="ToString"/> is the canonical name of a version folder.
/// </summary>
internal sealed class SemanticVersion : IComparable<SemanticVersion>
{
  private readonly int[] _core;
  private readonly string[] _prerelease;

  private SemanticVersion(int[] core, string[] prerelease)
  {
    _core = core;
    _prerelease = prerelease;
  }

  public bool IsPrerelease => _prerelease.Length > 0;

  public static SemanticVersion Parse(string value)
  {
    if (!TryParse(value, out SemanticVersion version))
      throw new FormatException("Invalid version: " + value);
    return version;
  }

  /// <summary>Accepts an optional leading v and build metadata; both are dropped.</summary>
  public static bool TryParse(string value, out SemanticVersion version)
  {
    version = null;
    if (string.IsNullOrWhiteSpace(value))
      return false;
    string text = value.Trim();
    if (text[0] is 'v' or 'V')
      text = text.Substring(1);
    int build = text.IndexOf('+');
    if (build >= 0)
      text = text.Substring(0, build);
    string prereleaseText = null;
    int dash = text.IndexOf('-');
    if (dash >= 0)
    {
      prereleaseText = text.Substring(dash + 1);
      text = text.Substring(0, dash);
    }
    string[] parts = text.Split('.');
    if (parts.Length != 3)
      return false;
    int[] core = new int[3];
    for (int i = 0; i < 3; i++)
    {
      if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out core[i]))
        return false;
    }
    string[] prerelease = Array.Empty<string>();
    if (prereleaseText != null)
    {
      prerelease = prereleaseText.Split('.');
      if (prerelease.Any(identifier => !IsIdentifier(identifier)))
        return false;
    }
    version = new SemanticVersion(core, prerelease);
    return true;
  }

  /// <summary>The canonical form of <paramref name="value"/>, e.g. "v0.3.0" becomes "0.3.0".</summary>
  public static string Normalize(string value) => Parse(value).ToString();

  public static bool Same(string left, string right) =>
    TryParse(left, out SemanticVersion a) && TryParse(right, out SemanticVersion b) && a.CompareTo(b) == 0;

  public int CompareTo(SemanticVersion other)
  {
    if (other == null)
      return 1;
    for (int i = 0; i < 3; i++)
    {
      int comparison = _core[i].CompareTo(other._core[i]);
      if (comparison != 0)
        return comparison;
    }
    if (_prerelease.Length == 0 || other._prerelease.Length == 0)
      return other._prerelease.Length.CompareTo(_prerelease.Length);
    int length = Math.Min(_prerelease.Length, other._prerelease.Length);
    for (int i = 0; i < length; i++)
    {
      int comparison = CompareIdentifier(_prerelease[i], other._prerelease[i]);
      if (comparison != 0)
        return comparison;
    }
    return _prerelease.Length.CompareTo(other._prerelease.Length);
  }

  public override string ToString()
  {
    string core = string.Join(".", _core.Select(part => part.ToString(CultureInfo.InvariantCulture)));
    return _prerelease.Length == 0 ? core : core + "-" + string.Join(".", _prerelease);
  }

  private static int CompareIdentifier(string left, string right)
  {
    bool leftNumeric = left.All(char.IsDigit);
    bool rightNumeric = right.All(char.IsDigit);
    if (leftNumeric && rightNumeric)
    {
      string a = left.TrimStart('0');
      string b = right.TrimStart('0');
      int comparison = a.Length.CompareTo(b.Length);
      return comparison != 0 ? comparison : string.CompareOrdinal(a, b);
    }
    if (leftNumeric != rightNumeric)
      return leftNumeric ? -1 : 1;
    return string.CompareOrdinal(left, right);
  }

  private static bool IsIdentifier(string value) =>
    !string.IsNullOrEmpty(value)
    && value.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-');
}
