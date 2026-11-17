using System;
using System.Collections.Generic;
using System.Text;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;
using Row = Element.Presentation.OverlayEditor.Parts.Inspector.InspectorRowStyle;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// A mod's settings drawn as the property column's rows, whichever way the
/// mod wrote them: a card or a section is a heading over its rows, a toggle
/// a row with a switch, a slider a row with a short track and its number, a
/// stepper − n +, a field the field on the right, a picker its choices side
/// by side when they fit or the choice with a chevron that opens the list,
/// and several choices what is on, opening a switch for each. Rows keep the
/// column's 6-unit rhythm, and a control that is off reads muted.
/// </summary>
internal sealed class ModColumnStyle : ControlStyle
{
  private const float ControlHeight = 30,
    NumberWidth = 72,
    SliderMinWidth = 64,
    SliderWidth = 120,
    NumberRoom = 40,
    StepSide = 26,
    SegmentPadding = 28;

  private readonly ModEditorColumn _column;
  private readonly Func<string, float> _measure;

  /// <param name="column">Where the rows stand; their tools open there.</param>
  /// <param name="measure">How wide words are at the rows' size, to see whether choices fit side by side.</param>
  public ModColumnStyle(ModEditorColumn column, Func<string, float> measure)
  {
    _column = column;
    _measure = measure;
  }

  private ModCommandsViewModel Commands => _column.Commands;

  // Inside the column, a stack is never airier than its rows.
  public override float StackSpacing(Axis axis, float spacing) =>
    axis == Axis.Vertical ? Mathf.Min(spacing, Row.Gap) : spacing;

  public override View Card(CardConfiguration card)
  {
    View content = string.IsNullOrEmpty(card.Title)
      ? card.Content
      : VStack(spacing: Row.Gap, Row.Heading(card.Title), card.Content);
    // A group ends with room before the next one's heading.
    return content
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .Padding(new Insets(0, 0, 14, 0));
  }

