using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// The dock's values, as tall as the input method beside them: only those
/// that fit where values go now. Under a mark of the type and words that
/// name the place ("숫자 칸에 넣을 수 있는 값"), one section per built-in
/// category and per mod, each a small heading over its chips, a mod's in a
/// row per group; a section with nothing that fits is left out. After them,
/// in a muted ink, how many values were left out. Each chip leads with its
/// type's mark, names its value and, in a muted ink, says what it reads now.
/// With nothing open, or a sign to pick, a line says what to do instead.
/// Only the sections scroll, a slim bar and a fading edge saying more lies
/// beyond.
/// </summary>
internal sealed class ValueBrowserView : View<ValueBrowserViewModel>
{
  public const float TitleHeight = KeypadView.TitleHeight,
    ListHeight = KeypadView.Height - TitleHeight,
    ChipHeight = 30,
    ChipGap = 6,
    SectionGap = 10,
    HeadingHeight = 18,
    HeadingGap = 5,
    GroupGap = 6,
    LiveWidth = 150,
    NoteHeight = 22;

  // A value is the same pill in the list as on the line.
  private static readonly UiColor ChipFill = FieldLook.Chip,
    ChipInk = new Color(.92f, .92f, .92f),
    LiveInk = UiPalette.Muted;

  public ValueBrowserView(ValueBrowserViewModel vm)
    : base(vm) { }

  public override View Body =>
    If(() => VM.Listing.Value, Listed, Resting)
      .Frame(maxWidth: float.PositiveInfinity, height: KeypadView.Height, alignment: Alignment.TopLeading)
      // A few times a second while shown, the chips take what their values read now.
      .OnChange(() => VM.Live.Tick, _ => VM.Follow())
      // Only when where values go changes, the list keeps what fits there.
      .OnChange(() => VM.Place.Value, VM.Refit);

