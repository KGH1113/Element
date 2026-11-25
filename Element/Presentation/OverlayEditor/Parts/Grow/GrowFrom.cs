using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using System;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// Where an edited element grows from: the property column's Grow from row,
/// the nine points it opens in the column, and a dot on the preview at the point that
/// stays put while the popover is open. Done saves the choice with the element.
/// </summary>
internal sealed class GrowFrom
{
  private const float MarkSize = 12;

  private readonly Action _closeOthers;
  private readonly Action _changed;
  private OverlayElementTarget _element;
  private InspectorPanel _inspector;
  private GrowPopoverPanel _popover;
  private UiCapsuleGraphic _mark;

  /// <param name="closeOthers">Closes what else is open before the popover opens.</param>
  /// <param name="changed">Redraws the editor after a click.</param>
  public GrowFrom(Action closeOthers, Action changed)
  {
    _closeOthers = closeOthers;
    _changed = changed;
  }

  public OverlayGrowth Growth { get; private set; }

  /// <summary>The column's Grow from row; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button { get; private set; }

  public bool Visible => _popover.Visible;

  /// <summary>The popover with the nine points; made by <see cref="Build"/>.</summary>
  public IPopover Popover => _popover;

  /// <summary>Starts from what <paramref name="element"/> has saved.</summary>
  public void Open(OverlayElementTarget element)
  {
    _element = element;
    Growth = element.Settings?.GrowFrom ?? OverlayGrowth.Auto;
  }

  /// <summary>The Grow from row, added where the column has it.</summary>
  public PropertyButtonViewModel BuildButton() =>
    Button = new PropertyButtonViewModel(
      "Grow from",
      L("기준점", "Grow from"),
      Toggle,
      opens: true,
      fieldGlyph: "⊙"
    );

  /// <summary>The points, opening in <paramref name="inspector"/>'s column, and the dot, over the editor's controls.</summary>
  public void Build(EditorFramePanel screen, InspectorPanel inspector)
  {
    _inspector = inspector;
    _popover = new GrowPopoverPanel(screen, Pick, inColumn: true);
    _mark = screen.Ui.Capsule("Grow point", screen.Frame, 0, 0, MarkSize, MarkSize, UiPalette.SoftAccent);
    _mark.gameObject.SetActive(false);
  }

  public void Hide() => _popover?.Hide();

  public bool Contains(Vector2 screen) => _popover.Contains(screen);

  /// <summary>The row opens the points in the column, and closes them again.</summary>
  private void Toggle()
  {
    bool open = _popover.Visible;
    _closeOthers();
    if (!open)
    {
      _popover.ShowIn(_inspector.Right, _inspector.ToolTop);
      _popover.Refresh(Growth, PointNow());
    }
    _changed();
  }

  private void Pick(OverlayGrowth growth)
  {
    Growth = growth;
    _popover.Refresh(Growth, PointNow());
    _changed();
  }

  // With Auto, the point picked from where the element sits on screen.
  private Anchor9 PointNow() => OverlayLayoutPosition.ValidGrowth(Growth) ? (Anchor9)(int)Growth : _element.GrowPoint;

  /// <summary>Shows the row's choice: the short name of the point, lit while the points are open.</summary>
  public void ShowButton() =>
    Button.ShowField(
      new FieldChip("point", GrowPickerViewModel.ShortName(Growth), OverlayValueType.Any) { Bare = true },
      _popover.Visible
    );

  /// <summary>
  /// While the popover is open, puts the dot on the point of the preview's
  /// box (frame units) that stays put; hides it otherwise.
  /// </summary>
  public void PlaceMark(float x, float y, float width, float height)
  {
    Anchor9 point = PointNow();
    Vector2 at = new(
      x + width * OverlayLayoutPosition.AnchorX(point),
      y + height * OverlayLayoutPosition.AnchorY(point)
    );
    _mark.gameObject.SetActive(true);
    UiFactory.Place(_mark.rectTransform, at.x - MarkSize * .5f, at.y - MarkSize * .5f, MarkSize, MarkSize);
    _mark.transform.SetAsLastSibling();
  }

  public void HideMark() => _mark.gameObject.SetActive(false);
}