  public override View Section(SectionConfiguration section) =>
    Row.Heading(section.Title).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading);

  public override View Toggle(ToggleConfiguration toggle)
  {
    Binding<bool> value = toggle.Value;
    Command flip = Commands.Make(() => value.Value = !value.Value);
    return Button(
        Row.Of(null, Name(toggle), true, new SwitchIndicator(() => (bool?)value.Value, () => toggle.IsEnabled)),
        () => flip,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(Row.Fill, Row.Radius))
      .Named((toggle.Appearance == ToggleAppearance.Checkbox ? "Checkbox " : "Switch ") + toggle.Label);
  }

  // Tracks are one length, so they line up down the column; one beside a long name is shorter.
  public override View Slider(SliderConfiguration slider)
  {
    Binding<float> value = slider.Value;
    View track = slider
      .Track()
      .Named("Slider " + slider.Label)
      .Frame(width: TrackWidth(slider.Label, slider.ShowsValue ? NumberRoom : 0));
    return Plate(
      slider.ShowsValue
        ? Row.Of(null, Name(slider), false, track, Readout(value, slider.Step, slider))
        : Row.Of(null, Name(slider), false, track)
    );
  }

  public override View RangeSlider(RangeSliderConfiguration range)
  {
    View track = range
      .Track()
      .Named("Range slider " + range.Label)
      .Frame(width: TrackWidth(range.Label, NumberRoom * 2));
    View numbers = HStack(
        spacing: 3,
        Number(range.Low, range.Step, range),
        UI.Text("–").Font(Font.Caption.Sized(13)).Foreground(Colors.Muted),
        Number(range.High, range.Step, range)
      )
      .Padding(new Insets(0, 10, 0, 0));
    return Plate(Row.Of(null, Name(range), false, track, numbers));
  }

  public override View Stepper(StepperConfiguration stepper)
  {
    Binding<int> value = stepper.Value;
    int min = stepper.Min,
      max = Mathf.Max(stepper.Min, stepper.Max),
      step = Mathf.Max(1, stepper.Step);
    Command down = Commands.Make(() => value.Value = Mathf.Max(min, value.Value - step), () => value.Value > min);
    Command up = Commands.Make(() => value.Value = Mathf.Min(max, value.Value + step), () => value.Value < max);
    View controls = HStack(
      spacing: 2,
      StepButton(UiIconKind.Minus, "Step down " + stepper.Label, down),
      UI.Text(() => value.Value, format: "{0}")
        .Font(Font.Caption.Sized(13))
        .Foreground(() => Ink(stepper.IsEnabled))
        .TextAlignment(TextAlign.Center)
        .Frame(minWidth: 28),
      StepButton(UiIconKind.Plus, "Step up " + stepper.Label, up)
    );
    return Plate(Row.Of(null, Name(stepper), false, controls));
  }

  public override View TextField(TextFieldConfiguration field)
  {
    View input = field
      .Field()
      .Named((field.IsNumber ? "Number " : "Text ") + field.Label)
      .Font(Font.Caption.Sized(13))
      .Frame(height: ControlHeight);
    return Plate(
      field.IsNumber
        ? Row.Of(null, Name(field), false, input.Frame(width: NumberWidth))
        : Row.Filled(null, Name(field), input.Frame(minWidth: 96, maxWidth: float.PositiveInfinity))
    );
  }

  public override View Picker(PickerConfiguration picker)
  {
    string[] options = Copy(picker.Options);
    Binding<int> selected = picker.Selected;
    float segments = SegmentsWidth(options);
    if (picker.Appearance == PickerAppearance.Segmented && segments <= RoomBeside(picker.Label))
      return Plate(
        Row.Of(
          null,
          Name(picker),
          false,
          picker
            .Segments()
            .Font(Font.Caption.Sized(13))
            .Named("Picker " + picker.Label)
            .Frame(width: segments, height: ControlHeight)
        )
      );
    return Opener(
      picker,
      "Choice " + picker.Label,
      Row.Value(() => OptionAt(options, selected.Value), () => Ink(picker.IsEnabled)),
      commands => ChoiceList(options, selected, commands)
    );
  }

  public override View MultiPicker(MultiPickerConfiguration picker)
  {
    string[] options = Copy(picker.Options);
    Binding<List<bool>> on = picker.On;
    // What is on, in the room the row has right of its name and before its chevron.
    float[] widths = new float[options.Length];
    for (int i = 0; i < widths.Length; i++)
      widths[i] = _measure(options[i]);
    SummaryRoom room = new(RoomBeside(picker.Label) - 28, _measure("·"), _measure(" +9"));
    return Opener(
      picker,
      "Choices " + picker.Label,
      Row.Value(() => Summary(options, widths, room, on.Value), () => Ink(picker.IsEnabled)),
      commands => Switches(options, picker, commands)
    );
  }

  /// <summary>
  /// A row that opens <paramref name="tool"/> in the column, named after the
  /// control: <paramref name="value"/> and a chevron on its right.
  /// </summary>
  private View Opener(ControlConfiguration control, string name, View value, Func<ModCommandsViewModel, View> tool)
  {
    string title = control.Label;
    Command open = Commands.Make(() => _column.Open(title, tool, height: ToolHeight.Scroll));
    return Button(Row.Of(null, Name(control), false, value, Row.Chevron()), () => open, ButtonStyle.Plain)
      .ButtonStyle(new ButtonSurfaceStyle(Row.Fill, Row.Radius))
      .Named(name);
  }

  // One row per option; the picked one is ticked. A click picks it and the list stays, to try another.
  private static View ChoiceList(string[] options, Binding<int> selected, ModCommandsViewModel commands)
  {
    View[] rows = new View[options.Length];
    UiColor tick = UiPalette.SoftAccent;
    for (int i = 0; i < rows.Length; i++)
    {
      int index = i;
      Command pick = commands.Make(() => selected.Value = index);
      rows[i] = Button(
          Row.Of(
            null,
            Row.Name(options[i], () => Colors.Foreground),
            false,
            Image(ElementIcons.Of(UiIconKind.Check), 16).Foreground(tick).Hidden(() => selected.Value != index)
          ),
          () => pick,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(Row.Fill, Row.Radius))
        .Named("Option " + options[i]);
    }
    return VStack(spacing: Row.Gap, HAlignment.Leading, rows);
  }

  // A switch for each option.
  private static View Switches(string[] options, MultiPickerConfiguration picker, ModCommandsViewModel commands)
  {
    Binding<List<bool>> on = picker.On;
    View[] rows = new View[options.Length];
    for (int i = 0; i < rows.Length; i++)
    {
      int index = i;
      Command flip = commands.Make(() => picker.Set(index, !MultiPickerConfiguration.IsOn(on.Value, index)));
      rows[i] = Button(
          Row.Of(
            null,
            Row.Name(options[i], () => Colors.Foreground),
            true,
            new SwitchIndicator(() => (bool?)MultiPickerConfiguration.IsOn(on.Value, index), () => true)
          ),
          () => flip,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(Row.Fill, Row.Radius))
        .Named("Option " + options[i]);
    }
    return VStack(spacing: Row.Gap, HAlignment.Leading, rows);
  }

  // A small round − or +; faded at its limit.
  private static View StepButton(UiIconKind icon, string name, Command step) =>
    Button(Image(ElementIcons.Of(icon), 16).Foreground(() => StepInk(step.CanExecute)), () => step, ButtonStyle.Plain)
      .ButtonStyle(new ButtonSurfaceStyle(UiPalette.Wash(UiPalette.EditorControl, .06f), StepSide / 2))
      .Frame(width: StepSide, height: StepSide)
      .Named(name);

  // A row whose controls take the clicks themselves: the fill alone.
  private static View Plate(View row) => row.Background(Row.Fill, Row.Radius);

  private static View Name(ControlConfiguration control) => Row.Name(control.Label, () => Ink(control.IsEnabled));

  // The number beside a track, at least as wide as two digits, a little away from it.
  private static View Readout(Binding<float> value, float step, ControlConfiguration control) =>
    Number(value, step, control)
      .TextAlignment(TextAlign.Trailing)
      .Frame(minWidth: 30, alignment: Alignment.Trailing)
      .Padding(new Insets(0, 10, 0, 0));

  // A number in the row's words: whole for whole steps, else to two places.
  private static View Number(Binding<float> value, float step, ControlConfiguration control) =>
    (
      step >= 1
        ? UI.Text(() => Mathf.RoundToInt(value.Value), format: "{0}")
        : UI.Text(() => value.Value, format: "{0}", number: "0.##")
    )
      .Font(Font.Caption.Sized(13))
      .Foreground(() => Ink(control.IsEnabled))
      .LineLimit(1);

  // A track's length: the column's own unless the name beside it needs the room.
  private float TrackWidth(string label, float numbers) =>
    Mathf.Clamp(RoomBeside(label) - numbers, SliderMinWidth, SliderWidth);

  // The room a row leaves right of its name.
  private float RoomBeside(string label) => _column.Area.width - 24 - Row.LeadWidth - 8 - _measure(label ?? "") - 12;

  private float SegmentsWidth(string[] options)
  {
    if (options.Length < 2 || options.Length > 3)
      return float.PositiveInfinity;
    float widest = 0;
    foreach (string option in options)
      widest = Mathf.Max(widest, _measure(option));
    // Segments are as wide as each other.
    return Mathf.Ceil((widest + SegmentPadding) * options.Length + 4);
  }

  private static string[] Copy(IReadOnlyList<string> options)
  {
    string[] copy = new string[options.Count];
    for (int i = 0; i < copy.Length; i++)
      copy[i] = options[i] ?? "";
    return copy;
  }

  [ElementUI.Pure]
  private static UiColor Ink(bool enabled) => enabled ? Colors.Foreground : Colors.Muted;

  [ElementUI.Pure]
  private static UiColor StepInk(bool can) => can ? Colors.Foreground : (UiColor)UiPalette.Disabled;

  [ElementUI.Pure]
  private static string OptionAt(string[] options, int index) =>
    index >= 0 && index < options.Length ? options[index] : "";

  /// <summary>
  /// What is on, such as "Rank·Name·Score"; when they don't all fit, the
  /// first ones and how many more, "Rank·Name +2"; "None" when nothing is.
  /// </summary>
  [ElementUI.Pure]
  internal static string Summary(string[] options, float[] widths, SummaryRoom room, List<bool> on)
  {
    int count = 0;
    float all = 0;
    for (int i = 0; i < options.Length; i++)
      if (MultiPickerConfiguration.IsOn(on, i))
      {
        all += (count > 0 ? room.Separator : 0) + widths[i];
        count++;
      }
    if (count == 0)
      return L("없음", "None");
    StringBuilder text = new();
    float used = 0;
    int shown = 0;
    for (int i = 0; i < options.Length; i++)
    {
      if (!MultiPickerConfiguration.IsOn(on, i))
        continue;
      float next = used + (shown > 0 ? room.Separator : 0) + widths[i];
      // Each one while all fit; otherwise while there is still room to say how many more.
      if (all > room.Width && shown > 0 && next + room.More > room.Width)
        break;
      if (shown > 0)
        text.Append('·');
      text.Append(options[i]);
      used = next;
      shown++;
    }
    if (shown < count)
      text.Append(" +").Append(count - shown);
    return text.ToString();
  }

  /// <summary>How much a several-choices row's summary may take, and what its separators and "+n" take.</summary>
  internal readonly struct SummaryRoom
  {
    public readonly float Width,
      Separator,
      More;

    public SummaryRoom(float width, float separator, float more)
    {
      Width = width;
      Separator = separator;
      More = more;
    }
  }
}
