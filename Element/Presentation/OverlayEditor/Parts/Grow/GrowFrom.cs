using System;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// Where an edited element grows from: a field like any other. It holds a
/// point (one of nine, or Auto), or a condition that gives points, and opens
/// in the dock, where a point's input method is the nine points. While it is
/// open a dot on the preview marks the point that stays put. Done saves the
/// field with the element.
/// </summary>
internal sealed class GrowFrom
{
  /// <summary>The field's key in the dock.</summary>
  public const string Key = "grow-from";

  private const float MarkSize = 12;

  private OverlayElementTarget _element;
  private OverlayVariable _variable = new(OverlayPiece.PointPiece(OverlayGrowth.Auto));
  private UiCapsuleGraphic _mark;

  public GrowFrom() => Slot = new FormulaSlot(Key, OverlayValueType.Point, () => _variable, held => _variable = held);

  /// <summary>The dock's way to read and write what the field holds.</summary>
  public FormulaSlot Slot { get; }

  /// <summary>The column's Grow from field; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button { get; private set; }

  /// <summary>Starts from what <paramref name="element"/> has saved.</summary>
  public void Open(OverlayElementTarget element)
  {
    _element = element;
    _variable = element.GrowFromVariable;
  }

  /// <summary>The Grow from field, added where the column has it; <paramref name="open"/> opens it in the dock.</summary>
  public PropertyButtonViewModel BuildButton(Action open) =>
    Button = new PropertyButtonViewModel("Grow from", L("기준점", "Grow from"), open, field: OverlayValueType.Point);

  /// <summary>The dot, over the editor's controls.</summary>
  public void Build(EditorFramePanel screen)
  {
    _mark = screen.Ui.Capsule("Grow point", screen.Frame, 0, 0, MarkSize, MarkSize, UiPalette.SoftAccent);
    _mark.gameObject.SetActive(false);
  }

  /// <summary>The point the field gives now, with <paramref name="samples"/> for the values it reads.</summary>
  public OverlayGrowth Growth(IOverlayNumberSource samples, OverlayVariable typed = null) =>
    (typed ?? _variable).TryPoint(samples, out OverlayGrowth point) ? point : OverlayGrowth.Auto;

  /// <summary>
  /// The field shows what it holds (<paramref name="typed"/> while the dock
  /// has it open) and, when that is worked out, the point it gives; lit
  /// while <paramref name="active"/>.
  /// </summary>
  public void ShowButton(bool active, OverlayVariable typed, IOverlayNumberSource samples)
  {
    OverlayVariable held = typed ?? _variable;
    bool plain = held.Count == 1 && held[0].Kind == OverlayPieceKind.Point;
    Button.ShowField(held, plain ? "" : GrowPickerViewModel.ShortName(Growth(samples, held)), active);
  }

  /// <summary>What the field gives, as the dock's line says it.</summary>
  public string Result(IOverlayNumberSource samples, OverlayVariable typed) =>
    GrowPickerViewModel.ShortName(Growth(samples, typed));

  /// <summary>Saves the field with <paramref name="element"/>, which follows it from then on.</summary>
  public void Save(OverlayElementTarget element, IOverlayNumberSource values) => element.SetGrowFrom(_variable, values);

  /// <summary>
  /// Puts the dot on the point of the preview's box (frame units) that stays
  /// put; with Auto, the point picked from where the element sits.
  /// </summary>
  public void PlaceMark(float x, float y, float width, float height, OverlayGrowth growth)
  {
    Anchor9 point = OverlayLayoutPosition.ValidGrowth(growth) ? (Anchor9)(int)growth : _element.GrowPoint;
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
