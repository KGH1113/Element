using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Infrastructure.Mods;
using Element.Presentation.Layout;
using Element.Presentation.Layout.Canvas;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Layout.ContentEditors.Mods;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Settings;
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
      page.Label(page.L("직접 놓은 라벨", "Placed by hand")).At(page.Size.x - 232, page.PreviewArea.y).Width(200);
    }
  }

  [Serializable]
  public sealed class PreviewMeterSettings
  {
    public Color Fill = new Color(.36f, .72f, 1f);
    public float Width = 240;
    public bool Rounded = true;
    public string Label = "Meter";
    public Anchor LabelAt = Anchor.TopLeft;
  }

  // The same kind of element with its editing screen written with ElementUI:
  // controls bound to its settings and Element's own pickers.
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
      Size = new Vector2(s.Width, 64);
      ElementKit.Drawing.Place(_label.rectTransform, 0, 0, s.Width, 28);
      _label.text = s.Label;
      _label.alignment = s.LabelAt == Anchor.Top ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
      ElementKit.Drawing.Place(_bar.Rect, 0, 32, s.Width, 32);
      _bar.Color = s.Fill;
      _bar.Rounding = s.Rounded ? 16 : 0;
    }

    protected override ElementUI.View Editor(EditorViewModel<PreviewMeterSettings> vm) =>
      UI.VStack(
        12,
        EditorParts.ColorPicker(vm.L("채우기", "Fill"), UI.Bind(() => vm.Settings.Fill)),
        UI.Slider(vm.L("너비", "Width"), UI.Bind(() => vm.Settings.Width), 80, 480, step: 1),
        UI.Toggle(vm.L("둥근 모서리", "Rounded"), UI.Bind(() => vm.Settings.Rounded))
          .Hint(vm.L("막대 양 끝을 둥글게 깎아요", "Rounds both ends of the bar")),
        UI.TextField(vm.L("이름", "Label"), UI.Bind(() => vm.Settings.Label)),
        EditorParts.AnchorPicker(vm.L("이름 위치", "Label at"), UI.Bind(() => vm.Settings.LabelAt))
      );
  }

  // Elements other mods draw: Add Element → More, the editing screen built
  // from Element's parts or written with ElementUI, the preview following
  // each change, undo, Done, and the box shown while the mod is missing.
  public static partial class SettingsPreview
  {
    private static void CheckModElements(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      PreviewModAssemblies mods = new PreviewModAssemblies();
      List<string> warnings = new List<string>();
      mods.Add(new ModAssembly("Preview", typeof(PreviewCard).Assembly, () => true));
      host.ModElements = new ModElements(mods, warnings.Add);
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
        OverlayModEditor mods2 = (OverlayModEditor)Kind<ModKind>(editor).Editor;
        Assert(element.Enabled && element.Drawn && mods2.IsOpen && mods2.Page != null && mods2.View == null,
          "a new element opens its editing screen at once, its parts on a page");
        Settle();
        string Working() => Field<string>(mods2, "_working");
        // A part's control, found by the name its part gives it, shown now.
        Pressable Find(string prefix) => mods2.Page.Root.GetComponentsInChildren<Pressable>(false).First(button => button.name.StartsWith(prefix));
        Pressable Option(string label) => mods2.Page.Root.GetComponentsInChildren<Pressable>(false).First(button => button.name == "Option " + label);
        // What the editor does at the start of the next frame: a button's parts made again.
        void Next()
        {
          Call(mods2, "ApplyPending");
          Settle();
        }

        // Tab 1: colors in a card, a slider, a switch, a choice and the nine points.
        CheckVisibleParts(mods2, name, "look");
        SaveFrame(camera, texture, canvases, name + "-mod-editor");

        // Grow from on its bar, as in the text and shape editors: its popover hangs under it.
        CheckEditorTopLine(mods2, "Mod element editor", L("미리보기 카드 편집", "Edit Preview card"));
        OverlayBarButtonModel modGrow = Field<OverlayBarButtonModel>(mods2, "_growButton");
        OverlayGrowth growth = Field<OverlayGrowth>(mods2, "_growth");
        Assert(modGrow.Value == OverlayGrowPickerModel.ShortName(growth), "the bar's Grow from names the element's point: " + modGrow.Value);
        PressBar(mods2, modGrow);
        Call(mods2, "FitPreview");
        OverlayGrowPopover modGrowPopover = Field<OverlayGrowPopover>(mods2, "_grow");
        Assert(modGrowPopover.Visible && modGrow.Active && ScreenRectOf(modGrowPopover.Root).yMax <= BarButtonRect(mods2, modGrow).yMin
          && InsideScreen(ScreenRectOf(modGrowPopover.Root)), "Grow from opens its popover under its button, lit while it is open");
        Settle();
        Press(modGrowPopover.Root.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.BottomRight));
        Call(mods2, "FitPreview");
        Assert(Field<OverlayGrowth>(mods2, "_growth") == OverlayGrowth.BottomRight && modGrow.Value == OverlayGrowPickerModel.ShortName(OverlayGrowth.BottomRight)
          && modGrowPopover.Caption.Contains(L("오른쪽 아래", "bottom right")), "a point picks where the element grows from");
        mods2.Escape();
        Call(mods2, "FitPreview");
        Assert(mods2.IsOpen && !modGrowPopover.Visible && !modGrow.Active, "Esc closes the popover before the editor");
        Call(mods2, "PickGrowth", growth);
        Press(Find("Switch "));
        Assert(Working().Contains("\"Rounded\":false"), "a switch changes the setting: " + Working());
        ByName<SliderDrag>(mods2.Page.Root, "Slider " + L("진행도", "Progress")).Moved(.25f, true);
        Assert(Working().Contains("\"Progress\":0.25"), "a slider changes the setting as it moves: " + Working());
        Press(Option(L("강조", "Accent")));
        Assert(Working().Contains("\"Style\":1"), "a choice changes the setting");
        Press(mods2.Page.Root.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.Top));
        Assert(Working().Contains("\"TitleAt\":1"), "the nine-point picker changes the setting");
        Press(Find("Paint "));
        Settle();
        RectTransform paint = ByName<RectTransform>(mods2.Page.Root, "Color picker");
        Assert(Shown(paint) && ByName<RectTransform>(paint, "Gradient kinds") != null && InsideScreen(ScreenRectOf(paint)),
          "a paint opens the full picker, gradients included, on screen");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-paint");
        Call(mods2, "ClosePopovers");
        Settle();
        Assert(!Shown(ByName<RectTransform>(mods2.Page.Root, "Color picker")), "and closes with the editor's popovers");

        // Tab 2: words, a font, a dropdown, chips and a list.
        Press(Option(L("내용", "Content")));
        Settle();
        CheckVisibleParts(mods2, name, "content");
        TMP_InputField title = mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).First(input => input.name.StartsWith("Text "));
        title.text = "Hello";
        Assert(Working().Contains("\"Title\":\"Hello\""), "a text field changes the setting as it is typed");
        Pressable unit = Find("Dropdown ");
        Press(unit);
        Settle();
        // The list opens on the page's popover layer.
        Pressable[] options = ByName<RectTransform>(mods2.Page.Root, "Overlays").GetComponentsInChildren<Pressable>(false)
          .Where(row => row.GetComponentInChildren<TextMeshProUGUI>() != null).ToArray();
        Assert(options.Length == 5 && mods2.Page.HasPopover && options.All(row => InsideScreen(ScreenRectOf((RectTransform)row.transform))),
          "a dropdown opens its list beside it: " + options.Length);
        SaveFrame(camera, texture, canvases, name + "-mod-editor-dropdown");
        Press(options.Single(row => row.GetComponentInChildren<TextMeshProUGUI>().text == "BPM"));
        Settle();
        Assert(!mods2.Page.HasPopover && Working().Contains("\"Unit\":2") && unit.GetComponentInChildren<TextMeshProUGUI>().text == "BPM",
          "picking an option saves it, shows it and closes the list");
        Press(Find("Chip "));
        Assert(Working().Contains("\"Show\":[false,false,true]"), "a chip turns its option on or off: " + Working());
        Press(Find("Font "));
        Settle();
        RectTransform fonts = ByName<RectTransform>(mods2.Page.Root, "Font popover");
        Assert(Shown(fonts) && InsideScreen(ScreenRectOf(fonts)), "a font part opens Element's font list beside it");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-font");
        Call(mods2, "ClosePopovers");
        Settle();
        Press(Find("List add"));
        Assert(Working().Contains("\"Words\":[\"44\",\"32\",\"new\"]"), "a list adds an item: " + Working());
        Next();
        Assert(mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).Count(input => input.name.StartsWith("List item")) == 3,
          "the parts are made again with the new item, the same tab still picked");
        Press(Find("List Remove"));
        Assert(Working().Contains("\"Words\":[\"32\",\"new\"]"), "a list removes an item");
        Next();

        // Tab 3: numbers, a stepper, a range, a shortcut, a checkbox with a hint, an icon button.
        Press(Option(L("고급", "More")));
        Settle();
        CheckVisibleParts(mods2, name, "more");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-more");
        TMP_InputField widthField = mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).First(input => input.name.StartsWith("Number "));
        CheckInputCaret(widthField, "mod number", "", "480", "960");
        widthField.text = "500";
        Assert(Working().Contains("\"Width\":500"), "a number shows as it is typed");
        widthField.text = "5000";
        widthField.onEndEdit.Invoke("5000");
        Settle();
        Assert(Working().Contains("\"Width\":960") && widthField.text == "960", "and is kept within its limits when typing is done: " + widthField.text);
        Press(Find("Step up "));
        Assert(Working().Contains("\"Bars\":2"), "a stepper steps");
        SliderDrag range = ByName<SliderDrag>(mods2.Page.Root, "Range slider");
        range.Moved(.9f, true);
        Assert(Working().Contains("\"From\":10") && Working().Contains("\"To\":90"), "a range moves its nearer handle: " + Working());
        range.Moved(.05f, true);
        Assert(Working().Contains("\"From\":5"), "and the other");
        Press(Find("Shortcut "));
        Assert(mods2.Page.Typing, "a shortcut part records the next key");
        ShortcutFieldModel shortcut = Field<List<ShortcutFieldModel>>(Field<ModEditorContext>(mods2, "_context"), "_shortcuts").Single();
        shortcut.TryAssign(KeyCode.H, KeyCode.None);
        Assert(!mods2.Page.Typing && Working().Contains("\"Hide\":{\"Key\":104"), "the key it records is the setting, and recording stops: " + Working());
        Pressable box = Find("Checkbox ");
        Press(box);
        Assert(Working().Contains("\"Always\":true"), "a checkbox ticks");
        PointerArea hover = box.GetComponentInParent<PointerArea>();
        hover.OnPointerEnter(null);
        TextMeshProUGUI tip = ByName<TextMeshProUGUI>(ByName<RectTransform>(mods2.Page.Root, "ElementUI hint"), "Text");
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
        Assert(Shown(ByName<SliderDrag>(mods2.Page.Root, "Range slider")), "after undo the same tab is still picked");

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
        host.ModElements = null;
      }
    }

    // An element whose editing screen is written with ElementUI: its views
    // under the preview, bound to its settings; each edit an undo step (a
    // drag one step), undo shown on the screen, and Done saving it.
    private static void CheckModElementView(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, OverlayModEditor mods)
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
      RectTransform view = mods.View.Root;
      Rect preview = ScreenRectOf(Field<RectTransform>(Field<ModPreview>(mods, "_preview"), "_holder"));
      Rect shown = ScreenRectOf(view);
      Assert(InsideScreen(shown) && !shown.Overlaps(preview), "its screen sits under the preview, on screen");
      foreach (Pressable control in view.GetComponentsInChildren<Pressable>(false))
        Assert(InsideScreen(ScreenRectOf((RectTransform)control.transform)), "its controls fit the screen: " + control.name);
      Assert(Shown(ByName<Pressable>(view, "Color " + L("채우기", "Fill"))), "Element's color picker draws on it");
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view");

      // A hint shows next to its control in the column, not at the screen's left edge.
      PointerArea rounded = ByName<Pressable>(view, "Switch").GetComponentInParent<PointerArea>();
      rounded.OnPointerEnter(null);
      Settle();
      TextMeshProUGUI roundedTip = ByName<TextMeshProUGUI>(ByName<RectTransform>(view, "ElementUI hint"), "Text");
      Assert(Shown(roundedTip) && roundedTip.text == L("막대 양 끝을 둥글게 깎아요", "Rounds both ends of the bar"),
        "a control's hint shows on hover");
      string misplaced = HintProblem(rounded);
      Assert(misplaced == null, "a control's hint shows beside it, in the column: " + misplaced);
      SaveFrame(camera, texture, canvases, name + "-mod-editor-view-hint");
      rounded.OnPointerExit(null);
      Settle();

      SliderDrag slider = ByName<SliderDrag>(view, "Slider");
      slider.Dragging(true);
      slider.Moved(.5f, true);
      slider.Moved(.75f, false);
      slider.Dragging(false);
      Assert(Working().Contains("\"Width\":380"), "a slider bound to the settings changes them as it moves: " + Working());
      Press(ByName<Pressable>(view, "Switch"));
      Assert(Working().Contains("\"Rounded\":false"), "a switch bound to the settings flips them");
      Press(view.GetComponentsInChildren<Pressable>(true).Single(point => point.name == "Grow from " + OverlayGrowth.Top));
      Assert(Working().Contains("\"LabelAt\":1"), "Element's nine-point picker sets them too");
      TMP_InputField label = view.GetComponentsInChildren<TMP_InputField>(false).Single();
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
      Call(mods, "Undo");
      Call(mods, "Undo");
      Settle();
      Assert(Working().Contains("\"Rounded\":true") && Working().Contains("\"Width\":380"), "each click is its own step");
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

    // The parts shown now fit the screen, keep clear of the preview and of
    // each other, and their words fit. Each part is found by the object its
    // Rect gives a mod, spread over it.
    private static void CheckVisibleParts(OverlayModEditor editor, string name, string tab)
    {
      ModEditorPage page = editor.Page;
      Rect preview = ScreenRectOf(Field<RectTransform>(Field<ModPreview>(editor, "_preview"), "_holder"));
      ModPart[] shown = page.Flowing.Where(part => part.Column.Value >= 0 && !(part is ModGroup { Transparent: true }))
        .Concat(page.Placed).ToArray();
      RectTransform[] parts = shown.Select(part => part.Rect).ToArray();
      Call(editor, "ApplyPending");
      Settle();
      Assert(parts.Length >= 3, "the " + tab + " tab shows its parts: " + parts.Length);
      foreach (RectTransform part in parts)
      {
        Rect rect = ScreenRectOf(part);
        Assert(part.gameObject.activeInHierarchy, tab + ": part is shown: " + part.name);
        Assert(InsideScreen(rect), tab + ": part fits the screen: " + part.name + " " + rect);
        Assert(!rect.Overlaps(preview), tab + ": part leaves the preview clear: " + part.name);
      }
      for (int i = 0; i < parts.Length; i++)
        for (int j = i + 1; j < parts.Length; j++)
          Assert(!ScreenRectOf(parts[i]).Overlaps(ScreenRectOf(parts[j])), tab + ": parts don't overlap: " + parts[i].name + " / " + parts[j].name);
      foreach (TextMeshProUGUI label in page.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
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
