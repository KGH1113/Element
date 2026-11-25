using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
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
/// The dock's values, as tall as the keypad beside it. On the left a search
/// field over the list of places: the built-in categories with how many
/// values each holds, then, under a small heading, one row per mod that adds
/// values. On the right the open place's chips wrap under its name, a mod's
/// in a row per group beside the group's heading; while something is typed
/// they give way to what the search finds, by place. Each chip names its
/// value and, in a muted ink, what it reads now. Only the list and the page
/// scroll; the list shows a slim bar and fades at an edge while more of it
/// lies beyond. Every surface is opaque.
/// </summary>
internal sealed class ValueBrowserView : View<ValueBrowserViewModel>
{
  public const float FieldHeight = 34,
    Gap = 8,
    ListHeight = KeypadView.Height - FieldHeight - Gap,
    RowHeight = 30,
    RowGap = 2,
    ModsHeadingHeight = 22,
    PaneGap = 20,
    ChipHeight = 30,
    ChipGap = 6,
    GroupGap = 8,
    HeadingGap = 18,
    LiveWidth = 150;

  // The search's icon and clear-button space, also used to size its hint.
  private const float SearchStart = 36,
    SearchEnd = 38;

  private const float RowInset = 12,
    ListRadius = 8,
    MinimumList = 184,
    MaximumList = 240,
    MaximumHeading = 150;

  private const float GhostHeight = 30;

  private static readonly UiColor GhostInk = new Color(.08f, .08f, .08f);

  private static readonly UiColor ChipFill = UiPalette.EditorControl,
    ChipInk = new Color(.92f, .92f, .92f),
    LiveInk = UiPalette.Muted;

  private float _listWidth = -1;

  public ValueBrowserView(ValueBrowserViewModel vm)
    : base(vm) { }

  /// <summary>How wide the list of places is: its longest name and count, within limits. Measured once, when first drawn.</summary>
  private float ListWidth => _listWidth >= 0 ? _listWidth : _listWidth = MeasureList();

  // How wide words are at the dock's size; handed down by the editor.
  private float Measure(string words) => EnvironmentValue(EditorEnvironment.Measure)(words);

  public override View Body =>
    HStack(spacing: PaneGap, alignment: VAlignment.Top, List(), Page())
      .Frame(maxWidth: float.PositiveInfinity, height: KeypadView.Height, alignment: Alignment.TopLeading)
      // A few times a second while shown, the chips take what their values read now.
      .OnChange(() => VM.Live.Tick, _ => VM.Follow());

  // ---- The list of places ----

  private View List() =>
    VStack(
        spacing: Gap,
        SearchBox(),
        // One opaque base under the rows: a row's hover lights it without letting the overlay through.
        Scroller(VStack(spacing: RowGap, Places()).Padding(new Insets(0, 0, 4, 0)))
          // A slim bar and fading edges say more places lie beyond what shows.
          .Configure<ScrollRect>(scroll => UiScrollCues.Add(scroll, UiPalette.EditorDim, ListRadius))
          .Named("Places")
          .Frame(width: ListWidth, height: ListHeight)
          .Background(UiPalette.EditorDim, ListRadius)
      )
      .Frame(width: ListWidth, alignment: Alignment.TopLeading);

  private View[] Places()
  {
    List<View> rows = new();
    foreach (ValueFolderViewModel folder in VM.Categories)
      rows.Add(Place(folder));
    if (VM.Mods.Count > 0)
    {
      rows.Add(
        UI.Text(L("모드", "Mods"))
          .Font(Font.Caption.Sized(11).Bolded())
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Named("Mods heading")
          .Padding(new Insets(0, RowInset, 4, RowInset))
          .Frame(maxWidth: float.PositiveInfinity, height: ModsHeadingHeight, alignment: Alignment.BottomLeading)
      );
      foreach (ValueFolderViewModel folder in VM.Mods)
        rows.Add(Place(folder));
    }
    return rows.ToArray();
  }

