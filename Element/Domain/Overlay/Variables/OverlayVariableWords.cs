namespace Element.Domain.Overlay.Variables;

/// <summary>The words a variable reads with, in the language shown: the caller's, since the domain has none.</summary>
internal sealed class OverlayVariableWords
{
  public OverlayVariableWords(
    string conditionStart,
    string then,
    string otherwise,
    string yes,
    string no,
    string custom,
    string unnamed
  )
  {
    ConditionStart = conditionStart;
    Then = then;
    Otherwise = otherwise;
    Yes = yes;
    No = no;
    Custom = custom;
    Unnamed = unnamed;
  }

  /// <summary>Before a condition: "if".</summary>
  public string ConditionStart { get; }

  public string Then { get; }

  public string Otherwise { get; }

  /// <summary>True and false, as shown.</summary>
  public string Yes { get; }

  public string No { get; }

  /// <summary>Shown for a made value nobody knows any more.</summary>
  public string Custom { get; }

  /// <summary>Shown for a made value whose name was left empty.</summary>
  public string Unnamed { get; }

  /// <summary>Plain symbols, for when no language is at hand.</summary>
  public static readonly OverlayVariableWords Plain = new("if", "→", "·", "true", "false", "?", "…");
}
