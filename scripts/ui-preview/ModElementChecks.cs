using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.ModEditor;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorBar;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Infrastructure.Mods;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Popovers;
using ElementKit;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UI = ElementUI.UI;

namespace Element.Editor
{
  [Serializable]
  public sealed class PreviewCardSettings
  {
    public string Title = "Card";
    public Color Fill = new Color(.12f, .12f, .14f, .9f);
    public Paint Accent = Paint.Solid(new Color(1f, .62f, .26f));
    public float Progress = .6f;
    public bool Rounded = true;
    public int Style;
    public Anchor TitleAt = Anchor.TopLeft;
    public string Font = "";
    public int Unit;
    public List<bool> Show = new List<bool> { true, false, true };
    public List<string> Words = new List<string> { "44", "32" };
    public float Width = 360;
    public float Height = 120;
    public int Bars = 1;
    public float From = 10;
    public float To = 80;
    public ElementKit.Shortcut Hide;
    public bool Always;
  }

  // A card drawn with Element's helpers, edited with every part a mod gets:
  // what a mod developer would write.
  [ElementKit.Element("card", "미리보기 카드", "Preview card")]
  public sealed class PreviewCard : OverlayElement<PreviewCardSettings>
  {
    private RoundedBox _background, _track, _fill;
    private TextMeshProUGUI _title;

    protected override void OnCreate()
    {
      _background = Drawing.Box(Root);
      _track = Drawing.Box(Root);
      _fill = Drawing.Box(Root);
      _title = Drawing.Text(Root, "", 28);
    }

    protected override void OnSettingsChanged()
    {
      PreviewCardSettings s = Settings;
      Size = new Vector2(s.Width, s.Height);
      ElementKit.Drawing.Place(_background.Rect, 0, 0, s.Width, s.Height);
      _background.Color = s.Fill;
      _background.Rounding = s.Rounded ? 14 : 0;
      ElementKit.Drawing.Place(_title.rectTransform, 20, 14, s.Width - 40, 36);
      _title.text = s.Title;
      _title.color = s.Style == 1 ? Drawing.Solid(s.Accent) : Color.white;
      _title.alignment = s.TitleAt == Anchor.Top ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
      Drawing.SetFont(_title, s.Font);
      float barTop = s.Height - 34;
      ElementKit.Drawing.Place(_track.Rect, 20, barTop, s.Width - 40, 12);
      _track.Color = new Color(1, 1, 1, .15f);
      _track.Rounding = 6;
      ElementKit.Drawing.Place(_fill.Rect, 20, barTop, Mathf.Max(1, (s.Width - 40) * s.Progress), 12);
      _fill.Paint = s.Accent;
      _fill.Rounding = 6;
    }

    protected override void OnEdit(EditorPage page)
    {
      page.Label(page.L("카드 한 장과 진행도 막대예요.", "A card with a progress bar."));
      EditorTabs tabs = page.Tabs(new[] { page.L("모양", "Look"), page.L("내용", "Content"), page.L("고급", "More") });
      using (tabs.Page(0))
      {
        using (page.Card(page.L("색", "Colors")))
        {
          page.ColorPicker(page.L("배경", "Background"), Settings.Fill, color => Settings.Fill = color);
          page.ColorPicker(page.L("강조 색", "Accent"), Settings.Accent, paint => Settings.Accent = paint);
        }
        page.Slider(page.L("진행도", "Progress"), Settings.Progress, 0, 1, value => Settings.Progress = value);
        page.Switch(page.L("둥근 모서리", "Rounded"), Settings.Rounded, on => Settings.Rounded = on);
        page.Choice(page.L("제목 색", "Title color"), new[] { page.L("흰색", "White"), page.L("강조", "Accent") }, Settings.Style, index => Settings.Style = index);
        page.AnchorPicker(page.L("제목 위치", "Title at"), Settings.TitleAt, at => Settings.TitleAt = at);
      }
      using (tabs.Page(1))
      {
        page.TextField(page.L("제목", "Title"), Settings.Title, text => Settings.Title = text);
        page.FontPicker(page.L("제목 폰트", "Title font"), Settings.Font, font => Settings.Font = font);
        page.Dropdown(page.L("단위", "Unit"), new[] { "px", "%", "BPM", page.L("키", "keys"), page.L("초", "s") }, Settings.Unit, index => Settings.Unit = index);
        page.MultiChoice(page.L("보일 것", "Show"), new[] { page.L("제목", "Title"), page.L("막대", "Bar"), page.L("숫자", "Number") }, Settings.Show, (index, on) => Settings.Show[index] = on);
        page.List(page.L("단어", "Words"), Settings.Words, null, "new");
      }
      using (tabs.Page(2))
      {
        page.Section(page.L("크기", "Size"));
        page.Number(page.L("너비", "Width"), Settings.Width, 120, 960, value => Settings.Width = value);
        page.Number(page.L("높이", "Height"), Settings.Height, 60, 400, value => Settings.Height = value);
        page.Stepper(page.L("막대 수", "Bars"), Settings.Bars, 1, 8, value => Settings.Bars = value);
        page.RangeSlider(page.L("표시 구간", "Range"), Settings.From, Settings.To, 0, 100, (low, high) => { Settings.From = low; Settings.To = high; }, step: 1);
        page.Divider();
        page.Shortcut(page.L("숨기기 키", "Hide key"), Settings.Hide, shortcut => Settings.Hide = shortcut);
        page.Checkbox(page.L("레벨 밖에서도 보이기", "Show outside levels"), Settings.Always, on => Settings.Always = on)
          .Hint(page.L("메뉴에서도 카드를 보여요", "Shows the card in menus too"));
        page.IconButton(EditorIcon.Reset, page.L("처음 모양으로", "Start over"), () =>
        {
          PreviewCardSettings fresh = new PreviewCardSettings();
          Settings.Title = fresh.Title;
          Settings.Rounded = fresh.Rounded;
        });
      }
      // Beside the preview, where a mod would put something of its own.
      page.Label(page.L("직접 놓은 라벨", "Placed by hand")).At(page.PreviewArea.x, page.PreviewArea.y).Width(200);
    }
  }