  // A place: its name, and on the right how many values it holds. The whole row is the button.
  private View Place(ValueFolderViewModel folder) =>
    Button(
        HStack(
            spacing: 0,
            UI.Text(folder.Name).Font(Font.Caption.Sized(14)).Foreground(() => folder.Ink.Value).LineLimit(1),
            Spacer(minLength: 10),
            UI.Text(() => folder.Count.Value).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1)
          )
          .Padding(0, RowInset)
          .Frame(maxWidth: float.PositiveInfinity, height: RowHeight),
        () => folder.Select,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => folder.Fill.Value, 9))
      .Named("Place " + folder.Key);

  // The search: a capsule like a chip, the glass leading, × once something is typed.
  private View SearchBox() =>
    HStack(
        spacing: 8,
        Image(ElementIcons.Of(UiIconKind.Search), 16).Foreground(Colors.Muted),
        TextField(Bind(() => VM.Query.Value), L("값 찾기", "Find a value"))
          .TextFieldStyle(TextFieldStyle.Plain)
          .CharacterLimit(40)
          .RestoreOriginalTextOnEscape(false)
          .Focused(Bind(() => VM.SearchFocused.Value))
          .OnTextChanged(_ => VM.Search.TryExecute())
          .OnSubmit(_ => VM.PickFirst.TryExecute())
          .OnEditEnded(
            (_, canceled) =>
            {
              if (canceled)
                VM.ClearSearch.TryExecute();
            }
          )
          .Named("Value search")
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        If(
          () => VM.Searching.Value,
          () =>
            Button(Image(ElementIcons.Of(UiIconKind.Close), 12), () => VM.ClearSearch, ButtonStyle.Icon)
              .Foreground(Colors.Muted)
              .Named("Clear value search")
              .Frame(width: 24, height: 24)
        )
      )
      .Font(Font.Caption.Sized(14))
      .Padding(new Insets(0, RowInset, 0, 5))
      .Frame(width: ListWidth, height: FieldHeight)
      .Background(UiPalette.EditorControl, Capsule());

  // ---- The page ----

  private View Page()
  {
    List<View> pages = new();
    foreach (ValueFolderViewModel folder in VM.Categories)
      pages.Add(FolderPage(folder));
    foreach (ValueFolderViewModel folder in VM.Mods)
      pages.Add(FolderPage(folder));
    pages.Add(If(() => VM.Searching.Value, Results));
    return VStack(
      spacing: Gap,
      HStack(
          spacing: 8,
          UI.Text(() => VM.Title.Value)
            .Font(Font.Caption.Sized(13).Bolded())
            .Foreground(Colors.Muted)
            .LineLimit(1)
            .Named("Values title"),
          // How many it found; a search that found nothing says so below instead.
          If(
            () => VM.Searching.Value && !VM.NothingFound.Value,
            () => UI.Text(() => VM.FoundCount.Value).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1)
          ),
          Spacer()
        )
        .Padding(new Insets(0, 2, 0, 0))
        .Frame(maxWidth: float.PositiveInfinity, height: FieldHeight),
      ZStack(Alignment.TopLeading, pages.ToArray())
        .Frame(maxWidth: float.PositiveInfinity, height: ListHeight, alignment: Alignment.TopLeading)
    );
  }

  // Each place's page is made the first time it opens and kept, with where
  // it was scrolled; "My values" is made again when its values change.
  private View FolderPage(ValueFolderViewModel folder) =>
    If(
      () => folder.Open.Value,
      () =>
        Switch(
          () => folder.Version.Value,
          _ => Scroller(Rows(folder.Groups)).Frame(maxWidth: float.PositiveInfinity, height: ListHeight)
        )
    );

  private View Rows(IReadOnlyList<ValueChipRow> groups)
  {
    if (groups.Count == 1 && groups[0].Heading == null)
      return Chips(groups[0].Chips, false);
    float column = 0;
    foreach (ValueChipRow group in groups)
      column = Mathf.Max(column, Measure(group.Heading ?? ""));
    column = Mathf.Min(MaximumHeading, column + HeadingGap);
    View[] rows = new View[groups.Count];
    for (int i = 0; i < rows.Length; i++)
      rows[i] = Row(groups[i].Heading ?? "", column, Chips(groups[i].Chips, false));
    return VStack(spacing: GroupGap, rows);
  }

  // What the search finds, by place, each beside the place's name; or that it found nothing.
  private View Results()
  {
    float column = 0;
    List<View> rows = new();
    foreach (ValueFolderViewModel folder in VM.Categories)
      column = Mathf.Max(column, Measure(folder.Name));
    foreach (ValueFolderViewModel folder in VM.Mods)
      column = Mathf.Max(column, Measure(folder.Name));
    column = Mathf.Min(MaximumHeading, column + HeadingGap);
    foreach (ValueFolderViewModel folder in VM.Categories)
      rows.Add(Found(folder, column));
    foreach (ValueFolderViewModel folder in VM.Mods)
      rows.Add(Found(folder, column));
    rows.Add(If(() => VM.NothingFound.Value, NothingFound));
    return Scroller(VStack(spacing: GroupGap, rows.ToArray()))
      // A new search starts at the top.
      .Configure<ScrollRect, string>(() => VM.Query.Value, (scroll, _) => scroll.verticalNormalizedPosition = 1)
      .Frame(maxWidth: float.PositiveInfinity, height: ListHeight);
  }

  private View Found(ValueFolderViewModel folder, float column) =>
    If(
      () => folder.Found.Value,
      () => Row(folder.Name, column, Switch(() => folder.Version.Value, _ => Chips(folder.Chips, true)))
    );

  private View NothingFound() =>
    VStack(
        spacing: 4,
        UI.Text(() => VM.Nothing.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(Colors.Foreground)
          .LineLimit(1)
          .Named("Nothing found"),
        UI.Text(
            L(
              "값 이름(한글·영어), 분류, 모드 이름으로 찾을 수 있어요",
              "Search by a value's name in Korean or English, its category or its mod"
            )
          )
          .Font(Font.Caption.Sized(12))
          .Foreground(Colors.Muted)
          .LineLimit(2)
      )
      .Padding(new Insets(6, 2, 0, 0))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);

  // A heading in a column on the left, its chips wrapping beside it.
  private static View Row(string heading, float column, View chips) =>
    HStack(
      spacing: 0,
      alignment: VAlignment.Top,
      UI.Text(heading)
        .Font(Font.Caption.Sized(12))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Frame(width: column, height: ChipHeight, alignment: Alignment.Leading),
      chips
    );

  private View Chips(IReadOnlyList<ValueChipViewModel> chips, bool found)
  {
    View[] views = new View[chips.Count];
    for (int i = 0; i < views.Length; i++)
      views[i] = found ? FoundChip(chips[i]) : Chip(chips[i]);
    return Flow(ChipGap, views).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
  }

  private View FoundChip(ValueChipViewModel chip) => If(() => chip.Found.Value, () => Chip(chip));

  // A chip is pressed to pick its value and dragged to carry it out. The
  // label takes the chip's height, so the capsule, its press and its drag
  // are all as tall as the chip. What the value reads now follows its name,
  // keeping the room its widest number took.
  private View Chip(ValueChipViewModel chip) =>
    Button(
        HStack(
            spacing: 0,
            ColorDot(chip.Value),
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
          .Padding(0, 12)
          .Frame(height: ChipHeight),
        () => chip.Pick,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(ChipFill, ChipHeight / 2))
      // A value that can't go where values go now reads faint; a click says why.
      .Opacity(() => VM.Fit(chip.Value) ? 1f : .38f)
      // Carried out of the dock, the value lands wherever it is let go.
      .Draggable(() => chip.Value, Ghost)
      .Named(chip.Name);

  // A color leads its name with its swatch: the highlight color, or white
  // ringed for one picked by hand. Other values have none.
  private static View ColorDot(OverlayValueDefinition value)
  {
    if (!value.IsColor)
      return null;
    UiColor fill = value.Id == OverlayValueId.HighlightColor ? (UiColor)UiTheme.Accent : Colors.Foreground;
    return Circle()
      .Fill(fill)
      .Frame(width: 10, height: 10)
      .Background(Circle().Fill(Colors.Muted).Frame(width: 13, height: 13))
      .Frame(width: 13, height: 13)
      .Padding(new Insets(0, 0, 0, 6))
      .AllowsHitTesting(false);
  }

  // The carried value beside the pointer, in the highlight color as it is
  // when the drag starts. Opaque, so the overlay behind never shows through;
  // greyed while the place under it can't take it.
  private static View Ghost(OverlayValueDefinition value)
  {
    UiColor carried = UiPalette.SoftAccent,
      refused = UiPalette.Wash(UiPalette.EditorDim, .45f);
    return UI.Text(ValueSectionNames.ValueName(value))
      .Font(Font.Caption.Sized(13))
      .Foreground(GhostInk)
      .LineLimit(1)
      .Padding(new Insets(0, 14, 0, 14))
      .Frame(height: GhostHeight)
      .Background(() => DragSession.State == DropState.Refuses ? refused : carried, GhostHeight / 2);
  }

  private View Scroller(View content) =>
    ScrollView(content)
      .Configure<ScrollRect>(scroll =>
      {
        scroll.scrollSensitivity = 34;
        scroll.inertia = !EnvironmentValue(EditorEnvironment.ReduceMotion);
      });

  // The list fits its longest name beside its largest count, and the search's hint.
  private float MeasureList()
  {
    float widest = Measure(L("값 찾기", "Find a value")) + SearchStart + SearchEnd + 4;
    foreach (ValueFolderViewModel folder in VM.Categories)
      widest = Mathf.Max(widest, Width(folder));
    foreach (ValueFolderViewModel folder in VM.Mods)
      widest = Mathf.Max(widest, Width(folder));
    return Mathf.Clamp(Mathf.Ceil(widest), MinimumList, MaximumList);
  }

  // Its words are a size above what Measure gives.
  private float Width(ValueFolderViewModel folder) =>
    RowInset + (Measure(folder.Name) + Measure(folder.Count.Value)) * 14 / 13 + 10 + RowInset + 2;
}
