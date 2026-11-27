using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// Nine points of a box, one of which stays put when the element changes size.
/// Like a canvas-size anchor picker: the chosen point is filled, and arrows
/// on the points around it show which way the element grows. Auto follows the
/// point picked from where the element sits on screen, and the caption says
/// what the choice does. <see cref="GrowPointsView"/> draws the points.
/// </summary>
internal sealed class GrowPickerViewModel : ViewModel
{
  private static readonly UiColor Faint = new Color(1, 1, 1, .32f);

  // Auto sits on the popover's panel: a little lighter than it, opaque.
  private static readonly Color Rest = UiPalette.Wash(UiPalette.Popover, .07f);
  private readonly UiColor _chosen,
    _followed,
    _autoOn;

  /// <param name="picked">A point or Auto was clicked; the owner shows the choice with <see cref="Show"/>.</param>
  public GrowPickerViewModel(Action<OverlayGrowth> picked)
  {
    Color accent = UiPalette.SoftAccent;
    _chosen = accent;
    _followed = Color.Lerp(UiPalette.Muted, accent, .6f);
    _autoOn = Color.Lerp(Rest, accent, .3f);
    GrowPointViewModel[] points = new GrowPointViewModel[9];
    for (int i = 0; i < points.Length; i++)
      points[i] = Own(new GrowPointViewModel((OverlayGrowth)i, picked));
    Points = points;
    Auto = Command(() => picked(OverlayGrowth.Auto));
  }

  /// <summary>The nine points, row by row from the top left.</summary>
  public readonly IReadOnlyList<GrowPointViewModel> Points;

  /// <summary>Auto's fill: lit while it is the choice.</summary>
  public readonly State<UiColor> AutoFill = new(Rest);

  /// <summary>Auto's words: white while it is the choice, muted otherwise.</summary>
  public readonly State<UiColor> AutoInk = new(Colors.Muted);

  /// <summary>What the choice does.</summary>
  public readonly State<string> Caption = new("");

  /// <summary>Auto is the choice.</summary>
  public readonly State<bool> IsAuto = new(true);

  public Command Auto { get; }

  /// <summary>
  /// Shows <paramref name="growth"/>; with Auto, <paramref name="point"/> is
  /// the point picked for the element right now.
  /// </summary>
  public void Show(OverlayGrowth growth, Anchor9 point)
  {
    bool auto = growth == OverlayGrowth.Auto;
    int chosen = auto ? (int)point : (int)growth;
    int row = chosen / 3,
      column = chosen % 3;
    for (int i = 0; i < Points.Count; i++)
    {
      GrowPointViewModel cell = Points[i];
      int dx = i % 3 - column,
        dy = i / 3 - row;
      bool selected = i == chosen;
      bool next = !selected && Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1;
      cell.Look.Value =
        selected ? GrowLook.Chosen
        : next ? GrowLook.Arrow
        : GrowLook.Dot;
      if (next)
        cell.Direction.Value = new Vector2(dx, dy);
      cell.DotColor.Value =
        !selected ? Faint
        : auto ? _followed
        : _chosen;
    }
    IsAuto.Value = auto;
    AutoFill.Value = auto ? _autoOn : Rest;
    AutoInk.Value = auto ? Colors.Foreground : Colors.Muted;
    Caption.Value = auto
      ? L("자동: 화면에 놓인 자리에 맞춰요. 지금은 ", "Auto: follows where it sits. For now, ")
        + PointName((Anchor9)chosen, false)
        + L(" 고정.", " stays.")
      : PointName((Anchor9)chosen, true) + L(" 고정. 화살표 쪽으로 커져요.", " stays. It grows along the arrows.");
  }

  /// <summary>What a grow button says: "자동", "왼쪽 위" or "Top left".</summary>
  public static string ShortName(OverlayGrowth growth)
  {
    if (!OverlayLayoutPosition.ValidGrowth(growth))
      return L("자동", "Auto");
    string name = PointName((Anchor9)(int)growth, true);
    return Korean ? name : char.ToUpperInvariant(name[4]) + name.Substring(5);
  }

  /// <summary>The point's name: "왼쪽 위" or "Top left".</summary>
  public static string PointName(Anchor9 point, bool sentence)
  {
    string english = point switch
    {
      Anchor9.TopLeft => "top left",
      Anchor9.Top => "top center",
      Anchor9.TopRight => "top right",
      Anchor9.Left => "left center",
      Anchor9.Right => "right center",
      Anchor9.BottomLeft => "bottom left",
      Anchor9.Bottom => "bottom center",
      Anchor9.BottomRight => "bottom right",
      _ => "center",
    };
    if (!Korean)
      return sentence ? "The " + english : "the " + english;
    return point switch
    {
      Anchor9.TopLeft => "왼쪽 위",
      Anchor9.Top => "위 가운데",
      Anchor9.TopRight => "오른쪽 위",
      Anchor9.Left => "왼쪽 가운데",
      Anchor9.Right => "오른쪽 가운데",
      Anchor9.BottomLeft => "왼쪽 아래",
      Anchor9.Bottom => "아래 가운데",
      Anchor9.BottomRight => "오른쪽 아래",
      _ => "가운데",
    };
  }
}
