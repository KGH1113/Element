using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  [Serializable]
  public sealed class PreviewCardSettings
  {
    public string Title = "Sample";
    public Color Fill = new Color(.12f, .12f, .14f, .9f);
    public Color Accent = new Color(1f, .62f, .26f);
    public float Progress = .6f;
    public bool Rounded = true;
    public int Style;
    public float Width = 360;
    public float Height = 120;
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
      float rounding = s.Rounded ? 14 : 0;
      ElementKit.Drawing.Place(_background.Rect, 0, 0, s.Width, s.Height);
      _background.Color = s.Fill;
      _background.Rounding = rounding;
      ElementKit.Drawing.Place(_title.rectTransform, 20, 14, s.Width - 40, 36);
      _title.text = s.Title;
      _title.color = s.Style == 1 ? s.Accent : Color.white;
      float barTop = s.Height - 34;
      ElementKit.Drawing.Place(_track.Rect, 20, barTop, s.Width - 40, 12);
      _track.Color = new Color(1, 1, 1, .15f);
      _track.Rounding = 6;
      ElementKit.Drawing.Place(_fill.Rect, 20, barTop, Mathf.Max(1, (s.Width - 40) * s.Progress), 12);
      _fill.Color = s.Accent;
      _fill.Rounding = 6;
    }

    protected override void OnEdit(EditorPage page)
    {
      page.Label(page.L("카드 한 장과 진행도 막대예요.", "A card with a progress bar."));
      page.TextField(page.L("제목", "Title"), Settings.Title, text => Settings.Title = text);
      page.ColorPicker(page.L("배경", "Background"), Settings.Fill, color => Settings.Fill = color);
      page.ColorPicker(page.L("강조 색", "Accent"), Settings.Accent, color => Settings.Accent = color, opacity: false);
      page.Slider(page.L("진행도", "Progress"), Settings.Progress, 0, 1, value => Settings.Progress = value);
      page.Switch(page.L("둥근 모서리", "Rounded"), Settings.Rounded, on => Settings.Rounded = on);
      page.Choice(page.L("제목 색", "Title color"), new[] { page.L("흰색", "White"), page.L("강조", "Accent") }, Settings.Style, index => Settings.Style = index);
      page.Gap();
      page.Number(page.L("너비", "Width"), Settings.Width, 120, 960, value => Settings.Width = value);
      page.Number(page.L("높이", "Height"), Settings.Height, 60, 400, value => Settings.Height = value);
      page.Button(page.L("처음 모양으로", "Start over"), () =>
      {
        PreviewCardSettings fresh = new PreviewCardSettings();
        Settings.Title = fresh.Title;
        Settings.Rounded = fresh.Rounded;
      });
      // A part of its own, out of the column: top right, under the bar.
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
        RectTransform menu = Field<RectTransform>(editor, "_addMenu");
        Button[] rows = menu.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Add ")).ToArray();
        Assert(rows.Length == 4 && rows[3].name == "Add Extension" && rows[3].interactable, "Add Element gains More once a mod declares an element");
        Assert(warnings.Count == 0, "the preview element is found without problems: " + string.Join(" | ", warnings));
        foreach (TextMeshProUGUI label in menu.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          label.ForceMeshUpdate();
          Assert(!label.isTextTruncated, "add menu label fits: " + label.text);
        }
        rows[3].onClick.Invoke();
        RectTransform list = Field<RectTransform>(editor, "_modMenu");
        Assert(list != null && list.GetComponentsInChildren<Button>(true).Select(button => button.name).SequenceEqual(new[] { "Add Preview/card" }),
          "More lists each element other mods declare");
        Rect addRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { menu });
        Rect listRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { list });
        Assert(PopoverInside(editor, list, width, height) && (listRect.x >= addRect.xMax - .5f || listRect.xMax <= addRect.x + .5f)
          && !listRect.Overlaps(editor.ToolbarBounds), "the list sits beside the add menu, clear of the toolbar");
        SaveFrame(camera, texture, canvases, name + "-mod-types");

        list.GetComponentsInChildren<Button>(true).Single().onClick.Invoke();
        ModOverlayElement element = host.LayoutTargets.OfType<ModOverlayElement>().Last();
        OverlayModEditor mods2 = Field<OverlayModEditor>(editor, "_modEditor");
        Assert(element.Enabled && element.Drawn && mods2.IsOpen, "a new element opens its editing screen at once");
        Assert(Mathf.Abs(element.Settings.Mod.Width - 360) < .01f && Mathf.Abs(element.Settings.Mod.Height - 120) < .01f,
          "the element takes the size the mod set: " + element.Settings.Mod.Width + "x" + element.Settings.Mod.Height);
        CheckPage(mods2, name);
        SaveFrame(camera, texture, canvases, name + "-mod-editor");

        ModEditorPage page = mods2.Page;
        RectTransform pageRoot = page.Root;
        Button Find(string prefix) => pageRoot.GetComponentsInChildren<Button>(true).First(button => button.name.StartsWith(prefix));
        string Working() => Field<string>(mods2, "_working");

        Find("Switch ").onClick.Invoke();
        Assert(Working().Contains("\"Rounded\":false"), "a switch changes the setting: " + Working());
        Slider slider = pageRoot.GetComponentsInChildren<Slider>(true).First();
        slider.value = .25f;
        Assert(Working().Contains("\"Progress\":0.25"), "a slider changes the setting as it moves: " + Working());
        SettingsSegmentedControlButton(pageRoot, Korean(name) ? "강조" : "Accent").onClick.Invoke();
        Assert(Working().Contains("\"Style\":1"), "a choice changes the setting");
        TMP_InputField[] inputs = pageRoot.GetComponentsInChildren<TMP_InputField>(true);
        TMP_InputField title = inputs.First(input => input.name.StartsWith("Text "));
        title.text = "Hello";
        title.onEndEdit.Invoke("Hello");
        Assert(Working().Contains("\"Title\":\"Hello\""), "a text field changes the setting when typing is done");
        TMP_InputField widthField = inputs.First(input => input.name.StartsWith("Number "));
        widthField.text = "5000";
        widthField.onEndEdit.Invoke("5000");
        Assert(Working().Contains("\"Width\":960") && widthField.text == "960", "a number past its limit is kept within it");
        RectTransform previewRoot = Field<RectTransform>(mods2, "_previewRoot");
        Assert(Mathf.Abs(previewRoot.sizeDelta.x - 960) < .01f, "the preview follows each change");
        Canvas.ForceUpdateCanvases();
        Assert(previewRoot.GetComponentsInChildren<Element.Presentation.Overlay.Elements.ShapeGraphic>(false)
            .Any(box => Mathf.Abs(Field<OverlayShapeSettings>(box, "_shape").Width - 960) < .01f),
          "the card's box is drawn at the new width in the same frame");
        Rect previewArea = page.PreviewArea;
        Rect previewOnScreen = ScreenRectOf(Field<RectTransform>(mods2, "_previewHolder"));
        Assert(InsideScreen(previewOnScreen), "the wider preview still fits the screen: " + previewOnScreen);

        Find("Color ").onClick.Invoke();
        SettingsColorPicker picker = Field<SettingsColorPicker>(mods2, "_picker");
        Assert(picker.Visible && InsideScreen(ScreenRectOf(picker.Root)), "a color part opens the picker beside it, on screen");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-color");
        picker.Hide();

        Call(mods2, "Undo");
        Assert(Working().Contains("\"Width\":360"), "undo brings the last change back: " + Working());
        TMP_InputField widthAgain = mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(true).First(input => input.name.StartsWith("Number "));
        Assert(widthAgain.text == "360", "after undo the parts show the restored settings");
        Find2(mods2, "Button ").onClick.Invoke();
        Assert(Working().Contains("\"Title\":\"Sample\"") && Working().Contains("\"Rounded\":true"), "a button changes settings");
        Assert(mods2.Page.Root.GetComponentsInChildren<TMP_InputField>(true).First(input => input.name.StartsWith("Text ")).text == "Sample",
          "after a button the parts show what it changed");

        mods2.Close(true, true);
        Assert(!mods2.IsOpen && element.Settings.Mod.Data.Contains("\"Style\":1") && element.Settings.Mod.Data.Contains("\"Progress\":0.25"),
          "Done saves the settings with the element");
        Call(editor, "Undo");
        Assert(!element.Enabled, "adding and editing it is one undo step");
        Call(editor, "Redo");
        Assert(element.Enabled && element.Settings.Mod.Data.Contains("\"Style\":1"), "and redo brings it back");
        Select(editor, IndexOf(editor, element));
        SaveFrame(camera, texture, canvases, name + "-mod-placed");

        // The mod goes away: the element keeps its place and settings.
        mods.Remove("Preview");
        host.TickLayoutPreview();
        Assert(!element.Drawn && element.Enabled && element.Settings.Mod.Data.Contains("\"Style\":1"),
          "without its mod the element stays, settings kept");
        Assert(element.Label == (Korean(name) ? "미리보기 카드" : "Preview card"), "it keeps its name: " + element.Label);
        RectTransform elementRect = (RectTransform)typeof(Element.Presentation.Overlay.Elements.OverlayElementTarget)
          .GetProperty("Rect", Private).GetValue(element);
        Assert(elementRect.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text.Contains("Preview") && label.font != null),
          "the editor names the mod it needs, in the overlay's font");
        SaveFrame(camera, texture, canvases, name + "-mod-missing");
        Call(editor, "EditSelected");
        Assert(!mods2.IsOpen && Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("Preview"),
          "editing it says what to install instead of opening");
        mods.Add(new ModAssembly("Preview", typeof(PreviewCard).Assembly, () => true));
        host.TickLayoutPreview();
        Assert(element.Drawn && element.Settings.Mod.Data.Contains("\"Style\":1"), "installing the mod again brings the element back as it was");
        Select(editor, -1);
      }
      finally
      {
        host.ModElements = null;
      }
    }

    private static Button Find2(OverlayModEditor editor, string prefix) =>
      editor.Page.Root.GetComponentsInChildren<Button>(true).First(button => button.name.StartsWith(prefix));

    private static Button SettingsSegmentedControlButton(RectTransform root, string label) =>
      root.GetComponentsInChildren<Button>(true).First(button => button.name == label);

    // Every part on screen, the column clear of the preview, labels fitting.
    private static void CheckPage(OverlayModEditor editor, string name)
    {
      ModEditorPage page = editor.Page;
      Assert(page != null, "the mod built its editing screen");
      RectTransform[] parts = Enumerable.Range(0, page.Root.childCount).Select(i => (RectTransform)page.Root.GetChild(i))
        .Where(rect => rect.gameObject.activeSelf && !rect.name.StartsWith("Gap")).ToArray();
      Assert(parts.Length == 11, "every part the mod made is on the screen: " + parts.Length);
      Rect preview = ScreenRectOf(Field<RectTransform>(editor, "_previewHolder"));
      foreach (RectTransform part in parts)
      {
        Rect rect = ScreenRectOf(part);
        Assert(InsideScreen(rect), "part fits the screen: " + part.name + " " + rect);
        Assert(!rect.Overlaps(preview), "part leaves the preview clear: " + part.name);
      }
      for (int i = 0; i < parts.Length; i++)
        for (int j = i + 1; j < parts.Length; j++)
          Assert(!ScreenRectOf(parts[i]).Overlaps(ScreenRectOf(parts[j])), "parts don't overlap: " + parts[i].name + " / " + parts[j].name);
      foreach (TextMeshProUGUI label in page.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "part label fits: " + label.text);
      }
      RectTransform placed = parts.Single(part => part.GetComponentsInChildren<TextMeshProUGUI>(true)
        .Any(label => label.text == (Korean(name) ? "직접 놓은 라벨" : "Placed by hand")));
      Assert(Mathf.Abs(placed.sizeDelta.x - 200) < .01f, "a part can be placed and sized by hand");
      Assert(Field<RectTransform>(editor, "_previewRoot").GetComponentsInChildren<Graphic>(false).Length >= 4,
        "the preview draws the element with its own instance");
    }
  }
}
