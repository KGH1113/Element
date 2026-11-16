using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.ValueColors;
using Element.Domain.Overlay.Values;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The selection's color (or the whole text's) following a value: turning
/// it on with a first look that suits the value, picking the value from the
/// text's own or from the dock, and the band's stops, each with a value the
/// dock types and a color the picker opens on beside the column. Every change
/// is one undo step; a stop's color is one per picker session, a drag one per
/// press. Each item keeps its own color underneath.
/// </summary>
internal sealed class TextValueColor : IOverlayValueColorEditor
{
  /// <summary>The dock types the picked stop's value.</summary>
  public const string StopKey = "color-stop";

  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextColor _color;
  private readonly OverlayValues _values;
  private readonly OverlayValueColorRamp _ramp = new();
  private readonly OverlayResolvedGradient _swatch = new();
  private readonly List<OverlayValueId> _inText = new();
  private readonly List<OverlayValueDefinition> _offered = new();
  private readonly OverlayFormulaSlot _stopSlot;
  private OverlayValueColorModel _model;
  private OverlayValueSourceMenu _menu;
  private Func<Rect> _sourceRow;
  private int _selected;
  private bool _armed;
  private bool _added;
  private bool _dragRemembered;
  private bool _pickingStop;

  public TextValueColor(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextColor color,
    OverlayValues values
  )
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    _color = color;
    _values = values;
    _stopSlot = new OverlayFormulaSlot(StopKey, false, ReadStop, WriteStop);
  }

  /// <summary>The menu of values to follow; made by <see cref="Build"/>.</summary>
  public IPopover Menu => _menu;

  /// <summary>The dock picks the value to follow: its next clicked value goes here, not into the text.</summary>
  public bool Armed => _armed;

  /// <summary>The color's controls, drawing on <paramref name="under"/>.</summary>
  public OverlayValueColorModel BuildModel(Color under) => _model = new OverlayValueColorModel(this, under);

  /// <summary>The menu of values, opening under the value row <paramref name="sourceRow"/> says is where (frame units).</summary>
  public void Build(OverlayEditorFrame screen, Func<Rect> sourceRow)
  {
    _menu = new OverlayValueSourceMenu(screen);
    _sourceRow = sourceRow;
    _armed = false;
    _pickingStop = false;
  }

  /// <summary>The tool closed: what it had open goes with it.</summary>
  public void Closed()
  {
    Disarm();
    _menu?.Hide();
    if (_color.PicksOther)
      _color.Picker.Hide();
    _pickingStop = false;
    if (_formulas.Field == StopKey)
      _formulas.Close();
  }

  /// <summary>Whether the range's color follows a value: all of it, none, or null for some.</summary>
  public bool? Follows()
  {
    _text.Range(out int start, out int end);
    return start < end ? _text.Document.FollowsValue(start, end) : _text.RangeColor().FollowsValue;
  }

  /// <summary>The range's color that follows a value, as the first item that does has it; null for none.</summary>
  public OverlayValueColor Current()
  {
    _text.Range(out int start, out int end);
    if (start >= end)
      return _text.RangeColor().ValueColor;
    for (int i = start; i < end; i++)
      if (_text.Document[i].Color.FollowsValue)
        return _text.Document[i].Color.ValueColor;
    return null;
  }

  /// <summary>
  /// The number the preview shows value colors at instead of the value's
  /// own while the picker shows a stop's color, so the text wears the color
  /// being picked; NaN otherwise.
  /// </summary>
  public double PreviewNumber
  {
    get
    {
      OverlayValueColor color = _pickingStop && _color.PicksOther ? Current() : null;
      return color != null && _selected >= 0 && _selected < color.Stops.Count ? color.Stops[_selected].At : double.NaN;
    }
  }

  // ---- On and off ----

  /// <summary>
  /// The switch: turns following a value on for the range, starting from how
  /// part of it already follows one or else from a look that suits the
  /// range's first value, or off again.
  /// </summary>
  public void Toggle()
  {
    _host.CloseOpen();
    bool? follows = Follows();
    _text.Remember(false);
    if (follows == true)
    {
      if (_color.PicksOther)
        _color.Picker.Hide();
      Write(null);
      _host.Screen.Notice(L("색이 다시 값을 따라가지 않아요.", "The color no longer follows a value."), false);
      _host.Render();
      return;
    }
    OverlayValueColor made = Current()?.Clone();
    if (made == null)
    {
      OverlayValueDefinition first = FirstValueInRange();
      made = OverlayValueColorDefaults.For(
        first,
        first != null ? Number(first, out _) : double.NaN,
        _text.RangeColor()
      );
    }
    Write(made);
    _selected = Starting(made);
    if (OverlayValueColorEdits.Followed(made) == null)
    {
      // Nothing to follow in the range: the values to pick from open at once.
      _host.Render();
      PickSource();
      _host.Screen.Notice(
        L(
          "고른 부분에 값이 없어요. 색이 따라갈 값을 골라 주세요.",
          "The selection has no value. Pick the value the color follows."
        ),
        false
      );
      return;
    }
    _host.Screen.Notice(
      L(
        FollowedName(made) + "에 따라 색이 바뀌어요. 점을 눌러 값과 색을 고쳐요.",
        "The color follows " + FollowedName(made) + ". Click a stop to change its value or color."
      ),
      false
    );
    _host.Render();
  }

  // ---- Which value it follows ----

  public void PickSource()
  {
    if (_menu.Visible || _armed)
    {
      _host.CloseOpen();
      _host.Render();
      return;
    }
    _host.CloseOpen();
    _offered.Clear();
    _inText.Clear();
    _text.Document.ValuesIn(0, _text.Document.Count, _inText);
    foreach (OverlayValueId id in _inText)
    {
      OverlayValueDefinition value = OverlayValueCatalog.Get(id);
      if (value.IsNumeric && value.Available)
        _offered.Add(value);
    }
    Rect row = _sourceRow();
    _menu.Show(_offered, Current()?.Value ?? "", Follow, Arm, row.xMax, row.yMax + 6);
    _host.Render();
  }

  // The next value clicked in the dock is the one to follow.
  private void Arm()
  {
    _host.CloseOpen();
    _armed = true;
    _formulas.ArmPick(PickedInDock);
    _host.Screen.Notice(
      L("아래 값 목록에서 따라갈 값을 눌러 주세요.", "Click the value to follow in the values below."),
      false
    );
    _host.Render();
  }

  /// <summary>The dock goes back to adding values at the caret.</summary>
  public void Disarm()
  {
    if (!_armed)
      return;
    _armed = false;
    _formulas.Disarm();
  }

  private void PickedInDock(OverlayValueDefinition value)
  {
    if (!value.IsNumeric)
    {
      _host.Screen.Notice(
        L(
          ValueSectionNames.ValueName(value) + "은 글자라서 색을 정할 수 없어요. 숫자인 값을 골라 주세요.",
          ValueSectionNames.ValueName(value) + " is words, so it can't pick a color. Pick a value that is a number."
        )
      );
      return;
    }
    Follow(value);
  }

  private void Follow(OverlayValueDefinition value)
  {
    Disarm();
    _menu.Hide();
    OverlayValueColor current = Current();
    if (current != null && current.Value == value.Key)
    {
      _host.Render();
      return;
    }
    _text.Remember(false);
    OverlayValueColor made = OverlayValueColorEdits.Follow(current, value, Number(value, out _), _text.RangeColor());
    Write(made);
    _selected = Starting(made);
    _host.Screen.Notice(
      L(FollowedName(made) + "에 따라 색이 바뀌어요.", "The color follows " + FollowedName(made) + "."),
      false
    );
    _host.Render();
  }

  // ---- The band ----

  public void SelectStop(int index)
  {
    OverlayValueColor color = Current();
    if (color == null || index < 0 || index >= color.Stops.Count)
      return;
    bool picking = _pickingStop && _color.PicksOther;
    _host.CloseOpen();
    _selected = index;
    // The picker moves over to the newly picked stop.
    if (picking)
      OpenStopColor(color);
    _host.Render();
  }

  public int AddStop(float at)
  {
    OverlayValueColor color = Current();
    if (color == null)
      return -1;
    if (color.Stops.Count >= OverlayValueColor.MaximumStops)
    {
      _host.Screen.Notice(
        L(
          "점은 " + OverlayValueColor.MaximumStops + "개까지 둘 수 있어요.",
          "A color holds up to " + OverlayValueColor.MaximumStops + " stops."
        )
      );
      return -1;
    }
    bool picking = _pickingStop && _color.PicksOther;
    _host.CloseOpen();
    _text.Remember(false);
    OverlayValueColor changed = color.Clone();
    int added = OverlayValueColorEdits.AddStop(changed, at, Accent);
    Write(changed);
    _selected = added;
    _added = true;
    if (picking)
      OpenStopColor(changed);
    _host.Render();
    return added;
  }

  public void BeginDrag(int index)
  {
    // A stop added by this press is already one undo step with its drag.
    _dragRemembered = _added;
    _added = false;
  }

  public void DragStop(int index, float at)
  {
    OverlayValueColor color = Current();
    if (color == null || index < 0 || index >= color.Stops.Count || color.Stops[index].At == at)
      return;
    if (!_dragRemembered)
    {
      _text.Remember(false);
      _dragRemembered = true;
    }
    OverlayValueColor changed = color.Clone();
    changed.Stops[index].At = at;
    Write(changed);
    _host.Render();
  }

  public void EndDrag()
  {
    _dragRemembered = false;
    _added = false;
    _host.Render();
  }

  public void RemoveStop()
  {
    OverlayValueColor color = Current();
    if (color == null || _selected < 0 || _selected >= color.Stops.Count)
      return;
    bool picking = _pickingStop && _color.PicksOther;
    _host.CloseOpen();
    OverlayValueColor changed = color.Clone();
    float at = changed.Stops[_selected].At;
    if (!OverlayValueColorEdits.RemoveStop(changed, _selected))
      return;
    _text.Remember(false);
    Write(changed);
    _selected = Nearest(changed, at);
    if (picking)
      OpenStopColor(changed);
    _host.Render();
  }

  public void SetMode(OverlayValueColorMode mode)
  {
    OverlayValueColor color = Current();
    if (color == null || color.Mode == mode)
      return;
    _text.Remember(false);
    OverlayValueColor changed = color.Clone();
    changed.Mode = mode;
    Write(changed);
    _host.Render();
  }

  // ---- The picked stop ----

  public void EditStopValue() => _formulas.Activate(StopKey);

  /// <summary>How the dock reads and writes the picked stop's value; null unless <paramref name="key"/> is it.</summary>
  public OverlayFormulaSlot Slot(string key) => key == StopKey ? _stopSlot : null;

  /// <summary>The dock's name for the picked stop's value; null for another key.</summary>
  public static string Name(string key) => key == StopKey ? L("점의 값", "Stop value") : null;

  private OverlayFormula ReadStop()
  {
    OverlayValueColor color = Current();
    return color != null && _selected >= 0 && _selected < color.Stops.Count
      ? OverlayFormula.FromNumber(Math.Round(color.Stops[_selected].At, 4))
      : new OverlayFormula();
  }

  // A calculation, such as a value dropped in, gives the stop its result now.
  private void WriteStop(OverlayFormula formula)
  {
    OverlayValueColor color = Current();
    double number = _formulas.Result(formula);
    if (
      color == null
      || _selected < 0
      || _selected >= color.Stops.Count
      || double.IsNaN(number)
      || double.IsInfinity(number)
    )
      return;
    _text.RememberFormula(StopKey);
    OverlayValueColor changed = color.Clone();
    changed.Stops[_selected].At = (float)number;
    Write(changed);
  }

  public void PickStopColor()
  {
    OverlayValueColor color = Current();
    if (color == null || _selected < 0 || _selected >= color.Stops.Count)
      return;
    if (_pickingStop && _color.PicksOther)
    {
      _color.Picker.Hide();
      _host.Render();
      return;
    }
    _host.CloseOpen();
    OpenStopColor(color);
    _host.Render();
  }

  // The picker beside the column on the picked stop's color.
  private void OpenStopColor(OverlayValueColor color)
  {
    OverlayValueColorStop stop = color.Stops[_selected];
    OverlayColorSettings shown = stop.Highlight
      ? OverlayColorSettings.HighlightColor(stop.Alpha)
      : OverlayColorSettings.Custom(stop.Hex, stop.Alpha);
    _pickingStop = true;
    int index = _selected;
    _color.PickOther(
      L(_model.Format(stop.At) + "일 때 색", "Color at " + _model.Format(stop.At)),
      shown,
      picked => SetStopColor(index, picked)
    );
  }

  private void SetStopColor(int index, OverlayColorSettings picked)
  {
    OverlayValueColor color = Current();
    if (color == null || index < 0 || index >= color.Stops.Count)
      return;
    OverlayValueColor changed = color.Clone();
    OverlayValueColorStop stop = changed.Stops[index];
    stop.Highlight = picked.Highlight;
    if (!picked.Highlight)
      stop.Hex = picked.Hex;
    stop.Alpha = Mathf.Clamp01(picked.Alpha);
    Write(changed);
  }

  // ---- Showing it ----

  /// <summary>The tool's color switch leads with the band, or the range's own color while it follows nothing.</summary>
  public void ShowSwatch(OverlayBarButtonModel row)
  {
    OverlayValueColor color = Current();
    OverlayColorSettings own = _text.RangeColor();
    _ramp.Load(color);
    bool band = color != null && OverlayValueColorSwatch.Fill(_swatch, _ramp, UiTheme.Accent);
    row.SetSwatch(OverlayTextComposer.Resolve(own, UiTheme.Accent), band ? _swatch : null);
  }

  /// <summary>Shows the range's color that follows a value in the tool's controls.</summary>
  public void Show()
  {
    OverlayValueColor color = Current();
    if (color == null || _model == null)
      return;
    int count = color.Stops.Count;
    _selected = count == 0 ? -1 : Mathf.Clamp(_selected, 0, count - 1);
    OverlayValueDefinition followed = OverlayValueColorEdits.Followed(color);
    double live = double.NaN;
    bool sample = false,
      time = false;
    OverlayFormula follows = OverlayFormulaText.Compile(color.Value);
    if (follows != null)
    {
      if (follows.TryEvaluate(_values, out double number, out time))
        live = number;
      else if (follows.TryEvaluate(_values.WithSamples, out number, out time))
      {
        live = number;
        sample = true;
      }
    }
    string source =
      followed != null ? ValueSectionNames.ValueName(followed)
      : _armed ? L("아래에서 골라 주세요", "Pick one below")
      : L("고르기", "Pick");
    _model.Show(
      color,
      _selected,
      live,
      sample,
      time,
      source,
      followed == null,
      _menu.Visible || _armed,
      _formulas.Field == StopKey,
      _pickingStop && _color.PicksOther
    );
  }

  /// <summary>What the dock's line says while the tool is open; null otherwise.</summary>
  public string Hint(bool open)
  {
    if (_armed)
      return L(
        "따라갈 값을 아래에서 눌러 주세요 · Esc를 누르면 그만둬요",
        "Click the value to follow below · Esc to stop"
      );
    if (!open || Follows() == false)
      return null;
    if (_pickingStop && _color.PicksOther)
      return L(
        "고른 점의 색을 정하는 중이에요 · 글자가 그 색으로 보여요",
        "Picking the stop's color · The text shows it meanwhile"
      );
    return L(
      "점을 누르면 그 점의 값과 색을 고쳐요 · 띠의 빈 곳을 누르면 점이 생겨요 · Alt를 누른 채 끌면 자유롭게",
      "Click a stop to change its value or color · Click the band to add one · Hold Alt to drag freely"
    );
  }

  // ---- Helpers ----

  private static OverlayRgba Accent
  {
    get
    {
      Color accent = UiTheme.Accent;
      return new OverlayRgba(accent.r, accent.g, accent.b, 1);
    }
  }

  // Into the range, each item keeping its own color; into what typing takes in an empty text.
  private void Write(OverlayValueColor color)
  {
    _text.Range(out int start, out int end);
    if (start < end)
    {
      _text.Document.SetValueColor(start, end, color);
      return;
    }
    OverlayColorSettings pending = _text.RangeColor().Clone();
    pending.ValueColor = color?.Clone();
    _text.PendingColor = pending;
  }

  // The first number the range shows; null when it shows none.
  private OverlayValueDefinition FirstValueInRange()
  {
    _text.Range(out int start, out int end);
    _inText.Clear();
    _text.Document.ValuesIn(start, end, _inText);
    foreach (OverlayValueId id in _inText)
      if (OverlayValueCatalog.Get(id).IsNumeric)
        return OverlayValueCatalog.Get(id);
    return null;
  }

  // The value's number now, or its example; NaN with neither.
  private double Number(OverlayValueDefinition value, out bool sample)
  {
    sample = false;
    if (_values.TryGetNumber(value.Id, out double number))
      return number;
    sample = true;
    return _values.WithSamples.TryGetNumber(value.Id, out number) ? number : double.NaN;
  }

  // The stop the value has reached now is picked first; the highest without a number.
  private int Starting(OverlayValueColor color)
  {
    _ramp.Load(color);
    if (_ramp.Count == 0)
      return 0;
    OverlayValueDefinition followed = OverlayValueColorEdits.Followed(color);
    double now = followed != null ? Number(followed, out _) : double.NaN;
    return _ramp.Source(double.IsNaN(now) ? _ramp.Count - 1 : _ramp.Reached(now));
  }

  private static int Nearest(OverlayValueColor color, float at)
  {
    int nearest = 0;
    for (int i = 1; i < color.Stops.Count; i++)
      if (Math.Abs(color.Stops[i].At - at) < Math.Abs(color.Stops[nearest].At - at))
        nearest = i;
    return nearest;
  }

  private static string FollowedName(OverlayValueColor color)
  {
    OverlayValueDefinition followed = OverlayValueColorEdits.Followed(color);
    return followed != null ? ValueSectionNames.ValueName(followed) : "";
  }
}
