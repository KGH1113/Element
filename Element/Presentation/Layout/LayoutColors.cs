using UnityEngine;

namespace Element.Presentation.Layout;

/// <summary>The layout editor's own colors, beside the settings palette it shares.</summary>
internal static class LayoutColors
{
  /// <summary>The toolbars' surface.</summary>
  public static readonly Color Toolbar = new(.07f, .07f, .07f, 1);

  /// <summary>The add menu and its lists.</summary>
  public static readonly Color Menu = new(.105f, .105f, .105f);

  /// <summary>An icon whose button can't be pressed now.</summary>
  public static readonly Color Disabled = new(.4f, .4f, .4f);
}
