using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Nine points of a box, one of which stays put when the element changes size.
/// Like a canvas-size anchor picker: the chosen point is filled, and arrows
/// on the points around it show which way the element grows. Auto follows the
/// point picked from where the element sits on screen.
/// </summary>
internal sealed class OverlayGrowPicker
{
  public const float Size = 78;
  private const float Cell = 26,
    AutoHeight = 26;

  private readonly List<(Button Button, UiCapsuleGraphic Dot, OverlayGrowArrow Arrow)> _cells = new();
  private readonly UiCapsuleGraphic _autoSurface;
  private readonly TextMeshProUGUI _autoLabel;

  public OverlayGrowPicker(UiFactory ui, Transform parent, bool reduceMotion, Action<OverlayGrowth> picked)
  {
    UiCapsuleGraphic surface = ui.Capsule("Grow from", parent, 0, 0, Size, Size, UiPalette.Bar);
    surface.raycastTarget = true;
    Root = surface.rectTransform;
    // The box the points belong to.
    Color edge = new(1, 1, 1, .16f);
    float inset = Cell * .5f + 1;
    float span = Size - inset * 2;
    ui.Panel("Top edge", Root, inset, inset, span, 1, edge, false);
    ui.Panel("Bottom edge", Root, inset, inset + span - 1, span, 1, edge, false);
    ui.Panel("Left edge", Root, inset, inset, 1, span, edge, false);
    ui.Panel("Right edge", Root, inset + span - 1, inset, 1, span, edge, false);
    for (int i = 0; i < 9; i++)
    {
      OverlayGrowth growth = (OverlayGrowth)i;
      Button button = UiCapsules.CapsuleButton(
        ui,
        Root,
        "Grow from " + growth,
        "",
        1 + i % 3 * Cell,
        1 + i / 3 * Cell,
        Cell - 2,
        Cell - 2,
        12,
        () => picked(growth),
        true,
        reduceMotion
      );
      UiCapsuleGraphic dot = ui.Capsule("Point", button.transform, 0, 0, 6, 6, Color.white);
      Center(dot.rectTransform, 6);
      OverlayGrowArrow arrow = ui.Rect("Arrow", button.transform, 0, 0, Cell - 2, Cell - 2)
        .gameObject.AddComponent<OverlayGrowArrow>();
      arrow.raycastTarget = false;
      arrow.color = UiPalette.Foreground;
      _cells.Add((button, dot, arrow));
    }
    Auto = UiCapsules.CapsuleButton(
      ui,
      parent,
      "Grow automatically",
      L("자동", "Auto"),
      0,
      0,
      10,
      AutoHeight,
      12,
      () => picked(OverlayGrowth.Auto),
      false,
      reduceMotion
    );
    _autoSurface = (UiCapsuleGraphic)Auto.targetGraphic;
    _autoLabel = Auto.GetComponentInChildren<TextMeshProUGUI>();
    AutoWidth = Mathf.Ceil(_autoLabel.GetPreferredValues(_autoLabel.text).x) + 26;
    Caption = ui.Text("", parent, 0, 0, 10, 10, 12, UiPalette.Muted);
    Caption.textWrappingMode = TextWrappingModes.Normal;
    Caption.alignment = TextAlignmentOptions.TopLeft;
  }

  public RectTransform Root { get; }
  public Button Auto { get; }
  public float AutoWidth { get; }
  public TextMeshProUGUI Caption { get; }

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
    Color accent = UiPalette.SoftAccent;
    for (int i = 0; i < 9; i++)
    {
      (Button _, UiCapsuleGraphic dot, OverlayGrowArrow arrow) = _cells[i];
      int dx = i % 3 - column,
        dy = i / 3 - row;
      bool selected = i == chosen;
      bool next = !selected && Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1;
      dot.gameObject.SetActive(!next);
      arrow.gameObject.SetActive(next);
      if (next)
        arrow.Point(new Vector2(dx, dy));
      float size = selected ? 12 : 5;
      Center(dot.rectTransform, size);
      dot.color = selected ? (auto ? Color.Lerp(UiPalette.Muted, accent, .6f) : accent) : new Color(1, 1, 1, .32f);
    }
    _autoSurface.color = Color.white;
    ColorBlock colors = Auto.colors;
    Color on = new(accent.r, accent.g, accent.b, .3f);
    colors.normalColor = colors.selectedColor = auto ? on : new Color(1, 1, 1, .07f);
    ((UiButton)Auto).SetPalette(colors);
    _autoLabel.color = auto ? UiPalette.Foreground : UiPalette.Muted;
    Caption.text = auto
      ? L("자동: 화면에 놓인 자리에 맞춰요. 지금은 ", "Auto: follows where it sits. For now, ")
        + PointName((Anchor9)chosen, false)
        + L(" 고정.", " stays.")
      : PointName((Anchor9)chosen, true) + L(" 고정. 화살표 쪽으로 커져요.", " stays. It grows along the arrows.");
  }

  public bool Contains(Vector2 screen) =>
    PopoverPlacement.Contains(Root, screen) || PopoverPlacement.Contains((RectTransform)Auto.transform, screen);

  /// <summary>What a grow button says: "자동", "왼쪽 위" or "Top left".</summary>
  public static string ShortName(OverlayGrowth growth)
  {
    if (!Domain.Layout.OverlayLayoutPosition.ValidGrowth(growth))
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

  private static void Center(RectTransform rect, float size)
  {
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(size, size);
  }
}
