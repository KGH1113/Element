using System;
using System.Collections.Generic;
using Element.Application.Input;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// A color picker on one of Element's own canvases, shown and hidden by its
/// owner: the shared overlay highlight, independent RGBA colors and overlay
/// element colors. A panel shows the picker's screen
/// (<see cref="ColorPickerCardView"/>, or <see cref="ColorPickerColumnsView"/>
/// once <see cref="LayOutAsColumn"/>); <see cref="Model"/> keeps the color
/// and the gestures. Consumers decide what the color affects.
/// </summary>
internal sealed class UiColorPicker : IDisposable
{
  public const float Width = ColorPickerCardView.Width;

  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly UiFactory _ui;
  private readonly ScreenPanel _panel;
  private readonly ColorPickerParts _parts;
  private readonly List<TMP_InputField> _fields = new();
  private Shape _shape;
  private bool _column;
  private float _columnWidth = ColorPickerLayout.Column;
  private bool _stale = true;

  /// <param name="under">What the picker is drawn on, which its color bar's buttons are lighter than: an overlay editor's dimmed view when null.</param>
  public UiColorPicker(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion, Color? under = null)
  {
    _font = font;
    _reduceMotion = reduceMotion;
    _ui = new UiFactory(font) { ReduceMotion = reduceMotion };
    _panel = new ScreenPanel("Color picker", parent, Color.clear, keyboard: false);
    // A press on the picker between its controls stays on the picker.
    Image blocker = Root.gameObject.AddComponent<Image>();
    blocker.color = Color.clear;
    blocker.raycastTarget = true;
    Model = new ColorPickerViewModel { CloseRequested = Hide, LayoutChanged = () => _stale = true };
    RectTransform park = _ui.Rect("Parts", Root, 0, 0, 0, 0);
    park.gameObject.SetActive(false);
    RectTransform opacitySlot = _ui.Rect(
      "Opacity slot",
      park,
      0,
      0,
      ColorPickerLayout.Box,
      ColorPickerLayout.FieldHeight
    );
    Gradient = new UiGradientBar(_ui, park, Model, under ?? UiPalette.EditorDim);
    Model.Gradient = Gradient;
    _parts = new ColorPickerParts(park, opacitySlot, Gradient);
    Root.gameObject.SetActive(false);
  }

  private enum Shape : byte
  {
    None,
    Card,
    Column,
    TwoColumns,

    // An overlay color that can only be solid: Highlight/Custom over its own controls.
    SourceColumn,

    // A solid overlay color picked in the place of a keypad: the square, hue and HEX.
    Compact,
  }

  private float _compactWidth,
    _compactHeight;

  /// <summary>
  /// Solid colors are picked in a place <paramref name="width"/> by
  /// <paramref name="height"/>, such as where a keypad is: only the square,
  /// its hue and HEX, the opacity being a field of the owner's. A gradient
  /// still takes the two columns with its color bar.
  /// </summary>
  public void UseCompact(float width, float height)
  {
    _compactWidth = width;
    _compactHeight = height;
  }

  /// <summary>Whether it shows as a solid color in its compact place now.</summary>
  public bool InCompactPlace => _shape == Shape.Compact;

  /// <summary>
  /// The owner shows another color than the one its other parts refresh (a
  /// color's case): refreshes leave it alone until the owner shows its own again.
  /// </summary>
  public bool ShowsOther { get; set; }

  /// <summary>What the picker shows and does.</summary>
  public ColorPickerViewModel Model { get; }

  public RectTransform Root => _panel.Root;

  /// <summary>
  /// It stays open but shows nothing and takes no clicks: what is picked
  /// now is no color (a number inside a condition that gives colors), and
  /// that piece's own input method has the place under it.
  /// </summary>
  public bool Resting
  {
    set
    {
      CanvasGroup group = Root.GetComponent<CanvasGroup>();
      if (group == null)
        group = Root.gameObject.AddComponent<CanvasGroup>();
      group.alpha = value ? 0 : 1;
      group.blocksRaycasts = !value;
    }
  }

  /// <summary>The color bar of an overlay editor's picker: its stops, kinds and the picked stop.</summary>
  public UiGradientBar Gradient { get; }

  /// <summary>The color bar shows: in an overlay editor's picker.</summary>
  public bool GradientAvailable => Model.SourceMode && _column && _shape != Shape.SourceColumn;

  /// <summary>Its height as laid out now.</summary>
  public float Height
  {
    get
    {
      Fit();
      return _panel.Size.y;
    }
  }

  /// <summary>The width as laid out now.</summary>
  public float CurrentWidth =>
    !_column ? Width
    : GradientAvailable ? TwoColumnsWidth
    : _shape == Shape.SourceColumn ? ColorPickerLayout.Column
    : _shape == Shape.Compact ? _compactWidth
    : _columnWidth;

  public bool Column => _column;

  public bool Visible => Root.gameObject.activeSelf;

  /// <summary>One of its fields has the keyboard.</summary>
  public bool IsTyping
  {
    get
    {
      if (!Visible)
        return false;
      foreach (TMP_InputField input in _fields)
        if (input != null && input.isFocused)
          return true;
      return false;
    }
  }

  /// <summary>Called once it hides.</summary>
  public Action Hidden;

  /// <summary>
  /// Where the opacity box sits. With <see cref="ExternalOpacity"/> the owner
  /// puts its own opacity control here and the typed box is hidden.
  /// </summary>
  public RectTransform OpacitySlot => _parts.OpacitySlot;

  /// <summary>The owner shows and edits the opacity in <see cref="OpacitySlot"/>.</summary>
  public bool ExternalOpacity
  {
    get => Model.ExternalOpacity;
    set => Model.ExternalOpacity = value;
  }

