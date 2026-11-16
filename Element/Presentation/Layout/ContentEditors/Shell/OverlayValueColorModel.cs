using System.Text;
using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// What the controls of a color that follows a value show: which value it
/// follows, the band with its stops and where the value is now, the picked
/// stop's value and color, and how it changes between stops. The editor
/// whose color it is shows it with <see cref="Show"/> after every change;
/// <see cref="OverlayValueColorView"/> draws it.
/// </summary>
internal sealed class OverlayValueColorModel : ViewModel
{
  private readonly StringBuilder _builder = new(24);
  private readonly OverlayValueColor _shown = new();
  private readonly UiColor _rest,
    _lit;
  private int _shownSelected = -1;
  private double _shownLive = double.NaN;
  private bool _shownSample;

  /// <param name="under">What the controls sit on; their fills are a little lighter than it.</param>
  public OverlayValueColorModel(IOverlayValueColorEditor editor, Color under)
  {
    Editor = editor;
    Color rest = UiPalette.Wash(under, .07f);
    _rest = rest;
    _lit = Color.Lerp(rest, UiPalette.SoftAccent, .3f);
    SourceFill = new State<UiColor>(_rest);
    StopValueFill = new State<UiColor>(_rest);
    StopColorFill = new State<UiColor>(_rest);
    Surface = rest;
    PickSource = Command(editor.PickSource);
    EditValue = Command(editor.EditStopValue);
    PickColor = Command(editor.PickStopColor);
    Remove = Command(editor.RemoveStop, canExecute: Computed(() => CanRemove.Value));
  }

  /// <summary>Who makes the changes; the band hands its gestures straight to it.</summary>
  public IOverlayValueColorEditor Editor { get; }

  /// <summary>The controls' opaque fill at rest, which the band draws under its colors.</summary>
  public Color Surface { get; }

  // ---- The band ----

  /// <summary>The color as the band shows it; the editor's own copy, not to be changed here.</summary>
  public OverlayValueColor Current { get; private set; } = new();

  /// <summary>The picked stop's index in <see cref="Current"/>'s stops.</summary>
  public int Selected { get; private set; }

  /// <summary>The followed value's number now; NaN while there is none.</summary>
  public double Live { get; private set; } = double.NaN;

  /// <summary>The number now is the editor's example, the game having none yet.</summary>
  public bool LiveIsSample { get; private set; }

  /// <summary>The followed value is a time, shown as m:ss.</summary>
  public bool IsTime { get; private set; }

  /// <summary>Goes up each time what the band draws changes.</summary>
  public readonly State<int> Revision = new(0);

  // ---- The rows ----

  /// <summary>The followed value's name, or what to do while there is none.</summary>
  public readonly State<string> SourceName = new("");

  public readonly State<UiColor> SourceInk = new(Colors.Foreground);

  /// <summary>Lit while the values to follow are offered.</summary>
  public readonly State<UiColor> SourceFill;

  /// <summary>The picked stop's value.</summary>
  public readonly State<string> StopValue = new("");

  /// <summary>Lit while the dock types the picked stop's value.</summary>
  public readonly State<UiColor> StopValueFill;

  public readonly State<Color> StopColor = new(Color.white);

  /// <summary>A stop's swatch shows one color, never a gradient.</summary>
  public readonly State<OverlayResolvedGradient> NoGradient = new(null);

  /// <summary>The picked stop's color in a word: Highlight, or its HEX.</summary>
  public readonly State<string> StopColorName = new("");

  /// <summary>Lit while the color picker shows the picked stop's color.</summary>
  public readonly State<UiColor> StopColorFill;

  /// <summary>More than two stops: the picked one can go.</summary>
  public readonly State<bool> CanRemove = new(false);

  public readonly State<UiColor> RemoveInk = new(Colors.Foreground);

  /// <summary>0 smooth, 1 in steps.</summary>
  public readonly State<int> ModeIndex = new(0);

  /// <summary>Smooth (0) or in steps (1), as the segmented control picks it.</summary>
  public int Mode
  {
    [Pure]
    get => ModeIndex.Value;
    set => Editor.SetMode(value == 1 ? OverlayValueColorMode.Step : OverlayValueColorMode.Smooth);
  }

  public Command PickSource { get; }

  public Command EditValue { get; }

  public Command PickColor { get; }

  public Command Remove { get; }

  /// <summary>
  /// Shows <paramref name="color"/> with stop <paramref name="selected"/>
  /// picked, the followed value's number <paramref name="live"/> (NaN for
  /// none), and what each row says and whether it is lit.
  /// </summary>
  public void Show(
    OverlayValueColor color,
    int selected,
    double live,
    bool sample,
    bool time,
    string source,
    bool sourceMissing,
    bool sourceOpen,
    bool valueOpen,
    bool colorOpen
  )
  {
    color ??= new OverlayValueColor();
    Current = color;
    int count = color.Stops.Count;
    Selected = count == 0 ? -1 : Mathf.Clamp(selected, 0, count - 1);
    Live = live;
    LiveIsSample = sample;
    IsTime = time;
    bool changed =
      !_shown.SameAs(color)
      || _shownSelected != Selected
      || _shownSample != sample
      || !(_shownLive.Equals(live) || double.IsNaN(_shownLive) && double.IsNaN(live));
    if (changed)
    {
      CopyInto(color, _shown);
      _shownSelected = Selected;
      _shownLive = live;
      _shownSample = sample;
      Revision.Value++;
    }
    SourceName.Value = source;
    SourceInk.Value = sourceMissing ? UiPalette.SoftAccent : Colors.Foreground;
    SourceFill.Value = sourceOpen ? _lit : _rest;
    ModeIndex.Value = color.Mode == OverlayValueColorMode.Step ? 1 : 0;
    CanRemove.Value = count > OverlayValueColor.MinimumStops;
    RemoveInk.Value = CanRemove.Value ? Colors.Foreground : Colors.Muted;
    StopValueFill.Value = valueOpen ? _lit : _rest;
    StopColorFill.Value = colorOpen ? _lit : _rest;
    if (Selected < 0)
    {
      StopValue.Value = "";
      StopColorName.Value = "";
      return;
    }
    OverlayValueColorStop stop = color.Stops[Selected];
    StopValue.Value = Format(stop.At);
    Color shown = stop.Highlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    shown.a = 1;
    StopColor.Value = shown;
    StopColorName.Value = stop.Highlight
      ? UiLocale.L("하이라이트", "Highlight")
      : (string.IsNullOrEmpty(stop.Hex) ? "#FFFFFF" : stop.Hex.ToUpperInvariant());
  }

  /// <summary>A number as the followed value shows it: up to two decimals, or m:ss for a time.</summary>
  public string Format(double value)
  {
    _builder.Clear();
    OverlayFormulaText.AppendResult(_builder, value, IsTime);
    return _builder.ToString();
  }

  // What the band last drew, kept to tell a change apart.
  private static void CopyInto(OverlayValueColor from, OverlayValueColor to)
  {
    to.Value = from.Value;
    to.Mode = from.Mode;
    to.Stops.Clear();
    foreach (OverlayValueColorStop stop in from.Stops)
      to.Stops.Add(stop.Clone());
  }
}
