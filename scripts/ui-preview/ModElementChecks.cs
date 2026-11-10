using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit.Elements;
using ElementKit.Hosting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  // Sample's bars as the preview draws them: the same properties and screen,
  // with sample groups instead of a level.
  public sealed class PreviewBars : ElementType
  {
    public static readonly PreviewBars Shared = new PreviewBars();
    public static readonly ColorProperty Left = new ColorProperty("left", "왼손 색", "Left hand", "#FF9F43");
    public static readonly ColorProperty Right = new ColorProperty("right", "오른손 색", "Right hand", "#4D96FF");
    public static readonly NumberProperty Groups = new NumberProperty("groups", "묶음 수", "Groups", 16, 2, 64, NumberUnit.None);
    public static readonly NumberProperty Gap = new NumberProperty("gap", "간격", "Gap", 4, 0, 40);
    public static readonly ToggleProperty Numbers = new ToggleProperty("numbers", "키 수", "Key counts", true);
    public static readonly ChoiceProperty Grow = new ChoiceProperty("grow", "막대 방향", "Bars grow",
      new ChoiceOption("up", "아래에서 위로", "Upward"), new ChoiceOption("middle", "가운데에서", "From the middle"));
    public static readonly NumberProperty Width = new NumberProperty("width", "너비", "Width", 480, 40, 1920);
    public static readonly NumberProperty Height = new NumberProperty("height", "높이", "Height", 96, 16, 600);
    private static readonly int[] Sample = { 4, 4, 3, 2, 4, 4, 6, 2, 0, 0, 4, 4, 8, 8, 3, 3, 2, 2, 4, 4 };

    public PreviewBars() : base("bars", "예시 막대", "Sample bars") { }

    protected override void Edit(EditorLayout editor)
    {
      editor.Bar(Left, Right, Groups, Gap, Numbers, Grow);
      editor.Below(Width);
      editor.Beside(Height);
    }

    protected override Vector2 Measure(Props props) => new Vector2((float)props[Width], (float)props[Height]);

    protected override void Draw(Surface surface, Props props)
    {
      int groups = (int)props[Groups];
      float gap = (float)props[Gap], bar = (surface.Size.x - gap * (groups - 1)) / groups;
      float label = props[Numbers] ? Mathf.Min(surface.Size.y * .3f, 30) : 0, area = surface.Size.y - label;
      ElementColor left = props[Left], right = props[Right];
      ElementColor leftAhead = left.Faded(.45f), rightAhead = right.Faded(.45f);
      for (int i = 0; i < groups && i < Sample.Length; i++)
      {
        int keys = Sample[i];
        ElementColor color = i % 2 == 0 ? (i < 2 ? left : leftAhead) : (i < 2 ? right : rightAhead);
        float tall = keys <= 0 ? Mathf.Min(3, area) : Mathf.Max(3, area * Mathf.Min(keys, 8) / 8f);
        float y = props[Grow] == 1 ? (area - tall) * .5f : area - tall;
        surface.Box(new Rect(i * (bar + gap), y, bar, tall), color, Mathf.Min(6, bar * .25f));
        if (label > 0 && keys > 0)
          surface.Text(new Rect(i * (bar + gap) - 4, area, bar + 8, label), keys.ToString(), label * .8f, color);
      }
    }
  }

  // Elements other mods add: Add Element → More, their type list, their
  // editing screen built from Element's parts, and the box shown while the
  // mod is missing.
  public static partial class SettingsPreview
  {
    private static void CheckModElements(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      List<string> reports = new List<string>();
      KitRegistry.Attach("Sample", typeof(PreviewBars).Assembly, () => true, reports.Add);
      host.ModElements = new ModElements(Debug.LogWarning);
      try
      {
        Assert(reports.Count == 0, "the preview type attaches cleanly: " + string.Join(" | ", reports));
        Call(editor, "ToggleAddMenu");
        RectTransform menu = Field<RectTransform>(editor, "_addMenu");
        Button[] rows = menu.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Add ")).ToArray();
        Assert(rows.Length == 4 && rows[3].name == "Add Extension" && rows[3].interactable, "Add Element gains More once a mod offers an element");
        Assert(rows[3].GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text.Contains("1")), "More says how many elements other mods add");
        foreach (TextMeshProUGUI label in menu.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          label.ForceMeshUpdate();
          Assert(!label.isTextTruncated, "add menu label fits: " + label.text);
        }
        Assert(PopoverInside(editor, menu, width, height), "the taller add menu stays on screen");
        rows[3].onClick.Invoke();
        RectTransform list = Field<RectTransform>(editor, "_modMenu");
        Assert(list != null && list.GetComponentsInChildren<Button>(true).Select(button => button.name).SequenceEqual(new[] { "Add Sample/bars" }),
          "More lists each type other mods add");
        Rect addRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { menu });
        Rect listRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { list });
        Assert(PopoverInside(editor, list, width, height) && (listRect.x >= addRect.xMax - .5f || listRect.xMax <= addRect.x + .5f)
          && !listRect.Overlaps(editor.ToolbarBounds), "the type list sits beside the add menu, clear of the toolbar: list "
          + listRect + " add " + addRect + " toolbar " + editor.ToolbarBounds);
        foreach (TextMeshProUGUI label in list.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          label.ForceMeshUpdate();
          Assert(!label.isTextTruncated, "type list label fits: " + label.text);
        }
        Assert(list.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text == "Sample")
          && list.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text == (Korean(name) ? "예시 막대" : "Sample bars")),
          "each type shows its name and its mod");
        SaveFrame(camera, texture, canvases, name + "-mod-types");

        list.GetComponentsInChildren<Button>(true).Single().onClick.Invoke();
        ModOverlayElement element = host.LayoutTargets.OfType<ModOverlayElement>().Last();
        OverlayPropertyEditor properties = Field<OverlayPropertyEditor>(editor, "_propertyEditor");
        Assert(element.Enabled && element.Type == PreviewBars.Shared && properties.IsOpen, "a new mod element opens its editing screen at once");
        Assert(editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "the editing screen covers native viewers");
        CheckPropertyScreen(properties, name);
        SaveFrame(camera, texture, canvases, name + "-mod-editor");

        Call(properties, "OpenColor", PreviewBars.Left);
        Assert(Field<SettingsColorPicker>(properties, "_picker").Visible, "a color button opens the picker above the keypad");
        Assert(InsideScreen(ScreenRectOf(Field<SettingsColorPicker>(properties, "_picker").Root)), "the picker stays on screen");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-color");
        Call(properties, "OpenColor", PreviewBars.Left);

        Call(properties, "ToggleChoice", PreviewBars.Grow);
        OverlayChoicePopover choices = Field<OverlayChoicePopover>(properties, "_choicePopover");
        Assert(choices.Visible && choices.Root.GetComponentsInChildren<Button>(true).Length == 2, "a choice opens its options under its button");
        Assert(InsideScreen(ScreenRectOf(choices.Root)), "the options stay on screen");
        SaveFrame(camera, texture, canvases, name + "-mod-editor-choice");
        Call(properties, "PickChoice", 1);
        OverlayModSettings working = Field<OverlayModSettings>(properties, "_working");
        Assert(!choices.Visible && working.Find("grow")?.Value == "middle", "picking an option saves it and closes the list");

        Call(properties, "Flip", PreviewBars.Numbers);
        Assert(Field<OverlayModSettings>(properties, "_working").Find("numbers")?.Value == "false", "a toggle flips on click");

        Call(properties, "Activate", "number:groups");
        OverlayInputDock dock = Field<OverlayInputDock>(properties, "_dock");
        Assert(dock.Owner == "number:groups", "a number in the bar opens the dock");
        Call(properties, "ClearFormula");
        foreach (char key in "99")
          Call(properties, "KeypadPress", key);
        Call(properties, "FinishEditing");
        Assert(Field<OverlayEditorFrame>(properties, "_screen").NoticeText.Contains("64"), "a number past its limit is kept within it, and says so");
        Assert(Field<OverlayModSettings>(properties, "_working").Find("groups")?.Value == "64", "the kept number is saved");

        properties.Close(true, true);
        Assert(!properties.IsOpen && element.Settings.Mod.Find("grow")?.Value == "middle" && element.Settings.Mod.Find("groups")?.Value == "64",
          "Done writes the properties to the element");
        Assert(Mathf.Abs(element.Settings.Mod.Width - 480) < .01f, "the element remembers the size it drew at");
        Call(editor, "Undo");
        Assert(!element.Enabled || element.Settings.Mod.Find("groups") == null, "the whole edit is one undo step");
        Call(editor, "Redo");
        Assert(element.Enabled && element.Settings.Mod.Find("groups")?.Value == "64", "and redo brings it back");

        // The mod goes away: the element keeps its place and properties, and
        // the editor shows why it's empty.
        KitRegistry.Detach("Sample");
        host.TickLayoutPreview();
        Assert(element.Type == null && element.Enabled && element.Settings.Mod.Find("groups")?.Value == "64",
          "without its mod the element stays, properties kept");
        RectTransform elementRect = (RectTransform)typeof(Element.Presentation.Overlay.Elements.OverlayElementTarget)
          .GetProperty("Rect", Private).GetValue(element);
        Assert(elementRect.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text.Contains("Sample")),
          "the editor names the mod it needs");
        Assert(element.Label == (Korean(name) ? "예시 막대" : "Sample bars"), "it keeps its name while its mod is missing: " + element.Label);
        Select(editor, IndexOf(editor, element));
        SaveFrame(camera, texture, canvases, name + "-mod-missing");
        Call(editor, "EditSelected");
        Assert(!properties.IsOpen && Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("Sample"),
          "editing it says what to install instead of opening");
        Select(editor, -1);
      }
      finally
      {
        KitRegistry.Clear();
        host.TickLayoutPreview();
      }
    }

    private static void CheckPropertyScreen(OverlayPropertyEditor properties, string name)
    {
      RectTransform bar = Field<RectTransform>(properties, "_bar");
      Rect barRect = ScreenRectOf(bar);
      Assert(InsideScreen(barRect), "the bar fits the screen: " + barRect);
      Button[] buttons = bar.GetComponentsInChildren<Button>(true);
      Assert(buttons.Length == 7, "the bar holds the mod's six properties and Grow from: " + buttons.Length);
      foreach (TextMeshProUGUI label in bar.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "bar label fits: " + label.text);
      }
      Assert(bar.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text == (Korean(name) ? "켬" : "On")),
        "a toggle says which way it is");
      Rect preview = ScreenRectOf(Field<RectTransform>(properties, "_preview"));
      Rect below = ScreenRectOf(Field<OverlayFormulaField>(properties, "_belowField").Root);
      Rect beside = ScreenRectOf(Field<OverlayFormulaField>(properties, "_besideField").Root);
      Assert(below.yMax < preview.yMin && Mathf.Abs(below.center.x - preview.center.x) < 1, "the width sits under the element");
      Assert(beside.xMin > preview.xMax && Mathf.Abs(beside.center.y - preview.center.y) < 1, "the height sits to its right");
      Assert(Field<OverlayFormulaField>(properties, "_belowField").Text == "480" && Field<OverlayFormulaField>(properties, "_besideField").Text == "96",
        "the numbers show the mod's defaults");
      OverlayInputDock dock = Field<OverlayInputDock>(properties, "_dock");
      Assert(dock.Root.gameObject.activeInHierarchy && dock.Owner == null && dock.HintText.Length > 0, "the dock rests with a hint");
      Assert(ScreenRectOf(dock.Root).yMax < below.yMin, "the element and its numbers stay above the dock");
      Assert(Field<RectTransform>(properties, "_preview").GetComponentsInChildren<Graphic>(false).Length > 16,
        "the element draws itself in the editor with sample data");
    }
  }
}