  /// <summary>
  /// Lays the picker out borderless for the panel beside it, such as the
  /// keypad below it: no card, title or close. An overlay editor's picker
  /// (<see cref="ShowSource"/>) takes two columns, the color square and its
  /// bars on the left, and Highlight/Custom, the color bar and the numbers on
  /// the right, so it stays short; others are one column
  /// <paramref name="width"/> wide. Whoever places it names the color and
  /// closes it.
  /// </summary>
  public void LayOutAsColumn(float width)
  {
    _column = true;
    _columnWidth = width;
    // Its screen is made now, with the editor, not when it first opens.
    Mount(Model.SourceMode || _shape == Shape.None ? Shape.TwoColumns : Shape.Column);
  }

  public void SetDefault(Color color) => Model.SetDefault(color);

  public void Show(string title, Color value, bool opacity, Action<Color> changed)
  {
    Root.gameObject.SetActive(true);
    Mount(_column ? Shape.Column : Shape.Card);
    Model.Show(title, value, opacity, changed);
    Opened();
  }

  /// <summary>
  /// Overlay element colors: follow the highlight color, keeping only their own
  /// opacity, or a custom color with opacity. Laid out as a column, the color
  /// bar on top makes gradients; <paramref name="keepStop"/> stays on the
  /// picked stop when the same color shows again, as after undo. Without
  /// <paramref name="gradient"/> the color stays solid: laid out as a column,
  /// Highlight/Custom stand over the color's own controls, one column wide.
  /// </summary>
  public void ShowSource(
    string title,
    OverlayColorSettings value,
    Action<OverlayColorSettings> changed,
    bool keepStop = false,
    bool gradient = true
  )
  {
    Root.gameObject.SetActive(true);
    Mount(SourceShape(value, gradient));
    Model.ShowSource(title, value, changed, keepStop);
    Opened();
  }

  private Shape SourceShape(OverlayColorSettings value, bool gradient) =>
    !_column ? Shape.Card
    : _compactHeight > 0 && (value == null || !value.IsGradient) ? Shape.Compact
    : gradient ? Shape.TwoColumns
    : Shape.SourceColumn;

  private void Opened()
  {
    _stale = true;
    Fit();
    Root.SetAsLastSibling();
  }

  /// <summary>
  /// Takes the color again after the editor changed it (handles, the dock,
  /// undo), keeping the picked stop. The controls follow without reporting.
  /// </summary>
  public void Refresh(OverlayColorSettings value)
  {
    // A color its owner shows instead of the one being refreshed isn't touched.
    if (ShowsOther)
      return;
    if (!Visible)
      return;
    // Made a gradient, or back to one color: the place it needs changes.
    if (_compactHeight > 0 && _column && (_shape == Shape.Compact || _shape == Shape.TwoColumns))
      Mount(value != null && value.IsGradient ? Shape.TwoColumns : Shape.Compact);
    Model.Refresh(value);
  }

  /// <summary>Moves the opacity to <paramref name="alpha"/> without reporting a change.</summary>
  public void ShowOpacity(float alpha) => Model.ShowOpacity(alpha);

  public void Hide()
  {
    if (!Visible)
      return;
    Model.Closed();
    Root.gameObject.SetActive(false);
    InputGate.SetTextEditing(false);
    Hidden?.Invoke();
  }

  /// <summary>Applies what is typed; false, with the reason shown, while it can't be read.</summary>
  public bool Commit() => !Visible || Model.Commit();

  // The screen for a shape, made the first time it is needed and again when the shape changes.
  private void Mount(Shape shape)
  {
    if (_shape == shape)
      return;
    _shape = shape;
    // Texts measure only while shown.
    bool hidden = !Root.gameObject.activeSelf;
    if (hidden)
      Root.gameObject.SetActive(true);
    Model.GradientShown = shape == Shape.TwoColumns;
    View view =
      shape == Shape.Card ? new ColorPickerCardView(Model, _parts)
      : shape == Shape.SourceColumn ? new ColorPickerSourceColumnView(Model, _parts)
      : shape == Shape.Compact ? new ColorPickerCompactView(Model, _parts, _compactWidth, _compactHeight)
      : new ColorPickerColumnsView(Model, _parts, shape == Shape.TwoColumns, _columnWidth);
    _panel.Mount(NewBackend(), view, null, WidthOf(shape));
    _stale = true;
    Fit();
    if (hidden)
      Root.gameObject.SetActive(false);
  }

  // Fits the panel to what shows, after something that changes it.
  private void Fit()
  {
    if (!_stale || _panel.Host == null)
      return;
    _stale = false;
    _panel.Refit(WidthOf(_shape));
    _panel.Sync();
    Root.GetComponentsInChildren(true, _fields);
  }

  // Following the highlight there is no color to pick: one column, as wide as the keypad.
  private float TwoColumnsWidth => Model.Custom.Value ? ColorPickerColumnsView.TwoColumnWidth : _columnWidth;

  private float WidthOf(Shape shape) =>
    shape switch
    {
      Shape.Card => Width,
      Shape.TwoColumns => TwoColumnsWidth,
      Shape.SourceColumn => ColorPickerLayout.Column,
      Shape.Compact => _compactWidth,
      _ => _columnWidth,
    };

  private ElementBackend NewBackend() =>
    new(new ElementTheme(UiFonts.Choose(_font), _reduceMotion()) { Compact = true });

  public void Dispose()
  {
    Model.Closed();
    _panel.Dispose();
    Model.Dispose();
    _ui.Dispose();
  }
}