  [Serializable]
  public sealed class PreviewMeterSettings
  {
    public Color Fill = new Color(.36f, .72f, 1f);
    public float Width = 240;
    public bool Rounded = true;
    public int Ticks = 4;
    public float Height = 32;
    public int Look;
    public string Label = "Meter";
    public string Font = "";
    public Anchor LabelAt = Anchor.TopLeft;
    public int Unit;
    public List<bool> Show = new List<bool> { true, true, false };
    public bool Glow;
    public Paint GlowColor = Paint.Solid(new Color(1f, .62f, .26f));
    public float From = 10;
    public float To = 80;
    public ElementKit.Shortcut Hide;
  }

  // The same kind of element with its editing screen written with ElementUI:
  // every kind of control there is, bound to its settings, with Element's
  // own pickers, a hint, a part that is off until a switch turns it on, and
  // one that shows only then. The mod writes it the way it always has; the
  // editor draws it as its property column.
  [ElementKit.Element("meter", "미리보기 미터", "Preview meter")]
  public sealed class PreviewMeter : OverlayElement<PreviewMeterSettings>
  {
    private RoundedBox _bar;
    private TextMeshProUGUI _label;

    protected override void OnCreate()
    {
      _bar = Drawing.Box(Root);
      _label = Drawing.Text(Root, "", 20);
    }

    protected override void OnSettingsChanged()
    {
      PreviewMeterSettings s = Settings;
      Size = new Vector2(s.Width, s.Height + 32);
      ElementKit.Drawing.Place(_label.rectTransform, 0, 0, s.Width, 28);
      _label.text = s.Label;
      _label.alignment = s.LabelAt == Anchor.Top ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
      Drawing.SetFont(_label, s.Font);
      ElementKit.Drawing.Place(_bar.Rect, 0, 32, s.Width, s.Height);
      _bar.Color = s.Fill;
      _bar.Rounding = s.Rounded ? s.Height / 2 : 0;
    }

    protected override ElementUI.View Editor(EditorViewModel<PreviewMeterSettings> vm) =>
      UI.VStack(
        12,
        UI.Card(
          vm.L("막대", "Bar"),
          UI.VStack(
            10,
            EditorParts.ColorPicker(vm.L("채우기", "Fill"), UI.Bind(() => vm.Settings.Fill)),
            UI.Toggle(vm.L("둥근 모서리", "Rounded"), UI.Bind(() => vm.Settings.Rounded))
              .Hint(vm.L("막대 양 끝을 둥글게 깎아요.", "Rounds both ends of the bar.")),
            UI.Slider(vm.L("너비", "Width"), UI.Bind(() => vm.Settings.Width), 80, 480, step: 1),
            UI.Stepper(vm.L("눈금", "Ticks"), UI.Bind(() => vm.Settings.Ticks), 0, 10),
            UI.NumberField(vm.L("높이", "Height"), UI.Bind(() => vm.Settings.Height), 8, 64),
            UI.Picker(vm.L("모양", "Look"), new[] { vm.L("채움", "Solid"), vm.L("점", "Dots") }, UI.Bind(() => vm.Settings.Look))
          )
        ),
        UI.Card(
          vm.L("이름", "Label"),
          UI.VStack(
            10,
            UI.TextField(vm.L("이름", "Label"), UI.Bind(() => vm.Settings.Label)),
            EditorParts.FontPicker(vm.L("폰트", "Font"), UI.Bind(() => vm.Settings.Font)),
            EditorParts.AnchorPicker(vm.L("이름 위치", "Label at"), UI.Bind(() => vm.Settings.LabelAt)),
            UI.Picker(vm.L("단위", "Unit"), new[] { "px", "%", "BPM", vm.L("키", "keys"), vm.L("초", "s") }, UI.Bind(() => vm.Settings.Unit), PickerAppearance.Dropdown),
            UI.MultiPicker(vm.L("보일 것", "Show"), new[] { vm.L("이름", "Name"), vm.L("숫자", "Number"), vm.L("최대", "Max") }, UI.Bind(() => vm.Settings.Show))
          )
        ),
        UI.Card(
          vm.L("더 보기", "More"),
          UI.VStack(
            10,
            UI.Toggle(vm.L("빛", "Glow"), UI.Bind(() => vm.Settings.Glow)),
            EditorParts.ColorPicker(vm.L("빛 색", "Glow color"), UI.Bind(() => vm.Settings.GlowColor)).Disabled(() => !vm.Settings.Glow),
            UI.Slider(vm.L("빛 세기", "Glow strength"), UI.Bind(() => vm.Settings.Height), 8, 64, step: 1).Disabled(() => !vm.Settings.Glow),
            UI.If(() => vm.Settings.Glow, () => UI.Text(vm.L("빛은 막대 뒤에 번져요.", "The glow spreads behind the bar.")).Font(ElementUI.Font.Caption).Foreground(ElementUI.Colors.Muted)),
            UI.RangeSlider(vm.L("표시 구간", "Range"), UI.Bind(() => vm.Settings.From), UI.Bind(() => vm.Settings.To), 0, 100, step: 1),
            EditorParts.ShortcutField(vm.L("숨기기 키", "Hide key"), UI.Bind(() => vm.Settings.Hide))
          )
        )
      );
  }

