using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Presentation.Layout;
using Element.Presentation.Layout.Canvas;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Layout.ContentEditors.Mods;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Editor
{
  [Serializable]
  public sealed class PreviewCardSettings
  {
    public string Title = "Sample";
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

  // Elements other mods draw: Add Element → More, the editing screen built
  // from Element's parts, the preview following each change, undo, Done,
  // and the box shown while the mod is missing.
  public static partial class SettingsPreview
  {
    private static void CheckModElements(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      ListedModAssemblies mods = new ListedModAssemblies();
      List<string> warnings = new List<string>();
      mods.Add(new ModAssembly("Preview", typeof(PreviewCard).Assembly, () => true));
      host.ModElements = new ModElements(mods, warnings.Add);
      try
      {
        Call(editor, "ToggleAddMenu");
        RectTransform menu = AddMenu(editor);
        Button[] rows = menu.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Add ")).ToArray();
        Assert(rows.Length == 4 && rows[3].name == "Add Extension" && rows[3].interactable, "Add Element gains More once a mod declares an element");
        Assert(warnings.Count == 0, "the preview element is found without problems: " + string.Join(" | ", warnings));
        rows[3].onClick.Invoke();
        RectTransform list = Field<ListMenu>(Kind<ModKind>(editor), "_menu").Root;
        Assert(list != null && list.GetComponentsInChildren<Button>(true).Select(button => button.name).SequenceEqual(new[] { "Add Preview/card" }),
          "More lists each element other mods declare");
        SaveFrame(camera, texture, canvases, name + "-mod-types");

        list.GetComponentsInChildren<Button>(true).Single().onClick.Invoke();
        ModOverlayElement element = host.LayoutTargets.OfType<ModOverlayElement>().Last();
        OverlayModEditor mods2 = (OverlayModEditor)Kind<ModKind>(editor).Editor;
        Assert(element.Enabled && element.Drawn && mods2.IsOpen, "a new element opens its editing screen at once");
        string Working() => Field<string>(mods2, "_working");
        Button Find(string prefix) => mods2.Page.Root.GetComponentsInChildren<Button>(true).First(button => button.name.StartsWith(prefix));
        Button Tab(string label) => mods2.Page.Root.GetComponentsInChildren<Button>(true).First(button => button.name == label);

        // Tab 1: colors in a card, a slider, a switch, a choice and the nine points.
        CheckVisibleParts(mods2, name, "look");
        SaveFrame(camera, texture, canvases, name + "-mod-editor");
        Find("Switch ").onClick.Invoke();
        Assert(Working().Contains("\"Rounded\":false"), "a switch changes the setting: " + Working());
        mods2.Page.Root.GetComponentsInChildren<Slider>(true).First().value = .25f;
        Assert(Working().Contains("\"Progress\":0.25"), "a slider changes the setting as it moves");
        Tab(Korean(name) ? "강조" : "Accent").onClick.Invoke();
        Assert(Working().Contains("\"Style\":1"), "a choice changes the setting");
        Tab("Grow from " + OverlayGrowth.Top).onClick.Invoke();
        Assert(Working().Contains("\"TitleAt\":1"), "the nine-point picker changes the setting");
        Find("Paint ").onClick.Invoke();
        SettingsColorPicker paint = Field<PickerPopover>(Field<ModEditorPopovers>(mods2, "_popovers"), "_paintPicker").Picker;
        Assert(paint.Visible && paint.GradientAvailable && InsideScreen(ScreenRectOf(paint.Root)),
          "a paint opens the full picker, gradients included, on screen");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-paint");
        Call(mods2, "ClosePopovers");

        // Tab 2: words, a font, a dropdown, chips and a list.
        Tab(Korean(name) ? "내용" : "Content").onClick.Invoke();
        CheckVisibleParts(mods2, name, "content");
        TMP_InputField title = mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).First(input => input.name.StartsWith("Text "));
        title.text = "Hello";
        Assert(Working().Contains("\"Title\":\"Hello\""), "a text field changes the setting as it is typed");
        Find("Dropdown ").onClick.Invoke();
        ModListPopover dropdown = Field<ModListPopover>(Field<ModEditorPopovers>(mods2, "_popovers"), "_list");
        Assert(dropdown.Visible && InsideScreen(ScreenRectOf(dropdown.Root)), "a dropdown opens its list under it");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-dropdown");
        dropdown.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Option 3").onClick.Invoke();
        Assert(!dropdown.Visible && Working().Contains("\"Unit\":2"), "picking an option saves it and closes the list");
        Find("Chip ").onClick.Invoke();
        Assert(Working().Contains("\"Show\":[false,false,true]"), "a chip turns its option on or off: " + Working());
        Find("Font ").onClick.Invoke();
        OverlayFontPopover fonts = Field<OverlayFontPopover>(Field<ModEditorPopovers>(mods2, "_popovers"), "_fontPopover");
        Assert(fonts != null && fonts.Visible && InsideScreen(ScreenRectOf(fonts.Root)), "a font part opens Element's font list beside it");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-font");
        Call(mods2, "ClosePopovers");
        Find("List add").onClick.Invoke();
        Assert(Working().Contains("\"Words\":[\"44\",\"32\",\"new\"]"), "a list adds an item: " + Working());
        Assert(mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).Count(input => input.name.StartsWith("List item")) == 3,
          "the parts are made again with the new item, the same tab still picked");
        Find("List Remove").onClick.Invoke();
        Assert(Working().Contains("\"Words\":[\"32\",\"new\"]"), "a list removes an item");

        // Tab 3: numbers, a stepper, a range, a shortcut, a checkbox with a hint, an icon button.
        Tab(Korean(name) ? "고급" : "More").onClick.Invoke();
        CheckVisibleParts(mods2, name, "more");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-more");
        TMP_InputField widthField = mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(false).First(input => input.name.StartsWith("Number "));
        CheckInputCaret(widthField, "mod number", "", "480", "960");
        widthField.text = "500";
        Assert(Working().Contains("\"Width\":500"), "a number shows as it is typed");
        widthField.text = "5000";
        widthField.onEndEdit.Invoke("5000");
        Assert(Working().Contains("\"Width\":960") && widthField.text == "960", "and is kept within its limits when typing is done");
        Find("Step up ").onClick.Invoke();
        Assert(Working().Contains("\"Bars\":2"), "a stepper steps");
        ModRangeDrag range = mods2.Page.Root.GetComponentsInChildren<ModRangeDrag>(true).Single();
        range.Moved(.9f, true);
        Assert(Working().Contains("\"From\":10") && Working().Contains("\"To\":90"), "a range moves its nearer handle: " + Working());
        range.Moved(.05f, true);
        Assert(Working().Contains("\"From\":5"), "and the other");
        Find("Shortcut ").onClick.Invoke();
        Assert(mods2.Page.Typing, "a shortcut part records the next key");
        Find("Shortcut ").onClick.Invoke();
        Assert(!mods2.Page.Typing, "and stops on a second click");
        Button box = Find("Checkbox ");
        box.onClick.Invoke();
        Assert(Working().Contains("\"Always\":true"), "a checkbox ticks");
        box.transform.parent.GetComponent<ModHint>().OnPointerEnter(null);
        Assert(Field<OverlayEditorFrame>(mods2, "_screen").TipText == (Korean(name) ? "메뉴에서도 카드를 보여요" : "Shows the card in menus too"),
          "a part's hint shows on hover");
        box.transform.parent.GetComponent<ModHint>().OnPointerExit(null);
        Find("Icon Reset").onClick.Invoke();
        Assert(Working().Contains("\"Rounded\":true") && Working().Contains("\"Title\":\"Sample\""), "an icon button runs its action");

        // Undo keeps the picked tab.
        Call(mods2, "Undo");
        Assert(Working().Contains("\"Rounded\":false"), "undo brings the last change back");
        Assert(mods2.Page.Root.GetComponentsInChildren<ModRangeDrag>(false).Length == 1, "after undo the same tab is still picked");

        mods2.Close(true, true);
        Assert(!mods2.IsOpen && element.Settings.Mod.Data.Contains("\"Style\":1") && element.Settings.Mod.Data.Contains("\"Unit\":2"),
          "Done saves the settings with the element");
        Select(editor, IndexOf(editor, element));
        SaveFrame(camera, texture, canvases, name + "-mod-placed");

        // The mod goes away: the element keeps its place and settings.
        mods.Remove("Preview");
        host.TickLayoutPreview();
        Assert(!element.Drawn && element.Enabled && element.Settings.Mod.Data.Contains("\"Style\":1"), "without its mod the element stays, settings kept");
        Assert(element.Label == (Korean(name) ? "미리보기 카드" : "Preview card"), "it keeps its name: " + element.Label);
        RectTransform elementRect = (RectTransform)typeof(Element.Presentation.Overlay.Elements.OverlayElementTarget)
          .GetProperty("Rect", Private).GetValue(element);
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

    // The parts shown now fit the screen, keep clear of the preview and of each other, and their words fit.
    private static void CheckVisibleParts(OverlayModEditor editor, string name, string tab)
    {
      ModEditorPage page = editor.Page;
      Rect preview = ScreenRectOf(Field<RectTransform>(Field<ModPreview>(editor, "_preview"), "_holder"));
      // A tab page at the top of the column is only a holder; its parts stand in the column themselves.
      IEnumerable<RectTransform> Children(Transform parent) => Enumerable.Range(0, parent.childCount)
        .Select(i => (RectTransform)parent.GetChild(i))
        .SelectMany(rect => rect.name.StartsWith("Tab page") ? Children(rect) : new[] { rect });
      RectTransform[] parts = Children(page.Root)
        .Where(rect => rect.gameObject.activeInHierarchy && !rect.name.StartsWith("Gap")).ToArray();
      Assert(parts.Length >= 3, "the " + tab + " tab shows its parts: " + parts.Length);
      foreach (RectTransform part in parts)
      {
        Rect rect = ScreenRectOf(part);
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
}