  // Nothing to list here: what to do instead, where the title would be.
  private View Resting() =>
    UI.Text(() => VM.Rest.Value)
      .Font(Font.Caption.Sized(13))
      .Foreground(Colors.Muted)
      .LineLimit(2)
      .Named("Values rest")
      .Padding(new Insets(4, 2, 0, 0))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);

  private View Listed() =>
    VStack(
      spacing: 0,
      // The type that goes there, then where that is.
      HStack(
          spacing: 8,
          Switch(() => VM.Type.Value, type => TypeMarks.Mark(type).Named("Values type")),
          UI.Text(() => VM.Title.Value)
            .Font(Font.Caption.Sized(14).Bolded())
            .Foreground(Colors.Foreground)
            .LineLimit(1)
            .Named("Values title"),
          Spacer()
        )
        .Padding(new Insets(0, 2, 0, 0))
        .Frame(maxWidth: float.PositiveInfinity, height: TitleHeight, alignment: Alignment.TopLeading),
      Scroller(VStack(spacing: SectionGap, HAlignment.Leading, Sections()).Padding(new Insets(0, 0, 8, 0)))
        // A slim bar and a fading edge say more values lie beyond what shows.
        .Configure<ScrollRect>(scroll => UiScrollCues.Add(scroll, UiPalette.EditorDim, 0))
        .Named("Values")
        .Frame(maxWidth: float.PositiveInfinity, height: ListHeight)
    );

  // Every section is made once; one with no value that fits is left out. Last, how many were left out.
  private View[] Sections()
  {
    List<View> sections = new();
    foreach (ValueFolderViewModel folder in VM.Sections)
      sections.Add(If(() => folder.Shown.Value, () => Section(folder)));
    sections.Add(
      UI.Text(() => VM.Hidden.Value)
        .Font(Font.Caption.Sized(12))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Named("Values hidden")
        .Padding(new Insets(0, 2, 0, 0))
        .Frame(maxWidth: float.PositiveInfinity, height: NoteHeight, alignment: Alignment.BottomLeading)
    );
    return sections.ToArray();
  }

  // A heading over its chips; "My values" is made again when its values change.
  private View Section(ValueFolderViewModel folder) =>
    VStack(
        spacing: HeadingGap,
        HAlignment.Leading,
        Heading(folder.Name).Named("Section " + folder.Key),
        Switch(() => folder.Version.Value, _ => Rows(folder.Groups))
      )
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);

  private View Rows(IReadOnlyList<ValueChipRow> groups)
  {
    if (groups.Count == 1 && groups[0].Heading == null)
      return Chips(groups[0].Chips);
    View[] rows = new View[groups.Count];
    for (int i = 0; i < rows.Length; i++)
    {
      ValueChipRow group = groups[i];
      // A group none of whose values fit is left out, heading and all.
      rows[i] = If(
        () => group.Shown.Value,
        () => VStack(spacing: 3, HAlignment.Leading, Heading(group.Heading ?? ""), Chips(group.Chips))
      );
    }
    return VStack(spacing: GroupGap, HAlignment.Leading, rows);
  }

  private static View Heading(string words) =>
    UI.Text(words)
      .Font(Font.Caption.Sized(12))
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .Padding(new Insets(0, 2, 0, 0))
      .Frame(height: HeadingHeight, alignment: Alignment.BottomLeading);

  private View Chips(IReadOnlyList<ValueChipViewModel> chips)
  {
    View[] views = new View[chips.Count];
    for (int i = 0; i < views.Length; i++)
    {
      ValueChipViewModel chip = chips[i];
      // A value that can't go where values go now is not listed at all.
      views[i] = If(() => chip.Fits.Value, () => Chip(chip));
    }
    return Flow(ChipGap, views).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
  }

  // A chip is pressed to pick its value; picking is the only way a value
  // goes in. The label takes the chip's height, so the capsule and its press
  // are as tall as the chip. What the value reads now follows its name,
  // keeping the room its widest number took.
  private View Chip(ValueChipViewModel chip) =>
    Button(
        HStack(
            spacing: 0,
            Lead(chip.Value),
            UI.Text(chip.Label).Font(Font.Caption.Sized(13)).Foreground(ChipInk).LineLimit(1).Named("Value name"),
            If(
              () => chip.HasLive.Value,
              () =>
                ZStack(
                    Alignment.Leading,
                    UI.Text(() => chip.Room.Value).Hidden(),
                    UI.Text(() => chip.Live.Value).Named("Value now")
                  )
                  .Font(Font.Caption.Sized(13))
                  .Foreground(LiveInk)
                  .LineLimit(1)
                  .Frame(maxWidth: LiveWidth, alignment: Alignment.Leading)
                  .Padding(new Insets(0, 7, 0, 0))
            )
          )
          .Padding(new Insets(0, 7, 0, 12))
          .Frame(height: ChipHeight),
        () => chip.Pick,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(ChipFill, ChipHeight / 2))
      .Named(chip.Name);

  // What leads a value's name: a color's swatch, else its type's mark. One
  // made on the spot that can be any type (a condition, a group) wears the
  // mark of the place it would go.
  private View Lead(OverlayValueDefinition value)
  {
    if (value.IsColor)
      return ColorDot(value);
    OverlayValueType type = OverlayVariableTypes.TypeOf(value);
    View mark = type == OverlayValueType.Any ? TypeMarks.Mark(() => VM.Type.Value, 16) : TypeMarks.Mark(type, 16);
    return mark.Padding(new Insets(0, 0, 0, 6)).AllowsHitTesting(false);
  }

  // The highlight color, or white ringed for one picked by hand.
  private static View ColorDot(OverlayValueDefinition value)
  {
    UiColor fill = value.Id == OverlayValueId.HighlightColor ? (UiColor)UiTheme.Accent : Colors.Foreground;
    return Circle()
      .Fill(fill)
      .Frame(width: 10, height: 10)
      .Background(Circle().Fill(Colors.Muted).Frame(width: 13, height: 13))
      .Frame(width: 16, height: 16)
      .Padding(new Insets(0, 0, 0, 6))
      .AllowsHitTesting(false);
  }

  private View Scroller(View content) =>
    ScrollView(content)
      .Configure<ScrollRect>(scroll =>
      {
        scroll.scrollSensitivity = 34;
        scroll.inertia = !EnvironmentValue(EditorEnvironment.ReduceMotion);
      });
}
