using System;
using Element.Configuration.Overlay;

namespace Element.Domain.Overlay.Variables;

/// <summary>The words a variable reads with, in the language shown: the caller's, since the domain has none.</summary>
internal sealed class OverlayVariableWords
{
  public OverlayVariableWords(string conditionStart, string then, string otherwise, string yes, string no)
  {
    ConditionStart = conditionStart;
    Then = then;
    Otherwise = otherwise;
    Yes = yes;
    No = no;
  }

  /// <summary>Before a condition: "if".</summary>
  public string ConditionStart { get; }

  public string Then { get; }

  public string Otherwise { get; }

  /// <summary>True and false, as shown.</summary>
  public string Yes { get; }

  public string No { get; }

  /// <summary>How a point is called: "Top left", "Auto". Its plain name unless the caller says otherwise.</summary>
  public Func<OverlayGrowth, string> Point { get; set; } = point => point.ToString();

  /// <summary>Plain symbols, for when no language is at hand.</summary>
  public static readonly OverlayVariableWords Plain = new("if", "→", "·", "true", "false");
}