  // Elements other mods draw: Add Element → More, the editing screen built
  // from Element's parts or written with ElementUI, both in the property
  // column where the text editor keeps its own, the preview following each
  // change, undo, Done, and the box shown while the mod is missing.
  public static partial class SettingsPreview
  {
    private static void CheckModElements(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      PreviewModAssemblies mods = new PreviewModAssemblies();
      List<string> warnings = new List<string>();
      mods.Add(new ModAssembly("Preview", typeof(PreviewCard).Assembly, () => true));
      host.ModElements = new ModElements(mods, warnings.Add);
      // What ElementUI reports while the editing screens run: none is expected.
      Action<Exception> report = _modScreenErrors.Add;
      _modScreenErrors.Clear();
      ElementUI.Runtime.ErrorReported += report;
      try
      {
        Call(editor, "ToggleAddMenu");
        RectTransform menu = AddMenu(editor);
        Settle();
        Pressable[] rows = Rows(menu);
        Assert(rows.Length == 4 && rows[3].name == "Add Extension" && rows[3].IsInteractable(), "Add Element gains More once a mod declares an element");
        Assert(warnings.Count == 0, "the preview elements are found without problems: " + string.Join(" | ", warnings));
        Press(rows[3]);
        RectTransform list = Field<ListMenu>(Kind<ModKind>(editor), "_menu").Root;
        Assert(list != null && Rows(list).Select(button => button.name).SequenceEqual(new[] { "Add Preview/card", "Add Preview/meter" }),
          "More lists each element other mods declare");
        Assert(list.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "Preview"),
          "each element says which mod it comes from");
        SaveFrame(camera, texture, canvases, name + "-mod-types");

        Press(Rows(list).First(row => row.name == "Add Preview/card"));
        ModOverlayElement element = host.LayoutTargets.OfType<ModOverlayElement>().Last();
        ModEditorScreen mods2 = (ModEditorScreen)Kind<ModKind>(editor).Editor;
        Assert(element.Enabled && element.Drawn && mods2.IsOpen && mods2.Page != null && mods2.View == null,
          "a new element opens its editing screen at once, its parts on a page");
        Settle();
        string Working() => Field<string>(mods2, "_working");
        // A button makes the parts again, in a column of their own.
        ModEditorColumn Column() => mods2.Page.Column;
        // A part's control in the column, found by the name its part gives it, shown now.
        Pressable Find(string prefix) => Column().Root.GetComponentsInChildren<Pressable>(false).First(button => button.name.StartsWith(prefix));
        Pressable Option(string label) => Column().Root.GetComponentsInChildren<Pressable>(false).First(button => button.name == "Option " + label);
        Pressable ToolOption(string label) => Column().ToolRoot.GetComponentsInChildren<Pressable>(false).First(button => button.name == "Option " + label);
        // What the editor does at the start of the next frame: a button's parts made again, a tool closed.
        void Next()
        {
          Call(mods2, "ApplyPending");
          Settle();
        }

        // Tab 1: colors in a card, a slider, a switch, a choice and the nine points.
        CheckModColumn(mods2, Column(), "card look");
        Assert(warnings.Count == 0, "its parts are laid out without problems: " + string.Join(" | ", warnings));
        CheckVisibleParts(mods2, name, "look");
        SaveFrame(camera, texture, canvases, name + "-mod-editor");

        // Grow from on its bar, over the preview: its popover hangs under it.
        CheckEditorTopLine(mods2, "Mod element editor", L("미리보기 카드 편집", "Edit Preview card"));
        PropertyButtonViewModel modGrow = Field<PropertyButtonViewModel>(mods2, "_growButton");
        OverlayGrowth growth = Field<OverlayGrowth>(mods2, "_growth");
        Assert(modGrow.Value == GrowPickerViewModel.ShortName(growth), "the bar's Grow from names the element's point: " + modGrow.Value);
        Rect barRect = ScreenRectOf(Field<EditorBarPanel>(mods2, "_bar").Root), previewRect = ModPreviewRect(mods2);
        Assert(barRect.xMax <= ScreenRectOf(Column().Root).xMin && Mathf.Abs(barRect.center.x - previewRect.center.x) < 2,
          "Grow from sits over the preview, left of the column: " + barRect + " / " + previewRect);
        PressBar(mods2, modGrow);
        Call(mods2, "FitPreview");
        GrowPopoverPanel modGrowPopover = Field<GrowPopoverPanel>(mods2, "_grow");
        Assert(modGrowPopover.Visible && modGrow.Active && ScreenRectOf(modGrowPopover.Root).yMax <= BarButtonRect(mods2, modGrow).yMin
          && InsideScreen(ScreenRectOf(modGrowPopover.Root)), "Grow from opens its popover under its button, lit while it is open");
        Settle();
        Press(modGrowPopover.Root.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.BottomRight));
        Call(mods2, "FitPreview");
        Assert(Field<OverlayGrowth>(mods2, "_growth") == OverlayGrowth.BottomRight && modGrow.Value == GrowPickerViewModel.ShortName(OverlayGrowth.BottomRight)
          && modGrowPopover.ViewModel.Caption.Value.Contains(L("오른쪽 아래", "bottom right")), "a point picks where the element grows from");
        mods2.Escape();
        Call(mods2, "FitPreview");
        Assert(mods2.IsOpen && !modGrowPopover.Visible && !modGrow.Active, "Esc closes the popover before the editor");
        Call(mods2, "PickGrowth", growth);
        Press(Find("Switch " + L("둥근 모서리", "Rounded")));
        Assert(Working().Contains("\"Rounded\":false"), "a switch row changes the setting: " + Working());
        ByName<SliderDrag>(Column().Root, "Slider " + L("진행도", "Progress")).Moved(.25f, true);
        Assert(Working().Contains("\"Progress\":0.25"), "a slider changes the setting as it moves: " + Working());
        Press(Option(L("강조", "Accent")));
        Assert(Working().Contains("\"Style\":1"), "two choices that fit sit side by side in their row, and pick the setting");
        Press(Find("Anchor " + L("제목 위치", "Title at")));
        AssertModTool(mods2, Column(), L("제목 위치", "Title at"));
        Press(Column().ToolRoot.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.Top));
        Assert(Working().Contains("\"TitleAt\":1"), "the nine points open in the column and pick the setting");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-anchor");
        PressToolBack(mods2, Column());
        Assert(Find("Anchor " + L("제목 위치", "Title at")).GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == GrowPickerViewModel.ShortName(OverlayGrowth.Top)),
          "and the row names the point picked");
        Press(Find("Paint "));
        Settle();
        RectTransform paint = ByName<RectTransform>(Column().ToolRoot, "Color picker");
        AssertModTool(mods2, Column(), L("강조 색", "Accent"));
        Assert(Shown(paint) && ByName<RectTransform>(paint, "Gradient kinds") != null && InsideScreen(ScreenRectOf(paint))
          && Mathf.Abs(ScreenRectOf(paint).xMax - ScreenRectOf(Column().Root).xMax) < 1,
          "a paint opens the full picker in the column, gradients included, as the text editor's Text color does");
        for (int i = 0; i < 30; i++)
          Call(mods2, "FitPreview");
        Assert(ModPreviewRect(mods2).xMax <= ScreenRectOf(paint).xMin + .5f, "the preview keeps left of the picker reaching past the column");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-paint");
        mods2.Escape();
        Next();
        Assert(mods2.IsOpen && Column().OpenTool == null && Shown(Column().Root), "Esc closes the tool before the editor, and the rows come back");
        SettlePreview(mods2);

        // Tab 2: words, a font, a list of choices, several choices and a list of words.
        Press(Option(L("내용", "Content")));
        Settle();
        CheckVisibleParts(mods2, name, "content");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-content");
        TMP_InputField title = ByName<TMP_InputField>(Column().Root, "Text " + L("제목", "Title"));
        title.text = "Hello";
        Assert(Working().Contains("\"Title\":\"Hello\""), "a text field changes the setting as it is typed");
        Pressable unit = Find("Choice " + L("단위", "Unit"));
        Press(unit);
        AssertModTool(mods2, Column(), L("단위", "Unit"));
        Pressable[] options = Column().ToolRoot.GetComponentsInChildren<Pressable>(false).Where(row => row.name.StartsWith("Option ")).ToArray();
        Assert(options.Length == 5 && options.All(row => InsideScreen(ScreenRectOf((RectTransform)row.transform))),
          "a list of choices opens in the column, a row each: " + options.Length);
        SaveFrame(camera, texture, canvases, name + "-mod-editor-dropdown");
        Press(ToolOption("BPM"));
        Settle();
        Assert(Working().Contains("\"Unit\":2") && Column().OpenTool != null, "picking an option saves it; the list stays to try another");
        PressToolBack(mods2, Column());
        Assert(Find("Choice " + L("단위", "Unit")).GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == "BPM"), "the row shows the option picked");
        Press(Find("Choices " + L("보일 것", "Show")));
        AssertModTool(mods2, Column(), L("보일 것", "Show"));
        Press(ToolOption(L("제목", "Title")));
        Assert(Working().Contains("\"Show\":[false,false,true]"), "several choices open a switch each: " + Working());
        SaveFrame(camera, texture, canvases, name + "-mod-editor-choices");
        PressToolBack(mods2, Column());
        Assert(Find("Choices " + L("보일 것", "Show")).GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == L("숫자", "Number")),
          "the row says what is on");
        Press(Find("Font "));
        Settle();
        RectTransform fonts = ByName<RectTransform>(Column().ToolRoot, "Font popover");
        AssertModTool(mods2, Column(), L("제목 폰트", "Title font"));
        Assert(Shown(fonts) && InsideScreen(ScreenRectOf(fonts)), "a font part opens Element's font list in the column");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-font");
        Call(mods2, "ClosePopovers");
        Next();
        SettlePreview(mods2);
        Press(Find("List add"));
        Assert(Working().Contains("\"Words\":[\"44\",\"32\",\"new\"]"), "a list adds an item: " + Working());
        Next();
        Assert(Column().Root.GetComponentsInChildren<TMP_InputField>(false).Count(input => input.name.StartsWith("List item")) == 3,
          "the parts are made again with the new item, the same tab still picked");
        Press(Find("List Remove"));
        Assert(Working().Contains("\"Words\":[\"32\",\"new\"]"), "a list removes an item");
        Next();

        // Tab 3: numbers, a stepper, a range, a shortcut, a checkbox with a hint, an icon button.
        Press(Option(L("고급", "More")));
        Settle();
        CheckVisibleParts(mods2, name, "more");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-more");
        TMP_InputField widthField = ByName<TMP_InputField>(Column().Root, "Number " + L("너비", "Width"));
        CheckInputCaret(widthField, "mod number", "", "480", "960");
        widthField.text = "500";
        Assert(Working().Contains("\"Width\":500"), "a number shows as it is typed");
        widthField.text = "5000";
        widthField.onEndEdit.Invoke(widthField.text);
        Settle();
        Assert(Working().Contains("\"Width\":960") && widthField.text == "960", "and is kept within its limits when typing is done: " + widthField.text);
        Press(Find("Step up "));
        Assert(Working().Contains("\"Bars\":2"), "a stepper steps");
        SliderDrag range = ByName<SliderDrag>(Column().Root, "Range slider " + L("표시 구간", "Range"));
        range.Moved(.9f, true);
        Assert(Working().Contains("\"From\":10") && Working().Contains("\"To\":90"), "a range moves its nearer handle: " + Working());
        range.Moved(.05f, true);
        Assert(Working().Contains("\"From\":5"), "and the other");
        Press(Find("Shortcut "));
        AssertModTool(mods2, Column(), L("숨기기 키", "Hide key"));
        Press(ByName<Pressable>(Column().ToolRoot, "Shortcut field " + L("숨기기 키", "Hide key")));
        Assert(mods2.Page.Typing, "a shortcut part records the next key");
        ShortcutFieldViewModel shortcut = Field<List<ShortcutFieldViewModel>>(Field<ModPickerContext>(mods2, "_pickers"), "_shortcuts").Single();
        shortcut.TryAssign(KeyCode.H, KeyCode.None);
        Assert(!mods2.Page.Typing && Working().Contains("\"Hide\":{\"Key\":104"), "the key it records is the setting, and recording stops: " + Working());
        SaveFrame(camera, texture, canvases, name + "-mod-editor-shortcut");
        PressToolBack(mods2, Column());
        Pressable box = Find("Checkbox ");
        Press(box);
        Assert(Working().Contains("\"Always\":true"), "a checkbox ticks");
        PointerArea hover = box.GetComponentInParent<PointerArea>();
        hover.OnPointerEnter(null);
        TextMeshProUGUI tip = ByName<TextMeshProUGUI>(ByName<RectTransform>(Column().Root, "ElementUI hint"), "Text");
        Assert(Shown(tip) && tip.text == (Korean(name) ? "메뉴에서도 카드를 보여요" : "Shows the card in menus too"),
          "a part's hint shows on hover");
        string misplaced = HintProblem(hover);
        Assert(misplaced == null, "a part's hint shows beside its part: " + misplaced);
        hover.OnPointerExit(null);
        Press(Find("Icon Reset"));
        Assert(Working().Contains("\"Rounded\":true") && Working().Contains("\"Title\":\"Card\""), "an icon button runs its action");
        Next();

        // Undo keeps the picked tab.
        Call(mods2, "Undo");
        Settle();
        Assert(Working().Contains("\"Rounded\":false"), "undo brings the last change back");
        Assert(Shown(ByName<SliderDrag>(mods2.Page.Column.Root, "Range slider " + L("표시 구간", "Range"))), "after undo the same tab is still picked");

        mods2.Close(true, true);
        Assert(!mods2.IsOpen && element.Settings.Mod.Data.Contains("\"Style\":1") && element.Settings.Mod.Data.Contains("\"Unit\":2"),
          "Done saves the settings with the element");
        Select(editor, IndexOf(editor, element));
        SaveFrame(camera, texture, canvases, name + "-mod-placed");

        CheckModElementView(editor, host, camera, texture, canvases, name, mods2);

        // The mod goes away: the element keeps its place and settings.
        mods.Remove("Preview");
        host.TickLayoutPreview();
        Assert(!element.Drawn && element.Enabled && element.Settings.Mod.Data.Contains("\"Style\":1"), "without its mod the element stays, settings kept");
        Assert(element.Label == (Korean(name) ? "미리보기 카드" : "Preview card"), "it keeps its name: " + element.Label);
        RectTransform elementRect = (RectTransform)typeof(Element.Presentation.Overlay.Elements.OverlayElementTarget)
          .GetProperty("Rect", Private).GetValue(element);
        Select(editor, IndexOf(editor, element));
        Assert(elementRect.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text.Contains("Preview") && label.font != null),
          "the editor names the mod it needs, in the overlay's font");
        SaveFrame(camera, texture, canvases, name + "-mod-missing");
        mods.Add(new ModAssembly("Preview", typeof(PreviewCard).Assembly, () => true));
        host.TickLayoutPreview();
        Assert(element.Drawn, "installing the mod again brings the element back as it was");
        Select(editor, -1);
      }
      finally
      {
        ElementUI.Runtime.ErrorReported -= report;
        host.ModElements = null;
      }
    }

    private static readonly List<Exception> _modScreenErrors = new List<Exception>();

    private static void AssertNoScreenErrors(string what) =>
      Assert(_modScreenErrors.Count == 0, what + " without errors: " + string.Join(" | ", _modScreenErrors.Select(error => error.Message)));

    // An element whose editing screen is written with ElementUI: its views
    // drawn as the column's rows, bound to its settings; each edit an undo
    // step (a drag one step), undo shown on the screen, and Done saving it.
    private static void CheckModElementView(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, ModEditorScreen mods)
    {
      Select(editor, -1);
      Call(editor, "ToggleAddMenu");
      Settle();
      Press(Rows(AddMenu(editor))[3]);
      Press(Rows(Field<ListMenu>(Kind<ModKind>(editor), "_menu").Root).First(row => row.name == "Add Preview/meter"));
      ModOverlayElement meter = host.LayoutTargets.OfType<ModOverlayElement>().Last();
      Assert(mods.IsOpen && mods.View != null && mods.Page == null && meter.Class.Info.Key == "meter",
        "an element that writes its screen with ElementUI shows that screen, not a page of parts");
      Settle();
      string Working() => Field<string>(mods, "_working");
      ModEditorColumn column = mods.View.Column;
      RectTransform view = column.Root;
      Pressable Row(string name) => ByName<Pressable>(view, name);
      void Next()
      {
        Call(mods, "ApplyPending");
        Settle();
      }
      CheckModColumn(mods, column, "meter");
      AssertNoScreenErrors("the meter's screen shows");
      // Rows past the bottom of the screen scroll into view.
      Rect columnRect = ScreenRectOf(view);
      foreach (Pressable control in view.GetComponentsInChildren<Pressable>(false))
      {
        Rect rect = ScreenRectOf((RectTransform)control.transform);
        Assert(rect.xMin >= columnRect.xMin - .5f && rect.xMax <= columnRect.xMax + .5f && rect.yMax <= columnRect.yMax + .5f,
          "its controls stand in the column: " + control.name + " " + rect);
      }

      // Its cards are the column's headings, its controls the column's rows, 40 tall and 6 apart, the names lined up.
      float scale = Field<EditorFramePanel>(mods, "_frame").Scale;
      TextMeshProUGUI heading = view.GetComponentsInChildren<TextMeshProUGUI>(false).First(label => label.text == L("막대", "Bar"));
      Rect fill = ScreenRectOf((RectTransform)Row("Color " + L("채우기", "Fill")).transform),
        rounded = ScreenRectOf((RectTransform)Row("Switch " + L("둥근 모서리", "Rounded")).transform);
      Assert(Mathf.Abs(fill.height - InspectorRowStyle.Height * scale) < 1 && Mathf.Abs(rounded.height - InspectorRowStyle.Height * scale) < 1
        && Mathf.Abs(fill.yMin - rounded.yMax - InspectorRowStyle.Gap * scale) < 1 && Mathf.Abs(fill.width - ScreenRectOf(view).width) < 1,
        "a card's controls are the column's rows, 40 tall, 6 apart, as wide as the column: " + fill + " / " + rounded);
      Assert(ScreenRectOf(heading.rectTransform).yMin >= fill.yMax - .5f && ScreenRectOf(heading.rectTransform).yMax <= ScreenRectOf(view).yMax + .5f,
        "its title is the heading over them");
      float NameLeft(Pressable row, string label) =>
        ScreenRectOf(row.GetComponentsInChildren<TextMeshProUGUI>().First(text => text.text == label).rectTransform).xMin;
      Assert(Mathf.Abs(NameLeft(Row("Color " + L("채우기", "Fill")), L("채우기", "Fill")) - NameLeft(Row("Switch " + L("둥근 모서리", "Rounded")), L("둥근 모서리", "Rounded"))) < 1,
        "every row's name starts at the same place, with or without what leads it");
      Assert(Shown(Row("Color " + L("채우기", "Fill"))) && Row("Color " + L("채우기", "Fill")).GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text.StartsWith("#")),
        "Element's color picker is a swatch row with the HEX");
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view");

      SliderDrag slider = ByName<SliderDrag>(view, "Slider " + L("너비", "Width"));
      slider.Dragging(true);
      slider.Moved(.5f, true);
      slider.Moved(.75f, false);
      slider.Dragging(false);
      Assert(Working().Contains("\"Width\":380"), "a slider bound to the settings changes them as it moves: " + Working());
      Press(Row("Switch " + L("둥근 모서리", "Rounded")));
      Assert(Working().Contains("\"Rounded\":false"), "a switch row flips them");
      Press(Row("Step up " + L("눈금", "Ticks")));
      Assert(Working().Contains("\"Ticks\":5"), "a stepper steps them");
      Press(view.GetComponentsInChildren<Pressable>(false).First(option => option.name == "Option " + L("점", "Dots")));
      Assert(Working().Contains("\"Look\":1"), "two choices side by side pick them");
      TMP_InputField heightField = ByName<TMP_InputField>(view, "Number " + L("높이", "Height"));
      heightField.text = "40";
      Assert(Working().Contains("\"Height\":40"), "a number field sets them");
      heightField.DeactivateInputField();

      // A hint by its row.
      PointerArea hover = Row("Switch " + L("둥근 모서리", "Rounded")).GetComponentInParent<PointerArea>();
      hover.OnPointerEnter(null);
      Settle();
      TextMeshProUGUI roundedTip = ByName<TextMeshProUGUI>(ByName<RectTransform>(view, "ElementUI hint"), "Text");
      Assert(Shown(roundedTip) && roundedTip.text == L("막대 양 끝을 둥글게 깎아요.", "Rounds both ends of the bar."),
        "a control's hint shows on hover");
      string misplaced = HintProblem(hover);
      Assert(misplaced == null, "a control's hint shows beside its row, in the column: " + misplaced);
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view-hint");
      hover.OnPointerExit(null);
      Settle();

      // Off until a switch turns it on, then shown.
      Pressable glowColor = Row("Paint " + L("빛 색", "Glow color"));
      TextMeshProUGUI glowName = glowColor.GetComponentsInChildren<TextMeshProUGUI>().First(text => text.text == L("빛 색", "Glow color"));
      Assert(glowName.color.r < .8f, "Element's own row reads muted while it is off: " + glowName.color);
      Assert(!glowColor.IsInteractable() && !Shown(view.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(label => label.text == L("빛은 막대 뒤에 번져요.", "The glow spreads behind the bar."))),
        "a row that is off can't be pressed, and what shows only with the switch on is hidden");
      Press(Row("Switch " + L("빛", "Glow")));
      Settle();
      Assert(glowName.color.r > .9f, "and plain again once on: " + glowName.color);
      Assert(glowColor.IsInteractable() && Shown(view.GetComponentsInChildren<TextMeshProUGUI>(true).First(label => label.text == L("빛은 막대 뒤에 번져요.", "The glow spreads behind the bar."))),
        "turning the switch on lets it be pressed, and shows what goes with it");

      // Element's color picker opens in the column, under a back row naming it, as the text editor's Text color does.
      Press(Row("Color " + L("채우기", "Fill")));
      AssertModTool(mods, column, L("채우기", "Fill"));
      RectTransform color = ByName<RectTransform>(column.ToolRoot, "Color picker");
      Assert(Shown(color) && Mathf.Abs(ScreenRectOf(color).xMax - ScreenRectOf(view).xMax) < 1
        && Mathf.Abs(ScreenRectOf(color).width - Element.Presentation.Ui.ColorPicker.ColorPickerLayout.Column * scale) < 1,
        "a color opens Element's picker ending with the column, as the text editor's color controls do: " + ScreenRectOf(color));
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view-color");
      PressToolBack(mods, column);

      // A list of choices, several choices, the nine points, a font and a shortcut open in the column too.
      Press(Row("Choice " + L("단위", "Unit")));
      AssertModTool(mods, column, L("단위", "Unit"));
      Press(column.ToolRoot.GetComponentsInChildren<Pressable>(false).First(option => option.name == "Option BPM"));
      Assert(Working().Contains("\"Unit\":2"), "a list of choices picks them");
      PressToolBack(mods, column);
      Press(Row("Choices " + L("보일 것", "Show")));
      AssertModTool(mods, column, L("보일 것", "Show"));
      Press(column.ToolRoot.GetComponentsInChildren<Pressable>(false).First(option => option.name == "Option " + L("최대", "Max")));
      Assert(Working().Contains("\"Show\":[true,true,true]"), "several choices turn them on and off: " + Working());
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view-choices");
      PressToolBack(mods, column);
      Assert(Row("Choices " + L("보일 것", "Show")).GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == string.Join("·", L("이름", "Name"), L("숫자", "Number"), L("최대", "Max"))),
        "the row says what is on");
      Press(Row("Anchor " + L("이름 위치", "Label at")));
      AssertModTool(mods, column, L("이름 위치", "Label at"));
      Press(column.ToolRoot.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.Top));
      Assert(Working().Contains("\"LabelAt\":1"), "Element's nine-point picker sets them too");
      PressToolBack(mods, column);
      Press(Row("Font " + L("폰트", "Font")));
      AssertModTool(mods, column, L("폰트", "Font"));
      Assert(Shown(ByName<RectTransform>(column.ToolRoot, "Font popover")), "a font opens the font list");
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view-font");
      mods.Escape();
      Next();
      Assert(column.OpenTool == null && mods.IsOpen, "Esc closes the tool first");
      SettlePreview(mods);
      Press(Row("Shortcut " + L("숨기기 키", "Hide key")));
      AssertModTool(mods, column, L("숨기기 키", "Hide key"));
      PressToolBack(mods, column);

      TMP_InputField label = ByName<TMP_InputField>(view, "Text " + L("이름", "Label"));
      label.text = "Hello";
      Assert(Working().Contains("\"Label\":\"Hello\""), "a text field bound to the settings changes them as it is typed");

      // Typing ends before ⌘Z reaches the editor.
      label.DeactivateInputField();
      Settle();
      Call(mods, "Undo");
      Settle();
      PreviewMeter instance = (PreviewMeter)Field<ModPreview>(mods, "_preview").Instance;
      Assert(!Working().Contains("Hello") && instance.Settings.Label == "Meter", "undo puts the settings back: " + Working());
      Assert(label.text == "Meter", "and the screen shows them: " + label.text);
      for (int i = 0; i < 8; i++)
        Call(mods, "Undo");
      Settle();
      Assert(Working().Contains("\"Rounded\":true") && Working().Contains("\"Width\":380"), "each click is its own step: " + Working());
      Call(mods, "Undo");
      Settle();
      Assert(instance.Settings.Width == 240 && instance.Settings.Rounded, "a whole drag is one step: " + instance.Settings.Width);
      Call(mods, "Redo");
      mods.Close(true, true);
      Assert(!mods.IsOpen && meter.Settings.Mod.Data.Contains("\"Width\":380"), "Done saves what the screen changed");
      // The layout goes on as the checks after these expect it.
      Select(editor, IndexOf(editor, meter));
      Call(editor, "DeleteSelected");
      Select(editor, -1);
    }

    // What is wrong with where the hint of a hovered view (its .Hint area)
    // shows; null when it is centred on the view (or kept on its canvas),
    // right above or below it, and on the canvas.
    internal static string HintProblem(PointerArea area)
    {
      Canvas canvas = area.GetComponentInParent<Canvas>().rootCanvas;
      RectTransform hint = canvas.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(rect => rect.name == "ElementUI hint");
      if (hint == null)
        return "no hint shows";
      Rect room = ScreenRectOf((RectTransform)canvas.transform), view = ScreenRectOf((RectTransform)area.transform), shown = ScreenRectOf(hint);
      // Screen pixels per unit of the screen the hint is in: 6 between hint and view, 4 from the canvas's edges.
      float unit = shown.height / hint.rect.height;
      float centred = Mathf.Clamp(view.center.x - shown.width / 2, room.xMin + 4 * unit, room.xMax - 4 * unit - shown.width);
      bool above = Mathf.Abs(shown.yMin - view.yMax - 6 * unit) < unit, below = Mathf.Abs(view.yMin - shown.yMax - 6 * unit) < unit;
      string where = " (view " + view + ", hint " + shown + ", canvas " + room + ")";
      if (Mathf.Abs(shown.xMin - centred) > unit)
        return "not centred on its view" + where;
      if (!above && !below)
        return "not right above or below its view" + where;
      bool inside = shown.xMin >= room.xMin - unit && shown.xMax <= room.xMax + unit && shown.yMin >= room.yMin - unit && shown.yMax <= room.yMax + unit;
      return inside ? null : "off its canvas" + where;
    }

    // Where the column stands: where the text editor keeps its own (the
    // keypad's column), from under the header, the preview left of it.
    private static void CheckModColumn(ModEditorScreen editor, ModEditorColumn column, string what)
    {
      Settle();
      EditorFramePanel screen = Field<EditorFramePanel>(editor, "_frame");
      Rect shown = ScreenRectOf(column.Root), preview = ModPreviewRect(editor);
      float left = (screen.ContentLeft + screen.ContentWidth - KeypadView.Width) * screen.Scale,
        top = Screen.height - (EditorFramePanel.TopBar + InspectorPanel.Gap) * screen.Scale;
      Assert(Mathf.Abs(shown.xMin - left) < 1 && Mathf.Abs(shown.width - KeypadView.Width * screen.Scale) < 1 && Mathf.Abs(shown.yMax - top) < 1
        && InsideScreen(shown), what + ": the column stands where the text editor's does: " + shown);
      Assert(preview.xMax <= shown.xMin - 20 && InsideScreen(preview), what + ": the preview keeps left of it: " + preview + " / " + shown);
    }

    // A tool a row opened, as the column shows it: the rows give way to a
    // back row naming it, and the tool hangs under it, ending with the column.
    private static void AssertModTool(ModEditorScreen editor, ModEditorColumn column, string tool)
    {
      Settle();
      AssertNoScreenErrors(tool + " opens");
      Pressable back = ByName<Pressable>(column.ToolRoot, "Tool back");
      Assert(column.OpenTool == tool && !Shown(column.Root) && back != null && Shown(back)
        && back.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == tool), "the column shows a back row naming " + tool);
      Rect backRect = ScreenRectOf((RectTransform)back.transform), toolRect = ScreenRectOf(column.ToolRoot);
      Assert(Mathf.Abs(toolRect.xMax - (column.Area.xMax * Field<EditorFramePanel>(editor, "_frame").Scale)) < 1
        && Mathf.Abs(backRect.yMax - toolRect.yMax) < 1 && InsideScreen(toolRect), tool + " opens in the column under its back row: " + toolRect);
    }

    private static void PressToolBack(ModEditorScreen editor, ModEditorColumn column)
    {
      Press(ByName<Pressable>(column.ToolRoot, "Tool back"));
      Call(editor, "ApplyPending");
      Settle();
      Assert(column.OpenTool == null && Shown(column.Root), "back brings the rows again");
      SettlePreview(editor);
    }

    // The preview glides back beside the column; the fixture's clock stands still, so it arrives at once.
    private static void SettlePreview(ModEditorScreen editor)
    {
      Field<EditorGlide>(editor, "_space").Snap();
      Call(editor, "FitPreview");
    }

    private static string PathOf(Transform transform)
    {
      string path = transform.name + (transform.gameObject.activeSelf ? "" : "(off)");
      for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        path = parent.name + (parent.gameObject.activeSelf ? "" : "(off)") + "/" + path;
      return path;
    }

    private static Rect ModPreviewRect(ModEditorScreen editor) =>
      ScreenRectOf(Field<RectTransform>(Field<ModPreview>(editor, "_preview"), "_holder"));

    // The parts shown now stand in the column, keep clear of the preview and
    // of each other, and their words fit. Each part is found by the object
    // its Rect gives a mod, spread over it; a card or tab page as a whole.
    private static void CheckVisibleParts(ModEditorScreen editor, string name, string tab)
    {
      ModEditorPage page = editor.Page;
      Rect preview = ModPreviewRect(editor);
      List<ModPart> shownParts = new List<ModPart>();
      foreach (ModPart item in page.Items)
        if (item is ModGroup { Tabs: not null } tabPage)
        {
          if (tabPage.Shown)
            shownParts.AddRange(tabPage.Children);
        }
        else
          shownParts.Add(item);
      RectTransform[] parts = shownParts.Select(part => part.Rect).Concat(page.Placed.Select(part => part.Rect)).ToArray();
      Call(editor, "ApplyPending");
      Settle();
      AssertNoScreenErrors("the " + tab + " tab shows");
      Rect column = ScreenRectOf(page.Column.Root);
      Assert(parts.Length >= 3, "the " + tab + " tab shows its parts: " + parts.Length);
      foreach (RectTransform part in parts)
      {
        Rect rect = ScreenRectOf(part);
        Assert(part.gameObject.activeInHierarchy, tab + ": part is shown: " + part.name + " in " + PathOf(part));
        Assert(InsideScreen(rect), tab + ": part fits the screen: " + part.name + " " + rect);
        Assert(!rect.Overlaps(preview), tab + ": part leaves the preview clear: " + part.name);
      }
      foreach (ModPart part in shownParts)
      {
        Rect rect = ScreenRectOf(part.Rect);
        Assert(rect.xMin >= column.xMin - .5f && rect.xMax <= column.xMax + .5f, tab + ": part stands in the column: " + part.Rect.name + " " + rect + " / " + column);
      }
      for (int i = 0; i < parts.Length; i++)
        for (int j = i + 1; j < parts.Length; j++)
          Assert(!ScreenRectOf(parts[i]).Overlaps(ScreenRectOf(parts[j])), tab + ": parts don't overlap: " + parts[i].name + " / " + parts[j].name);
      foreach (TextMeshProUGUI label in page.Column.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, tab + ": label fits: " + label.text + " width " + label.rectTransform.rect.width + " needs " + label.GetPreferredValues(label.text, 1000, 36).x + " font " + (label.font != null ? label.font.name : "none") + " size " + label.fontSize);
      }
    }
  }

  // The preview lists its element's mod itself, without UMM.
  internal sealed class PreviewModAssemblies : IModAssemblies
  {
    private readonly List<ModAssembly> _mods = new List<ModAssembly>();

    public int Version { get; private set; }

    public IReadOnlyList<ModAssembly> Current => _mods;

    public List<ElementClass> FindElements(ModAssembly mod, Action<string> report) =>
      ElementClasses.Find(mod.Id, mod.Assembly, report);

    public void Add(ModAssembly mod)
    {
      _mods.Add(mod);
      Version++;
    }

    public void Remove(string id)
    {
      if (_mods.RemoveAll(mod => mod.Id == id) > 0)
        Version++;
    }
  }
}
