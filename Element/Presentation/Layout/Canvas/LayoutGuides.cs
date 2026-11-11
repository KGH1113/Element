using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Presentation.Layout.Canvas;

/// <summary>The lines a dragged element snaps to, and the marks of equal spacing.</summary>
internal sealed class LayoutGuides
{
  private RectTransform _vertical,
    _horizontal;
  private LayoutSpacingGraphic _spacing;
  private float _x = float.NaN,
    _y = float.NaN;
  private LayoutSpacingGuide _horizontalSpacing,
    _verticalSpacing;

  public void Build(SettingsUiFactory ui, Transform root)
  {
    _vertical = ui.Panel(
      "Vertical alignment",
      root,
      0,
      0,
      1,
      Screen.height,
      new Color(1, 1, 1, .7f),
      false
    ).rectTransform;
    _horizontal = ui.Panel(
      "Horizontal alignment",
      root,
      0,
      0,
      Screen.width,
      1,
      new Color(1, 1, 1, .7f),
      false
    ).rectTransform;
    _spacing = ui.Rect("Equal spacing guides", root, 0, 0, Screen.width, Screen.height)
      .gameObject.AddComponent<LayoutSpacingGraphic>();
    _spacing.raycastTarget = false;
    SettingsUiFactory.Stretch(_spacing.rectTransform, 0, 0, 0, 0);
  }

  /// <summary>Hides the alignment lines.</summary>
  public void HideLines() => _x = _y = float.NaN;

  /// <summary>Hides the spacing marks.</summary>
  public void HideSpacing() => _horizontalSpacing = _verticalSpacing = default;

  /// <summary>
  /// Moves <paramref name="box"/> onto the edges, centers and equal spacing of
  /// <paramref name="references"/> within <paramref name="threshold"/>, showing where.
  /// </summary>
  public LayoutBounds Snap(LayoutBounds box, List<LayoutBounds> references, float threshold) =>
    LayoutSnap.Move(
      box,
      references,
      Screen.width,
      Screen.height,
      threshold,
      out _x,
      out _y,
      out _horizontalSpacing,
      out _verticalSpacing
    );

  /// <summary>Once a frame: draws what the last snap found.</summary>
  public void Refresh(float uiScale)
  {
    _spacing.color = SettingsPalette.SoftAccent;
    _spacing.SetGuides(_horizontalSpacing, _verticalSpacing, uiScale);
    _vertical.gameObject.SetActive(!float.IsNaN(_x));
    _horizontal.gameObject.SetActive(!float.IsNaN(_y));
    if (!float.IsNaN(_x))
      SettingsUiFactory.Place(_vertical, _x, 0, 1, Screen.height);
    if (!float.IsNaN(_y))
      SettingsUiFactory.Place(_horizontal, 0, _y, Screen.width, 1);
  }
}
