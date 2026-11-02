using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Settings;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Overlay.Elements;
using Element.Domain.Elements;
using Element.Presentation.Layout;
using Element.Domain.Input;
using Element.Domain.Layout;
using Element.Application.Updates;
using Element.Infrastructure.Game.DmNote;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  public static class SettingsPreview
  {
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int _checks;
    private static string _output;
    public static void RenderAll()
    {
      _output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../build/ui-preview"));
      Directory.CreateDirectory(_output);
      CheckMigration();
      CheckSpacingGuides();
      _checks += PlanetPreviewChecks.Run(_output);
      _checks += OverlayTargetChecks.Run(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset"),
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset").material);
      _checks += NativeLayoutChecks.Run(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset"));
      Render(1280, 720, SystemLanguage.Korean, 0, "home-ko-720");
      Render(1920, 1080, SystemLanguage.Korean, 0, "home-ko-1080");
      Render(760, 720, SystemLanguage.English, 0, "home-en-narrow");
      Render(540, 720, SystemLanguage.English, 0, "home-en-compact");
      Render(1280, 720, SystemLanguage.English, 1, "tweaks-en-720");
      Render(1280, 720, SystemLanguage.Korean, 2, "general-ko-720");
      Render(760, 720, SystemLanguage.English, 2, "general-en-narrow");
      Render(360, 720, SystemLanguage.English, 2, "general-en-compact");
      Render(1280, 720, SystemLanguage.English, 3, "effects-en-720");
      Render(760, 720, SystemLanguage.Korean, 3, "effects-ko-narrow");
      Render(1280, 720, SystemLanguage.English, 4, "editor-tweaks-en-720");
      Render(1280, 720, SystemLanguage.English, 5, "judgment-en-720");
      Render(1280, 720, SystemLanguage.Korean, 6, "planets-ko-720");
      Render(760, 720, SystemLanguage.English, 6, "planets-en-narrow");
      Render(1280, 720, SystemLanguage.English, 7, "preset-editor-en-720");
      Render(760, 720, SystemLanguage.Korean, 7, "preset-editor-ko-narrow");
      Render(1280, 720, SystemLanguage.English, 4, "editor-editing-en-720");
      Render(1280, 720, SystemLanguage.Korean, 8, "chatter-blocker-ko-720");
      Render(360, 720, SystemLanguage.English, 8, "chatter-blocker-en-compact");
      Render(2880, 1800, SystemLanguage.Korean, 0, "home-ko-retina");
      Debug.Log("UI_PREVIEW_SCREEN " + Screen.width + "x" + Screen.height);
      RenderLayout(Screen.width, Screen.height, SystemLanguage.Korean, "layout-ko");
      RenderLayout(Screen.width, Screen.height, SystemLanguage.English, "layout-en");
      RenderLayout(760, 720, SystemLanguage.English, "layout-en-narrow");
      CheckColorPicker();
      RenderInputCarets();
      File.WriteAllText(Path.Combine(_output, "checks.txt"), _checks + " UI assertions passed. Game integration is not exercised.\n");
      Debug.Log("UI_PREVIEW_OK " + _checks + " assertions");
    }
    private static void RenderLayout(int width, int height, SystemLanguage language, string name)
    {
      RDString.language = language;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      // Elements measure themselves in screen pixels, so the editor uses the
      // fixture's screen; the narrow pass checks the toolbar at its own width.
      int screenWidth = Screen.width, screenHeight = Screen.height;
      var settings = new ElementSettings { ReduceUiMotion = true };
      settings.Migrate();
      OverlayTheme.SetHighlight(settings.HighlightColor);
      var host = new OverlayCanvasHost();
      host.SetFont(font, font.material);
      host.ConfigureLayouts(settings.OverlayLayouts);
      var viewers = new PreviewKeyViewers(screenWidth);
      var editor = new OverlayLayoutEditor(host, viewers, font, _ => {}, settings);
      var eventsRoot = new GameObject("Fixture events", typeof(EventSystem));
      editor.Open();
      GameObject root = Field<GameObject>(editor, "_root"); root.SetActive(true);
      var cameraRoot = new GameObject("Layout preview camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .05f, .07f);
      var texture = new RenderTexture(screenWidth, screenHeight, 24); camera.targetTexture = texture;
      var canvases = new[] { host.Root.GetComponent<Canvas>(), root.GetComponent<Canvas>() };
      _refresh = () => Call(editor, "UpdateFrames");
      editor.Resize(width, height); Canvas.ForceUpdateCanvases();
      Rect toolbar = editor.ToolbarBounds;
      Assert(toolbar.xMin >= 0 && toolbar.xMax <= width && toolbar.yMax <= height, "layout toolbar fits viewport");
      Assert(toolbar.width <= 640.1f && toolbar.height <= 120.1f, "overlay toolbar stays compact with element actions");
      RectTransform toolbarRect = Field<RectTransform>(editor, "_toolbar");
      Call(editor, "UpdateFrames");
      Assert(Field<RectTransform>(editor, "_settingsPanel") == null, "no element toolbar without a selection");
      int fpsIndex = IndexOf(editor, host.LayoutTargets.Single(target => target.Id == "fps"));
      Select(editor, fpsIndex);
      Assert(Field<RectTransform>(editor, "_settingsPanel") == null, "texts have no element toolbar");
      Assert(!toolbarRect.GetComponentsInChildren<Button>(true).Any(button => button.name.Contains("settings")),
        "the toolbar has no settings button");
      Button editContent = Field<Button>(editor, "_editContent");
      Assert(editContent.gameObject.activeSelf && !Field<TMP_InputField>(editor, "_scaleInput").gameObject.activeSelf
        && !Field<Button>(editor, "_plus").gameObject.activeSelf && !Field<Button>(editor, "_minus").gameObject.activeSelf,
        "a text shows Edit text where the scale was");
      editContent.GetComponentInChildren<TextMeshProUGUI>().ForceMeshUpdate();
      Assert(!editContent.GetComponentInChildren<TextMeshProUGUI>().isTextTruncated, "Edit text fits");
      Rect fpsRect = host.LayoutTargets.Single(target => target.Id == "fps").Bounds;
      object[] hit = { fpsRect.max, null };
      int hitIndex = (int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, hit);
      Assert(hitIndex == fpsIndex && (LayoutHandle)hit[1] == LayoutHandle.Move, "a text's corner moves it instead of scaling it");
      var cutouts = editor.NativeExclusions;
      float toolbarScale = (float)editor.GetType().GetProperty("ToolbarScale", Private).GetValue(editor);
      Assert(cutouts.Count == 1 && cutouts.All(cutout => Mathf.Abs(cutout.Radius - 8 * toolbarScale) < .01f),
        "native viewers get the toolbar as a rounded cutout: " + cutouts.Count);
      Assert(root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 1, "only the selected element has an outline");
      // Without a selection the scale shows again; key viewers use it.
      Select(editor, -1);
      Assert(Field<TMP_InputField>(editor, "_scaleInput").gameObject.activeSelf && !Field<Button>(editor, "_editContent").gameObject.activeSelf,
        "the scale returns when no text or shape is selected");
      RectTransform grip = (RectTransform)toolbarRect.Find("Drag panel");
      Assert(Mathf.Abs(CenterIn(grip, toolbarRect).y - CenterIn(Field<TextMeshProUGUI>(editor, "_instruction").rectTransform, toolbarRect).y) < .1f,
        "move grip and title share the same vertical center");
      Button plus = Field<Button>(editor, "_plus"), minus = Field<Button>(editor, "_minus");
      TMP_InputField sizeInput = Field<TMP_InputField>(editor, "_scaleInput");
      Assert(Mathf.Abs(CenterIn((RectTransform)plus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f
        && Mathf.Abs(CenterIn((RectTransform)minus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f,
        "plus, minus and percentage field share one vertical center");
      CheckInputCaret(sizeInput, "overlay scale", "", "100", "99.9");
      foreach (LayoutActionIcon icon in root.GetComponentsInChildren<LayoutActionIcon>(true))
        using (var mesh = new VertexHelper())
        {
          typeof(LayoutActionIcon).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(icon, new object[] { mesh });
          Assert(mesh.currentVertCount > 0, "vector icon renders: " + icon.Kind);
        }
      foreach (TextMeshProUGUI label in toolbarRect.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(label.font == font && !label.isTextTruncated, "toolbar label fits: " + label.text);
      }
      Assert(Field<Button>(editor, "_save").colors.normalColor.maxColorComponent < .5f, "save uses a dark tinted fill");
      if (width != screenWidth || height != screenHeight)
      {
        Cleanup(editor, root, eventsRoot, cameraRoot, texture, host);
        return;
      }

      // Everything below edits real elements on the fixture's screen.
      // The scale itself, as key viewers use it, measured on a real element.
      IOverlayLayoutTarget fps = host.LayoutTargets.Single(target => target.Id == "fps");
      Select(editor, fpsIndex);
      Call(editor, "CommitScale", "150");
      Assert(Mathf.Abs(fps.EffectiveScale - 1.5f) < .01f, "precise layout scale commits");
      Call(editor, "Undo"); Assert(Mathf.Abs(fps.EffectiveScale - 1) < .01f, "layout undo restores the scale");
      Call(editor, "Redo"); Assert(Mathf.Abs(fps.EffectiveScale - 1.5f) < .01f, "layout redo restores the edit");
      Call(editor, "CommitScale", "1999"); Call(editor, "UpdateToolbar");
      Assert(Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("1000"), "scale validation names the 1000% limit");
      SaveFrame(camera, texture, canvases, name);

      // Add Element: three kinds; key viewers explain why they're unavailable.
      viewers.Availability = KeyViewerAvailability.AppClosed;
      Call(editor, "ToggleAddMenu");
      RectTransform menu = Field<RectTransform>(editor, "_addMenu");
      Button[] kinds = menu.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Add ")).ToArray();
      Assert(kinds.Length == 3, "Add Element offers Text, Key viewer and Shape");
      Assert(kinds[0].interactable && !kinds[1].interactable && kinds[2].interactable, "a closed ElementNote disables only key viewers");
      Assert(kinds[1].GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text.Contains("ElementNote")), "the disabled key viewer says how to enable it");
      foreach (TextMeshProUGUI label in menu.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "add menu label fits: " + label.text);
      }
      Assert(PopoverInside(editor, menu, screenWidth, screenHeight), "the add menu stays on screen");
      SaveFrame(camera, texture, canvases, name + "-add");
      Call(editor, "ToggleAddMenu");
      Assert(Field<RectTransform>(editor, "_addMenu") == null, "Add Element closes its menu again");

      CheckShapes(editor, host, camera, texture, canvases, name, screenWidth, screenHeight);
      CheckKeyViewers(editor, viewers);
      CheckTextEditor(editor, host, camera, texture, canvases, name);
      CheckMultiSelect(editor, host, settings, camera, texture, canvases, name);
      CheckHighlightColor(editor, settings, camera, texture, canvases, name);

      var beforeReset = new LayoutSnapshot(host.LayoutSettings);
      List<OverlayLayoutSettings> beforeCopies = host.LayoutSettings.Select(layout => layout.Clone()).ToList();
      Call(editor, "OpenResetConfirmation");
      Assert(Field<GameObject>(editor, "_modalRoot") != null && editor.NativeExclusionBounds == new Rect(0, 0, screenWidth, screenHeight),
        "Reset all asks first, covering native viewers");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "opening the confirmation changes nothing");
      Call(editor, "CloseModal");
      Call(editor, "RestoreDefaultOverlays");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled)
        && host.LayoutTargets.Single(target => target.Id == "fps").Enabled, "Reset all removes added elements and restores built-ins");
      Call(editor, "Undo");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "Reset all is one undo step: "
        + string.Join(", ", host.LayoutSettings.Where(layout => !(beforeCopies.Find(copy => copy.Id == layout.Id)?.SameAs(layout) ?? !layout.Enabled))
          .Select(layout => layout.Id + (layout.Enabled ? "" : " (hidden)"))));
      editor.Close(false);
      Assert(!editor.IsOpen && settings.HighlightColor == "#BA712B", "cancel restores the opening highlight and exits");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled), "cancel discards added shapes");
      Cleanup(editor, root, eventsRoot, cameraRoot, texture, host);
    }

    private static void CheckShapes(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      // Add → Shape asks for the kind beside the add menu.
      Call(editor, "ToggleAddMenu");
      Field<RectTransform>(editor, "_addMenu").GetComponentsInChildren<Button>(true).Single(button => button.name == "Add Shape").onClick.Invoke();
      RectTransform kindMenu = Field<RectTransform>(editor, "_shapeMenu");
      Assert(kindMenu != null && Field<RectTransform>(editor, "_addMenu") != null, "Shape opens a kind menu next to the add menu");
      string[] kindNames = kindMenu.GetComponentsInChildren<Button>(true).Select(button => button.name).ToArray();
      Assert(kindNames.SequenceEqual(new[] { "Add Square", "Add Triangle", "Add Circle" }), "the kinds are square, triangle and circle");
      Rect addRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { Field<RectTransform>(editor, "_addMenu") });
      Rect kindRect = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { kindMenu });
      Assert(PopoverInside(editor, kindMenu, width, height) && (kindRect.x >= addRect.xMax - .5f || kindRect.xMax <= addRect.x + .5f),
        "the kind menu sits beside the add menu, on screen");
      Assert(!kindRect.Overlaps(editor.ToolbarBounds), "the kind menu leaves the toolbar uncovered");
      SaveFrame(camera, texture, canvases, name + "-shape-kinds");
      kindMenu.GetComponentsInChildren<Button>(true).Single(button => button.name == "Add Square").onClick.Invoke();
      ShapeOverlayElement shape = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      OverlayShapeEditor shapes = Field<OverlayShapeEditor>(editor, "_shapeEditor");
      Assert(shape.Enabled && shape.Shape.Kind == OverlayShapeKind.Square && shapes.IsOpen, "a new square opens its editor at once");
      Assert(editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "the shape editor covers native viewers");
      float scale = shape.EffectiveScale;
      OverlayInputDock dock = Field<OverlayInputDock>(shapes, "_dock");
      Assert(dock.Root.gameObject.activeInHierarchy && dock.Owner == null && dock.HintText.Length > 0
        && dock.Keypad.Root.GetComponentsInChildren<Button>(true).All(button => !button.interactable),
        "the input dock is always there, resting with a hint until a number is clicked");
      dock.Content.GetComponentsInChildren<Button>(true).Single(chip => chip.name == "Value combo").onClick.Invoke();
      Assert(Field<OverlayEditorFrame>(shapes, "_screen").NoticeText.Length > 0, "a value with no number picked says to pick one first");
      OverlayFormulaField widthField = Field<OverlayFormulaField>(shapes, "_widthField"), heightField = Field<OverlayFormulaField>(shapes, "_heightField");
      Assert(widthField.Text == "160" && heightField.Text == "160", "W and H show the size in pixels");
      Rect restingDock = ScreenRectOf(dock.Root);
      Assert(InsideScreen(restingDock) && restingDock.yMax < ScreenRectOf(widthField.Root).yMin, "the shape and its numbers stay above the dock");
      Rect shapeRect = ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"));
      Rect wRect = ScreenRectOf(widthField.Root), hRect = ScreenRectOf(heightField.Root);
      Assert(wRect.yMax < shapeRect.yMin && Mathf.Abs(wRect.center.x - shapeRect.center.x) < 1, "W sits under the shape");
      Assert(hRect.xMin > shapeRect.xMax && Mathf.Abs(hRect.center.y - shapeRect.center.y) < 1, "H sits to its right");
      Assert(shapes.GetType().GetField("_widthLine", Private) == null, "no dimension lines");
      List<Button> arcs = Field<List<Button>>(shapes, "_arcs");
      Assert(arcs.Count == 4 && arcs.All(arc => arc.gameObject.activeSelf), "a square has an arc at each corner");
      Assert(Field<List<OverlayFormulaField>>(shapes, "_radiusFields").All(field => !field.Root.gameObject.activeSelf),
        "rounding values stay hidden until an arc is clicked");
      foreach (Button arc in arcs)
      {
        Rect arcRect = ScreenRectOf((RectTransform)arc.transform);
        Vector2 corner = arcRect.center;
        Assert(Mathf.Abs(corner.x - shapeRect.xMin) < 1 || Mathf.Abs(corner.x - shapeRect.xMax) < 1, "each arc is centered on a corner");
        using (var mesh = new VertexHelper())
        {
          typeof(OverlayCornerArc).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(arc.GetComponent<OverlayCornerArc>(), new object[] { mesh });
          List<UIVertex> vertices = new List<UIVertex>();
          mesh.GetUIVertexStream(vertices);
          Vector3 middle = vertices.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex.position) / vertices.Count;
          RectTransform arcTransform = (RectTransform)arc.transform;
          Vector2 outward = (Vector2)arcTransform.TransformPoint(middle) - (Vector2)arcTransform.TransformPoint(arcTransform.rect.center);
          Vector2 away = corner - shapeRect.center;
          Assert(Vector2.Dot(outward, away) > 0, "an arc faces away from the shape");
        }
      }
      OverlayBarButton fillButton = Field<OverlayBarButton>(shapes, "_fillButton"), strokeButton = Field<OverlayBarButton>(shapes, "_strokeButton"),
        strokeWidth = Field<OverlayBarButton>(shapes, "_strokeWidth"), shapeGrow = Field<OverlayBarButton>(shapes, "_growButton");
      Assert(shapeGrow.Value.Contains(Korean(name) ? "자동" : "Auto"), "the grow button says where the shape grows from");
      Assert(shapes.GetType().GetField("_fillOpacity", Private) == null && shapes.GetType().GetField("_strokeOpacity", Private) == null,
        "opacity lives in the color picker, not the bar");
      OverlayBarButton[] shapeBar = { fillButton, strokeButton, strokeWidth, shapeGrow };
      string[] shapeLabels = Korean(name) ? new[] { "채우기", "테두리", "테두리 두께", "기준점" } : new[] { "Fill", "Border", "Border width", "Grow from" };
      for (int i = 0; i < shapeBar.Length; i++)
      {
        Assert(shapeBar[i].Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == shapeLabels[i]),
          "each bar button says what it is: " + shapeLabels[i]);
        Assert(shapeBar[i].Root.GetComponent<Button>().targetGraphic.transform == shapeBar[i].Root, "a bar button lights up where it is drawn");
        if (i > 0)
          Assert(ScreenRectOf(shapeBar[i].Root).xMin > ScreenRectOf(shapeBar[i - 1].Root).xMax, "bar buttons sit side by side");
      }
      Assert(strokeWidth.Value.Length > 0 && !strokeWidth.Computed, "the border width shows its number");
      foreach (OverlayFormulaField measure in new[] { widthField, heightField })
        Assert(measure.Root.GetComponent<Button>().targetGraphic.transform == measure.Root,
          "W and H light up exactly where the pill is drawn: " + measure.Name);
      Assert(!Field<RectTransform>(shapes, "_bar").GetComponentsInChildren<Button>(true).Any(button => button.name.EndsWith("smaller") || button.name.EndsWith("larger")),
        "numbers have no − and + buttons");
      RectTransform bar = Field<RectTransform>(shapes, "_bar");
      foreach (TextMeshProUGUI label in bar.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "shape bar label fits: " + label.text);
      }
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-editor");

      // Clicking W opens the input dock; the shape moves up to stay in sight.
      Click(widthField.Root, "Width");
      OverlayFormulaEditing editing = Field<OverlayFormulaEditing>(shapes, "_editing");
      Assert(dock.Owner == "width" && editing.Active && editing.Working.Serialize() == "160",
        "W opens the input dock with its number");
      Rect dockRect = ScreenRectOf(dock.Root);
      shapeRect = ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"));
      Assert(InsideScreen(dockRect) && dockRect.yMax < shapeRect.yMin && dockRect.yMax < ScreenRectOf(widthField.Root).yMin,
        "the dock sits below the shape and its numbers, never over them");
      Assert(dockRect.width > Screen.width * .7f, "the dock spans the screen");
      Assert(dock.Content.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Value ")) == OverlayValueCatalog.All.Count(value => !OverlayValueCatalog.IsLegacy(value)),
        "every value is laid out at once");
      Assert(dock.Keypad.Root.GetComponentsInChildren<Button>(true).Any(button => button.name == "Key clear")
        && dock.Keypad.Root.GetComponentsInChildren<Button>(true).Any(button => button.name == "Key done"), "the keypad has AC and Done");
      foreach (TextMeshProUGUI label in dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        if (label.GetComponentInParent<ScrollRect>() != null || label.name == "Formula" || label.richText)
          continue;
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "dock label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-dock");
      foreach (char key in "\b\b\b240")
        Call(shapes, "KeypadPress", key);
      OverlayShapeSettings working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(Mathf.Abs(working.Width * scale - 240) < .01f && widthField.Text == "240", "keypad digits set the width as they are typed");
      Call(shapes, "KeypadPress", '0');
      Call(shapes, "KeypadPress", '0');
      dock.Keypad.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Key done").onClick.Invoke();
      Assert(dock.Owner == null && Mathf.Abs(working.Width * scale - 10000) < .01f
        && Field<OverlayEditorFrame>(shapes, "_screen").NoticeText.Contains("10000"),
        "Done lets the dock rest; a width past the limit is kept in range and says so");
      Call(shapes, "Undo");
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(Mathf.Abs(working.Width * scale - 160) < .01f, "keypad typing undoes as one step");

      // A value picked in the dock: the formula in the dock, the result on W.
      Click(widthField.Root, "Width");
      dock.Content.GetComponentsInChildren<Button>(true).Single(chip => chip.name == "Value progress-current").onClick.Invoke();
      Call(shapes, "KeypadPress", '*');
      Call(shapes, "KeypadPress", '3');
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.WidthFormula == "{progress-current}*3", "the keypad calculates with the value: " + working.WidthFormula);
      Assert(dock.FormulaText.Contains("×") && dock.ResultText.Contains("144"), "the dock shows the formula and its result: " + dock.FormulaText + " / " + dock.ResultText);
      Assert(widthField.Text == "144" && widthField.Computed, "W shows the result, marked as calculated");
      dock.Keypad.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Key clear").onClick.Invoke();
      Assert(editing.Working.IsEmpty && working.WidthFormula == "{progress-current}*3", "AC starts over without losing the width yet");
      Call(shapes, "KeypadPress", '9');
      Call(shapes, "KeypadPress", '0');
      Assert(working.WidthFormula == "" && Mathf.Abs(working.Width * scale - 90) < .01f, "typing after AC replaces the formula");
      Call(shapes, "Undo");
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.WidthFormula == "{progress-current}*3", "undo brings the formula back");
      Click(heightField.Root, "Height");
      Assert(dock.Owner == "height", "another number switches the dock to it");
      SaveFrame(camera, texture, canvases, name + "-shape-values");
      Call(shapes, "CloseEditing");

      // Dragging still works: words are refused, numbers land on the number.
      Vector2 heightPoint = ScreenCenter(heightField.Root);
      Call(shapes, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.SongTitle), heightPoint);
      Call(shapes, "EndValueDrag", heightPoint);
      Assert(working.HeightFormula == "" && Field<OverlayEditorFrame>(shapes, "_screen").NoticeText.Length > 0,
        "the song title is refused by numbers, with a reason");
      Vector2 strokePoint = ScreenCenter(strokeWidth.Root);
      Call(shapes, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.Accuracy), strokePoint);
      Assert(Field<int>(strokeWidth, "_drop") == 1, "the border width lights up for a number dragged over it");
      Call(shapes, "EndValueDrag", strokePoint);
      Assert(working.StrokeWidthFormula == "accuracy" && dock.Owner == "stroke" && strokeWidth.Active,
        "a value dropped on the border width opens it in the dock");
      Assert(strokeWidth.Computed && strokeWidth.Value == "99.5", "the border width shows the result, marked as calculated: " + strokeWidth.Value);
      Call(shapes, "Undo");
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Call(shapes, "CloseEditing");

      // A corner's arc opens its rounding, shown beside the arc.
      arcs[2].onClick.Invoke();
      OverlayFormulaField radius = Field<List<OverlayFormulaField>>(shapes, "_radiusFields")[2];
      Assert(dock.Owner == "radius:2" && radius.Root.gameObject.activeSelf, "an arc opens its corner's rounding with its value");
      foreach (char key in "\b\b\b20")
        Call(shapes, "KeypadPress", key);
      Assert(radius.Text == "20", "the rounding value follows the keypad");
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-corner");
      Call(shapes, "CloseEditing");
      Call(shapes, "Render");
      Assert(Mathf.Abs(working.Points[2].Radius * scale - 20) < .01f && working.Points[0].Radius == 0 && !radius.Root.gameObject.activeSelf,
        "one corner rounds; its value hides again");

      // Colors open right above the keypad: the keypad below types the
      // opacity and the values beside it can be dropped on it.
      fillButton.Button.onClick.Invoke();
      SettingsColorPicker picker = Field<SettingsColorPicker>(shapes, "_picker");
      Rect pickerRect = ScreenRectOf(picker.Root), dockArea = ScreenRectOf(dock.Root);
      Assert(picker.Visible && fillButton.Active && !strokeButton.Active, "Fill opens its picker and stays lit");
      Rect keypadRect = ScreenRectOf(dock.Keypad.Root);
      Assert(picker.Column && dock.Keypad.Root.gameObject.activeInHierarchy && Mathf.Abs(pickerRect.xMin - keypadRect.xMin) < 1
        && Mathf.Abs(pickerRect.xMax - keypadRect.xMax) < 1 && pickerRect.yMin >= dockArea.yMax - 1 && pickerRect.yMin - dockArea.yMax < 20
        && InsideScreen(pickerRect), "the picker opens right above the keypad, in its column: " + pickerRect + " / " + keypadRect);
      Assert(!pickerRect.Overlaps(ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"))) && dock.HintText.Contains(Korean(name) ? "채우기 색" : "fill color"),
        "the shape stays in sight, and the dock's line says which color is picked: " + dock.HintText);
      OverlayOpacityField opacity = Field<OverlayOpacityField>(shapes, "_opacity");
      Assert(picker.ExternalOpacity && picker.OpacitySlot.gameObject.activeInHierarchy && opacity.Field.Root.IsChildOf(picker.OpacitySlot)
        && !Field<TMP_InputField>(picker, "_opacity").gameObject.activeSelf && opacity.Field.Text == "100",
        "the picker carries the opacity as a number like W and H");
      RectTransform plane = Field<RectTransform>(picker, "_planeRect");
      SaveFrame(camera, texture, canvases, name + "-shape-color");
      Field<SettingsSegmentedControl>(picker, "_source").Select(1, true);
      Call(shapes, "Render");
      pickerRect = ScreenRectOf(picker.Root);
      Rect planeRect = ScreenRectOf(plane), hueRect = ScreenRectOf((RectTransform)Field<Slider>(picker, "_hue").transform),
        alphaRect = ScreenRectOf((RectTransform)Field<Slider>(picker, "_alpha").transform),
        hexRect = ScreenRectOf((RectTransform)Field<TMP_InputField>(picker, "_hex").transform);
      Assert(Mathf.Abs(plane.rect.width - 256) < .01f && Mathf.Abs(plane.rect.height - 156) < .01f, "the color square keeps its full size");
      Assert(planeRect.center.y > hueRect.center.y && hueRect.center.y > alphaRect.center.y && alphaRect.center.y > hexRect.center.y
        && pickerRect.Contains(planeRect.min) && pickerRect.Contains(hexRect.min) && !Field<TextMeshProUGUI>(picker, "_title").gameObject.activeSelf,
        "top to bottom: Highlight/Custom, the square, hue, opacity, then HEX with opacity, without a title or card");
      float topGap = ((RectTransform)picker.Root).anchoredPosition.y;
      Assert(pickerRect.yMin >= ScreenRectOf(dock.Root).yMax - 1 || Mathf.Abs(-topGap - (OverlayEditorFrame.TopBar + 8)) < 1,
        "with the color square, it still sits above the dock, or right under the top bar on a short screen: " + pickerRect + " top " + topGap);
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "docked picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-color-custom");
      Field<TMP_InputField>(picker, "_hex").text = "#2050A0";
      Assert(picker.Commit() && working.Fill.Hex == "#2050A0", "the fill takes the new color");
      Slider alphaSlider = Field<Slider>(picker, "_alpha");
      opacity.Field.Root.GetComponent<Button>().onClick.Invoke();
      Assert(picker.Visible && dock.Owner == "fill-opacity" && Field<OverlayFormulaEditing>(shapes, "_editing").Working.Serialize() == "100",
        "clicking the opacity types it in the dock, with the picker still open");
      Assert(picker.Visible && dock.Keypad.Root.gameObject.activeInHierarchy, "the keypad below types the opacity, the picker staying open");
      foreach (char key in "\b\b\b50")
        Call(shapes, "KeypadPress", key);
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(Mathf.Abs(working.Fill.Alpha - .5f) < .001f && working.Fill.AlphaFormula == "" && Mathf.Abs(alphaSlider.value - .5f) < .001f
        && opacity.Field.Text == "50", "a typed opacity moves the slider");
      Call(shapes, "FinishEditing");
      Assert(picker.Visible && dock.Owner == null, "done typing, the picker stays open for the color");
      Vector2 opacityPoint = ScreenCenter(opacity.Field.Root);
      Call(shapes, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.Accuracy), opacityPoint);
      Assert(picker.Visible && Field<int>(opacity.Field, "_drop") == 1, "the picker stays open while a value is dragged, and its opacity lights up");
      Call(shapes, "EndValueDrag", opacityPoint);
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.AlphaFormula == "accuracy" && dock.Owner == "fill-opacity" && opacity.Field.Computed && opacity.Field.Text == "99.5",
        "a value dropped on the opacity calculates it: " + working.Fill.AlphaFormula + " / " + opacity.Field.Text);
      Assert(Mathf.Abs(alphaSlider.value - .995f) < .001f && Mathf.Abs(working.Fill.Alpha - .5f) < .001f,
        "the slider shows the result; the plain opacity stays underneath");
      Assert(dock.ResultText.Contains("99.5%"), "the dock shows the opacity in percent: " + dock.ResultText);
      SaveFrame(camera, texture, canvases, name + "-shape-opacity");
      Field<TMP_InputField>(picker, "_hex").text = "#3060B0";
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.Hex == "#3060B0" && working.Fill.AlphaFormula == "accuracy", "a new color keeps the calculated opacity");
      Assert(picker.Commit() && Field<OverlayShapeSettings>(shapes, "_shape").Fill.AlphaFormula == "accuracy", "confirming the picker keeps it too");
      Call(shapes, "Undo");
      Assert(picker.Visible && Field<OverlayShapeSettings>(shapes, "_shape").Fill.Hex == "#2050A0"
        && Field<TMP_InputField>(picker, "_hex").text == "2050A0", "undo keeps the picker open, showing the restored color");
      Call(shapes, "Redo");
      alphaSlider.value = .3f;
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.AlphaFormula == "" && Mathf.Abs(working.Fill.Alpha - .3f) < .01f && !opacity.Field.Computed && opacity.Field.Text == "30",
        "moving the slider makes it a plain number again: " + opacity.Field.Text);
      Call(shapes, "CloseEditing");
      Field<TMP_InputField>(picker, "_hex").text = "#2050A0";
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      strokeButton.Button.onClick.Invoke();
      Assert(picker.Visible && strokeButton.Active && !fillButton.Active && ScreenRectOf(picker.Root) == pickerRect
        && dock.HintText.Contains(Korean(name) ? "테두리 색" : "border color"), "Border switches the picker to its color, in the same place");
      strokeButton.Button.onClick.Invoke();
      Assert(!picker.Visible && !strokeButton.Active && dock.Keypad.Root.gameObject.activeSelf, "clicking it again closes the picker and brings the keypad back");

      // Where the shape grows from.
      shapeGrow.Button.onClick.Invoke();
      OverlayGrowPopover grow = Field<OverlayGrowPopover>(shapes, "_growPopover");
      Assert(grow.Visible && InsideScreen(ScreenRectOf(grow.Root)) && shapeGrow.Active, "the grow point opens its own popover, on screen");
      Assert(ScreenRectOf(grow.Root).yMax <= ScreenRectOf(shapeGrow.Root).yMin, "the grow popover hangs under its button");
      Call(shapes, "PickGrowth", OverlayGrowth.TopLeft);
      Assert(Field<SettingsCapsuleGraphic>(shapes, "_growMark").gameObject.activeSelf, "the preview marks the point that stays put");
      Assert(!shapeGrow.Value.Contains("Auto") && !shapeGrow.Value.Contains("자동"), "the button names the chosen point");
      SaveFrame(camera, texture, canvases, name + "-shape-grow");
      Call(shapes, "CloseEditing");
      Call(shapes, "Render");
      SaveFrame(camera, texture, canvases, name + "-shape-edited");

      shapes.Close(true, true);
      Assert(!shapes.IsOpen && shape.Shape.WidthFormula == "{progress-current}*3" && shape.Shape.Fill.Hex == "#2050A0"
        && shape.Settings.GrowFrom == OverlayGrowth.TopLeft, "Done saves the shape and where it grows from");
      Assert(Mathf.Abs(shape.Resolved.Width * scale - 144) < .5f, "the canvas shape follows its formula");
      Canvas.ForceUpdateCanvases();
      Rect before = shape.Bounds;
      host.Values.Set(OverlayValueId.ProgressCurrent, new System.Text.StringBuilder("60"), 60);
      host.Tick();
      Canvas.ForceUpdateCanvases();
      Rect after = shape.Bounds;
      Assert(after.width > before.width + 1 && Mathf.Abs(after.x - before.x) < 1 && Mathf.Abs(after.y - before.y) < 1,
        "a shape growing from its top left keeps that corner: " + before + " -> " + after);
      host.Values.Clear(OverlayValueId.ProgressCurrent);
      host.Tick();
      Call(editor, "Undo");
      Assert(!shape.Enabled, "adding and editing a new shape is one undo step");
      Call(editor, "Redo");
      Assert(shape.Enabled && shape.Shape.WidthFormula == "{progress-current}*3", "redo brings it back edited");

      // On the canvas: Edit shape where the scale was; cancel keeps it as it was.
      Select(editor, IndexOf(editor, shape));
      Call(editor, "UpdateToolbar");
      Button editContent = Field<Button>(editor, "_editContent");
      Assert(Field<RectTransform>(editor, "_settingsPanel") == null && editContent.gameObject.activeSelf
        && editContent.GetComponentInChildren<TextMeshProUGUI>().text == (Korean(name) ? "도형 편집" : "Edit shape"),
        "a selected shape offers Edit shape where the scale was");
      editContent.onClick.Invoke();
      Assert(shapes.IsOpen, "Edit shape opens the shape editor");
      heightField = Field<OverlayFormulaField>(shapes, "_heightField");
      Click(heightField.Root, "Height");
      Call(shapes, "KeypadPress", '9');
      Call(shapes, "PickGrowth", OverlayGrowth.Center);
      shapes.Close(false, true);
      Assert(shape.Enabled && shape.Shape.HeightFormula == "" && Mathf.Abs(shape.Shape.Height - OverlayShapeGeometry.DefaultSize) < .01f
        && shape.Settings.GrowFrom == OverlayGrowth.TopLeft, "cancel leaves the shape as it was");

      // Triangles and circles.
      Call(editor, "AddShape", OverlayShapeKind.Triangle);
      Assert(Field<List<Button>>(shapes, "_arcs").Count == 3, "a triangle has three corners");
      ShapeOverlayElement triangle = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-triangle");
      shapes.Close(false, true);
      Assert(!triangle.Enabled, "cancelling a new shape removes it");
      Call(editor, "AddShape", OverlayShapeKind.Circle);
      Assert(Field<List<Button>>(shapes, "_arcs").Count == 0, "circles have only W and H");
      SaveFrame(camera, texture, canvases, name + "-shape-circle");
      shapes.Close(false, true);
      Call(editor, "ClosePopovers");
    }

    private static bool Korean(string name) => name.Contains("-ko");

    // Presses a field's number (its body button is named after the field).
    private static void Click(RectTransform field, string buttonName)
    {
      Button[] buttons = field.GetComponentsInChildren<Button>(true);
      Button body = buttonName == null ? buttons.First() : buttons.Single(button => button.name == buttonName);
      body.onClick.Invoke();
    }

    private static float OverlapArea(Rect a, Rect b)
    {
      float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.x, b.x), height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y);
      return width > 0 && height > 0 ? width * height : 0;
    }

    private static bool InsideScreen(Rect rect) =>
      rect.xMin >= -.5f && rect.yMin >= -.5f && rect.xMax <= Screen.width + .5f && rect.yMax <= Screen.height + .5f;

    // Every shown control of the shape editor stays on screen, and the
    // measures around the shape never cover each other.
    private static void CheckShapeLayout(OverlayShapeEditor shapes, int width, int height)
    {
      GameObject root = Field<OverlayEditorFrame>(shapes, "_screen").Root;
      foreach (RectTransform rect in root.GetComponentsInChildren<SettingsCapsuleGraphic>(false).Select(graphic => graphic.rectTransform))
      {
        if (rect.name == "Dragged value" || rect.name == "Value tooltip" || rect.GetComponentInParent<ScrollRect>() != null)
          continue;
        Assert(InsideScreen(ScreenRectOf(rect)), "shape editor control stays on screen: " + rect.name);
      }
      List<OverlayFormulaField> measures = new List<OverlayFormulaField>
      {
        Field<OverlayFormulaField>(shapes, "_widthField"), Field<OverlayFormulaField>(shapes, "_heightField"),
      };
      Rect shapeRect = ScreenRectOf(Field<RectTransform>(shapes, "_shapeRect"));
      foreach (OverlayFormulaField field in measures)
        Assert(!ScreenRectOf(field.Root).Overlaps(shapeRect), "W and H stay off the shape: " + field.Name);
      measures.AddRange(Field<List<OverlayFormulaField>>(shapes, "_radiusFields"));
      List<OverlayFormulaField> shown = measures.Where(field => field.Root.gameObject.activeInHierarchy).ToList();
      for (int i = 0; i < shown.Count; i++)
      for (int j = i + 1; j < shown.Count; j++)
        Assert(!ScreenRectOf(shown[i].Root).Overlaps(ScreenRectOf(shown[j].Root)),
          "shape measures stay apart: " + shown[i].Name + " / " + shown[j].Name);
    }

    private static string FieldText(OverlayFormulaField field) => Field<TextMeshProUGUI>(field, "_text").text;

    private static Vector2 ScreenCenter(RectTransform rect) =>
      RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

    // Several elements: modifier clicks, the marquee, moving, arrows, align,
    // distribute, delete and select all, each one undo step.
    private static void CheckMultiSelect(OverlayLayoutEditor editor, OverlayCanvasHost host, ElementSettings settings, Camera camera,
      RenderTexture texture, Canvas[] canvases, string name)
    {
      var before = new LayoutSnapshot(host.LayoutSettings);
      List<IOverlayLayoutTarget> selection = Field<List<IOverlayLayoutTarget>>(editor, "_selection");
      GameObject root = Field<GameObject>(editor, "_root");
      // Three texts spread across the screen: the leftmost, the one nearest the middle, the rightmost.
      List<IOverlayLayoutTarget> shown = host.LayoutTargets.OfType<TextOverlayElement>().Where(text => text.Available && text.Enabled)
        .Cast<IOverlayLayoutTarget>().ToList();
      IOverlayLayoutTarget leftmost = shown.OrderBy(text => text.Bounds.xMin).First(), rightmost = shown.OrderBy(text => text.Bounds.xMax).Last();
      IOverlayLayoutTarget middle = shown.Where(text => text != leftmost && text != rightmost)
        .OrderBy(text => Mathf.Abs(text.Bounds.center.x - Screen.width * .5f)).First();
      IOverlayLayoutTarget[] texts = { leftmost, middle, rightmost };
      Assert(leftmost.Bounds.xMax < middle.Bounds.xMin && middle.Bounds.xMax < rightmost.Bounds.xMin, "three texts apart from each other");
      int a = IndexOf(editor, texts[0]), b = IndexOf(editor, texts[1]), c = IndexOf(editor, texts[2]);
      Call(editor, "SelectOnly", a);
      Call(editor, "ToggleSelection", b);
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 2 && selection.Contains(texts[0]) && selection.Contains(texts[1]) && Field<int>(editor, "_selected") == b,
        "Cmd/Ctrl + click adds an element");
      Assert(root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 2, "each selected element has its outline");
      Assert(Field<TextMeshProUGUI>(editor, "_instruction").text == (Korean(name) ? "2개 선택됨" : "2 selected"), "the toolbar counts the selection");
      TMP_InputField scaleInput = Field<TMP_InputField>(editor, "_scaleInput");
      Assert(!Field<Button>(editor, "_editContent").gameObject.activeSelf && scaleInput.gameObject.activeSelf && !scaleInput.interactable
        && scaleInput.text == "—" && Field<Button>(editor, "_delete").interactable && Field<Button>(editor, "_reset").interactable,
        "several selected: no Edit, the scale greys out, delete and reset stay");
      RectTransform bar = Field<RectTransform>(editor, "_settingsPanel");
      Assert(bar != null && bar.gameObject.activeSelf, "several selected get the align toolbar");
      Func<LayoutAction, Button> arrange = kind =>
        bar.GetComponentsInChildren<Button>(true).Single(button => button.GetComponentInChildren<LayoutActionIcon>()?.Kind == kind);
      Assert(arrange(LayoutAction.AlignLeft).interactable && !arrange(LayoutAction.DistributeHorizontal).interactable
        && !arrange(LayoutAction.DistributeVertical).interactable, "two can be aligned; spacing waits for three");
      Assert(PopoverInside(editor, bar, Screen.width, Screen.height), "the align toolbar stays on screen");
      object[] hit = { texts[0].Bounds.center, null };
      Assert((int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, hit) == a && (LayoutHandle)hit[1] == LayoutHandle.Move,
        "each selected element moves the group");
      Call(editor, "ToggleSelection", b);
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 1 && selection[0] == texts[0] && Field<int>(editor, "_selected") == a && Field<RectTransform>(editor, "_settingsPanel") == null,
        "a second modifier click takes it out again");

      // The marquee: only what lies fully inside.
      Rect covered = Rect.MinMaxRect(texts.Min(t => t.SelectionBounds.xMin) - 4, texts.Min(t => t.SelectionBounds.yMin) - 4,
        texts.Max(t => t.SelectionBounds.xMax) + 4, texts.Max(t => t.SelectionBounds.yMax) + 4);
      Call(editor, "SelectOnly", -1);
      Call(editor, "BeginMarquee", covered.min, -1, false);
      SetField(editor, "_marqueeActive", true);
      Call(editor, "SelectInside", covered.max);
      Call(editor, "DrawMarquee", covered.max);
      Call(editor, "UpdateFrames");
      Assert(texts.All(selection.Contains) && selection.All(t => covered.Contains(t.SelectionBounds.min) && covered.Contains(t.SelectionBounds.max)),
        "dragging over the canvas selects what lies fully inside");
      Assert(Field<Image>(editor, "_marqueeFill").gameObject.activeSelf, "the selected area is drawn while dragging");
      SaveFrame(camera, texture, canvases, name + "-multi-marquee");
      Call(editor, "EndMarquee");
      Assert(!Field<Image>(editor, "_marqueeFill").gameObject.activeSelf, "the area disappears on release");
      Rect inner = texts[0].SelectionBounds;
      Call(editor, "BeginMarquee", inner.min + Vector2.one, -1, false);
      SetField(editor, "_marqueeActive", true);
      Call(editor, "SelectInside", inner.max + Vector2.one * 4);
      Assert(!selection.Contains(texts[0]), "an element only partly inside stays out");
      Call(editor, "EndMarquee");
      Call(editor, "SelectOnly", a);
      Rect third = texts[2].SelectionBounds;
      Call(editor, "BeginMarquee", third.min - Vector2.one * 3, -1, true);
      SetField(editor, "_marqueeActive", true);
      Call(editor, "SelectInside", third.max + Vector2.one * 3);
      Assert(selection.Contains(texts[0]) && selection.Contains(texts[2]), "Cmd/Ctrl + drag adds to the selection, even starting on an element");
      Call(editor, "EndMarquee");
      // Released without moving: a modifier click toggles, a plain one on empty space clears.
      Call(editor, "BeginMarquee", texts[1].Bounds.center, b, true);
      Call(editor, "ReleaseMarquee");
      Assert(selection.Count == 3 && selection.Contains(texts[1]), "a modifier click without moving adds what it hit");
      Call(editor, "BeginMarquee", new Vector2(2, 2), -1, false);
      Call(editor, "ReleaseMarquee");
      Assert(selection.Count == 0 && Field<int>(editor, "_selected") == -1, "a click on empty space clears the selection");

      // Moving all three, as one undo step.
      foreach (int index in new[] { a, b, c })
        Call(editor, "ToggleSelection", index);
      bool snapping = settings.LayoutSnapping;
      settings.LayoutSnapping = false;
      Rect[] start = texts.Select(t => t.Bounds).ToArray();
      Vector2 grab = texts[1].Bounds.center;
      SetField(editor, "_lastClickTime", -10f);
      Call(editor, "PressElement", b, LayoutHandle.Move, grab);
      Assert(selection.Count == 3 && Field<int>(editor, "_dragging") == b, "pressing one of several keeps them all");
      SetField(editor, "_pressMoved", true);
      Call(editor, "DragGroup", grab + new Vector2(30, 20));
      Call(editor, "FinishDrag");
      settings.LayoutSnapping = snapping;
      for (int i = 0; i < 3; i++)
        Assert(Vector2.Distance(texts[i].Bounds.position, start[i].position + new Vector2(30, 20)) < 1.5f,
          "dragging one moves every selected element the same: " + texts[i].Id);
      Call(editor, "Undo");
      Assert(texts.Select((t, i) => Vector2.Distance(t.Bounds.position, start[i].position)).All(d => d < 1.5f), "one undo puts them all back");
      SetField(editor, "_lastClickTime", -10f);
      Call(editor, "PressElement", b, LayoutHandle.Move, grab);
      Call(editor, "ReleaseDrag");
      Assert(selection.Count == 1 && selection[0] == texts[1], "a click on one of several, without moving, selects it alone");
      foreach (int index in new[] { a, c })
        Call(editor, "ToggleSelection", index);
      Call(editor, "NudgeSelection", 10f, 0f);
      Assert(texts.Select((t, i) => t.Bounds.x - start[i].x).All(d => Mathf.Abs(d - 10) < 1.5f), "arrow keys move every selected element");
      Field<LayoutHistory>(editor, "_history").Commit(host.LayoutSettings);
      Call(editor, "Undo");

      // Align and distribute.
      Call(editor, "UpdateFrames");
      bar = Field<RectTransform>(editor, "_settingsPanel");
      Assert(arrange(LayoutAction.DistributeHorizontal).interactable, "three can be spaced evenly");
      arrange(LayoutAction.AlignLeft).onClick.Invoke();
      float left = start.Min(r => r.x);
      Assert(texts.All(t => Mathf.Abs(t.Bounds.x - left) < 1.5f), "align left lines them up on the leftmost edge");
      Assert(selection.Count == 3, "aligning keeps the selection");
      SaveFrame(camera, texture, canvases, name + "-multi");
      Call(editor, "Undo");
      Call(editor, "UpdateFrames");
      bar = Field<RectTransform>(editor, "_settingsPanel");
      arrange(LayoutAction.AlignMiddle).onClick.Invoke();
      Assert(texts.Select(t => t.Bounds.center.y).Max() - texts.Select(t => t.Bounds.center.y).Min() < 1.5f, "align middle shares one vertical center");
      Call(editor, "Undo");
      Call(editor, "UpdateFrames");
      bar = Field<RectTransform>(editor, "_settingsPanel");
      arrange(LayoutAction.DistributeHorizontal).onClick.Invoke();
      Rect[] spaced = texts.Select(t => t.Bounds).OrderBy(r => r.x).ToArray();
      Assert(Mathf.Abs((spaced[1].x - spaced[0].xMax) - (spaced[2].x - spaced[1].xMax)) < 1.5f, "distribute leaves the same gaps: "
        + string.Join(" | ", start.Select(r => r.ToString())) + " -> " + string.Join(" | ", spaced.Select(r => r.ToString()))
        + " ids " + string.Join(",", texts.Select(t => t.Id)));
      Call(editor, "Undo");
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "undo after arranging restores every element");

      // A column already evenly spaced: the press says so and changes nothing.
      IOverlayLayoutTarget[] column = new[] { "progress", "accuracy", "time" }
        .Select(id => host.LayoutTargets.First(t => t.Id == id)).ToArray();
      Call(editor, "SelectOnly", IndexOf(editor, column[0]));
      Call(editor, "ToggleSelection", IndexOf(editor, column[1]));
      Call(editor, "ToggleSelection", IndexOf(editor, column[2]));
      Call(editor, "UpdateFrames");
      bar = Field<RectTransform>(editor, "_settingsPanel");
      Rect[] columnStart = column.Select(t => t.Bounds).ToArray();
      bool couldUndo = Field<LayoutHistory>(editor, "_history").CanUndo;
      arrange(LayoutAction.DistributeVertical).onClick.Invoke();
      Call(editor, "UpdateToolbar");
      TextMeshProUGUI help = Field<TextMeshProUGUI>(editor, "_keyboardHelp");
      Assert(column.Select((t, i) => t.Bounds == columnStart[i]).All(same => same) && help.text.Length > 0
        && help.color == SettingsUiFactory.Foreground && Field<LayoutHistory>(editor, "_history").CanUndo == couldUndo,
        "spacing an evenly spaced column says it already is, without an undo step: " + help.text);
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "nothing moved");
      foreach (int index in new[] { a, b, c })
        Call(editor, "ToggleSelection", index);
      foreach (IOverlayLayoutTarget member in column)
        Call(editor, "ToggleSelection", IndexOf(editor, member));
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 3 && texts.All(selection.Contains), "back to the three spread texts");

      // Delete and select all.
      Call(editor, "DeleteSelected");
      Assert(texts.All(t => !t.Enabled) && selection.Count == 0, "delete removes every selected element");
      Call(editor, "Undo");
      Assert(texts.All(t => t.Enabled), "one undo brings them all back");
      Call(editor, "SelectAll");
      Call(editor, "UpdateFrames");
      int selectable = host.LayoutTargets.Count(t => t.Available && t.Enabled);
      Assert(selection.Count >= selectable && selection.All(t => t.Available && t.Enabled), "Cmd/Ctrl + A selects every element shown");
      Call(editor, "SelectOnly", -1);
      Call(editor, "UpdateFrames");
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "multi-select leaves the layout as it was");
    }

    private static void CheckKeyViewers(OverlayLayoutEditor editor, PreviewKeyViewers viewers)
    {
      viewers.Availability = KeyViewerAvailability.Ready;
      Call(editor, "ToggleAddMenu");
      Field<RectTransform>(editor, "_addMenu").GetComponentsInChildren<Button>(true).Single(button => button.name == "Add KeyViewer").onClick.Invoke();
      IOverlayLayoutTarget hand = viewers.Targets.Last();
      Assert(hand.TemplateId == OverlayElementKinds.HandKeyViewer && Field<int>(editor, "_selected") == IndexOf(editor, hand),
        "a key viewer is added at once, as a hand viewer");
      Call(editor, "UpdateFrames");
      SettingsSegmentedControl kind = Field<SettingsSegmentedControl>(editor, "_kindControl");
      Assert(kind.Selected == 0, "key viewer settings show hand selected");
      kind.Select(1, true);
      IOverlayLayoutTarget foot = viewers.Targets.Last();
      Assert(foot.TemplateId == OverlayElementKinds.FootKeyViewer && !hand.Enabled && foot.Bounds.center == hand.Bounds.center,
        "switching to foot replaces the viewer in place");
      Assert(Field<int>(editor, "_selected") == IndexOf(editor, foot), "the switched viewer stays selected");
      Call(editor, "UpdateFrames");
      Assert(!Field<Button>(editor, "_editContent").gameObject.activeSelf && Field<TMP_InputField>(editor, "_scaleInput").gameObject.activeSelf,
        "key viewers keep their scale");
      RectTransform popup = Field<RectTransform>(editor, "_settingsPanel");
      Assert(popup != null && popup.gameObject.activeSelf && popup.Find("Drag element toolbar") != null,
        "a key viewer's toolbar has a grip to move it");
      Rect barBefore = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Vector2 grab = barBefore.position + new Vector2(14, 20);
      Call(editor, "BeginElementBarDrag", grab);
      Call(editor, "MoveElementBar", grab + new Vector2(-60, -40));
      SetField(editor, "_elementBarDragging", false);
      Rect barAfter = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Assert(Mathf.Abs(barAfter.x - (barBefore.x - 60)) < .5f && Mathf.Abs(barAfter.y - (barBefore.y - 40)) < .5f,
        "the element toolbar follows its grip one to one");
      int footIndex = IndexOf(editor, foot);
      Select(editor, -1);
      Select(editor, footIndex);
      Rect barKept = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Assert(barKept.position == barAfter.position, "the element toolbar keeps its place for the next selection");
      var cutouts = editor.NativeExclusions;
      Assert(cutouts.Count == 2, "native viewers get each panel as its own cutout: " + cutouts.Count);
    }

    private static void CheckTextEditor(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      TextOverlayElement fps = host.LayoutTargets.OfType<TextOverlayElement>().Single(text => text.Id == "fps");
      Select(editor, IndexOf(editor, fps));
      ElementSettings preferences = Field<ElementSettings>(editor, "_preferences");
      preferences.ReduceUiMotion = false;
      Call(editor, "UpdateToolbar");
      Field<Button>(editor, "_editContent").onClick.Invoke();
      OverlayTextEditor text = Field<OverlayTextEditor>(editor, "_textEditor");
      Assert(text.IsOpen && editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "Edit text opens the editor over everything");
      OverlayEditorFrame screen = Field<OverlayEditorFrame>(text, "_screen");
      Assert(!screen.Root.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text == "텍스트 편집" || label.text == "Edit text"),
        "the editor has no title card");
      Assert(Field<CanvasGroup>(screen, "_fade").alpha < 1, "the editor fades in instead of cutting in");
      SetField(screen, "_openedAt", -10f); screen.Tick();
      Assert(Field<CanvasGroup>(screen, "_fade").alpha == 1, "the fade finishes");
      OverlayInputDock dock = Field<OverlayInputDock>(text, "_dock");
      Assert(dock.Root.gameObject.activeInHierarchy && dock.Owner == null && dock.HintText.Length > 0,
        "the input dock is always there, resting with a hint");
      RectTransform format = Field<RectTransform>(text, "_formatBar");
      OverlayBarButton sizeButton = Field<OverlayBarButton>(text, "_sizeButton"), colorButton = Field<OverlayBarButton>(text, "_colorButton"),
        textGrow = Field<OverlayBarButton>(text, "_growButton");
      Button[] barButtons = format.GetComponentsInChildren<Button>(true);
      Button growButton = textGrow.Button;
      Assert(sizeButton.Root.IsChildOf(format) && colorButton.Root.IsChildOf(format) && textGrow.Root.IsChildOf(format) && barButtons.Length == 3
        && text.GetType().GetField("_opacityField", Private) == null && textGrow.Value.Length > 0,
        "size, color and grow point share one bar; no opacity, no + Value");
      string[] textLabels = Korean(name) ? new[] { "크기", "글자 색", "기준점" } : new[] { "Size", "Text color", "Grow from" };
      OverlayBarButton[] textBar = { sizeButton, colorButton, textGrow };
      for (int i = 0; i < textBar.Length; i++)
      {
        Assert(textBar[i].Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == textLabels[i]),
          "each bar button says what it is: " + textLabels[i]);
        Assert(textBar[i].Button.targetGraphic.transform == textBar[i].Root, "a bar button lights up where it is drawn");
      }
      Assert(!barButtons.Any(button => button.name.EndsWith("smaller") || button.name.EndsWith("larger")), "numbers have no − and + buttons");
      foreach (TextMeshProUGUI label in format.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "format bar label fits: " + label.text);
      }
      float actual = fps.EffectiveScale * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      Assert(Mathf.Abs(Field<float>(text, "_previewScale") - actual) < .001f, "the edited text is shown at its game size");
      OverlayTextDocument document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 7 && document[6].IsValue, "the editor reads FPS | {fps}");
      Assert(sizeButton.Value.Length > 0 && !sizeButton.Computed, "the size button shows the size");
      SaveFrame(camera, texture, canvases, name + "-text");
      preferences.ReduceUiMotion = true;

      // With nothing being calculated, a value from the dock goes in at the caret.
      SetField(text, "_caretIndex", 7); SetField(text, "_anchor", 7);
      Call(text, "Type", " x");
      Call(text, "Render");
      TextMeshProUGUI preview = Field<TextMeshProUGUI>(text, "_preview");
      Rect dockRect = ScreenRectOf(dock.Root);
      Assert(InsideScreen(dockRect) && dockRect.yMax < ScreenRectOf(preview.rectTransform).yMin, "the text stays above the dock");
      Button[] chips = dock.Content.GetComponentsInChildren<Button>(true);
      Assert(chips.Length == OverlayValueCatalog.All.Count(value => !OverlayValueCatalog.IsLegacy(value))
        && chips.All(chip => chip.GetComponent<OverlayValueChipHandle>() != null), "every value is laid out, each draggable");
      TextMeshProUGUI[] categories = dock.Content.GetComponentsInChildren<TextMeshProUGUI>(true).Where(label => label.transform.parent == dock.Content).ToArray();
      Assert(categories.Length == OverlayValueCatalog.Categories.Length && categories.All(label => label.rectTransform.anchoredPosition.x == 0),
        "category names line up in one column");
      float column = categories.Max(label => label.rectTransform.sizeDelta.x);
      Assert(chips.All(chip => ((RectTransform)chip.transform).anchoredPosition.x >= column - .5f), "values wrap to the right of the names");
      foreach (TextMeshProUGUI label in dock.Content.GetComponentsInChildren<TextMeshProUGUI>(true))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "value label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-text-values");
      chips.Single(chip => chip.name == "Value combo").onClick.Invoke();
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 10 && document[9].Value == "combo" && Field<int>(text, "_caretIndex") == 10 && Field<int>(text, "_anchor") == 10,
        "a clicked value lands at the caret, and typing goes on after it");
      chips.Single(chip => chip.name == "Value miss").onClick.Invoke();
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 11 && document[9].Value == "combo" && document[10].Value == "miss", "a second value follows the first");
      Call(text, "Undo");
      Call(text, "OpenChip", 9);
      OverlayFormulaEditing editing = Field<OverlayFormulaEditing>(text, "_editing");
      Assert(dock.Owner == "value:9" && editing.Active && editing.Slot.IsText, "clicking a value in the text calculates it in the dock");

      // The dock shows the formula; the text shows the result.
      Call(text, "KeypadPress", '*');
      Call(text, "KeypadPress", '2');
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document[9].Value == "{combo}*2" && preview.text.Contains("240") && !preview.text.Contains("×"),
        "the text shows the result: " + document[9].Value);
      Assert(dock.FormulaText.Contains("×") && dock.ResultText.Contains("240"), "the dock shows the formula and its result");
      Call(text, "KeypadPress", '+');
      Vector2 line = ScreenCenter((RectTransform)dock.Root.Find("Formula line"));
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.Miss), line);
      Call(text, "EndValueDrag", line);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document[9].Value == "{combo}*2+{miss}", "a value dropped on the formula line joins it: " + document[9].Value);
      Assert(dockRect.yMax < ScreenRectOf(preview.rectTransform).yMin, "the dock never covers the text");
      SaveFrame(camera, texture, canvases, name + "-text-keypad");
      dock.Keypad.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == "Key done").onClick.Invoke();
      Assert(dock.Owner == null, "Done lets the dock rest and leaves the text alone");
      Call(text, "UpdateHover", ScreenCenterOfItem(text, 9));
      Assert(screen.TipText.Contains("×") && screen.TipText.Contains("+"), "hovering a value names it: " + screen.TipText);
      Call(text, "OpenChip", 9);
      Assert(dock.Owner == "value:9" && Field<OverlayFormulaEditing>(text, "_editing").Working.Serialize() == "{combo}*2+{miss}",
        "clicking a value reopens its formula");
      Call(text, "ClosePopovers");
      Call(text, "Render");

      // Dragging a value into the text marks where it lands.
      Vector3[] previewCorners = new Vector3[4];
      preview.rectTransform.GetWorldCorners(previewCorners);
      Vector2 end = RectTransformUtility.WorldToScreenPoint(null, previewCorners[2]) + new Vector2(20, -4);
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.Fps), end);
      Assert(Field<Image>(text, "_dropCaret").enabled, "a dragged value shows where it will land");
      Assert(screen.Root.transform.Find("Controls/Dragged value") != null, "the value follows the pointer");
      SaveFrame(camera, texture, canvases, name + "-text-drag");
      Call(text, "EndValueDrag", end);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 11 && document[10].Value == "fps", "a dropped value lands in the text");
      Call(text, "Undo");
      Call(text, "ClosePopovers");

      // The size in the bar: words are refused; values size the selection.
      SetField(text, "_anchor", 9); SetField(text, "_caretIndex", 10);
      Call(text, "Render");
      Vector2 sizePoint = ScreenCenter(sizeButton.Root);
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.SongTitle), sizePoint);
      Call(text, "EndValueDrag", sizePoint);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(screen.NoticeText.Length > 0 && document[9].SizeFormula == "", "the song title is refused by the size");
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.ProgressCurrent), sizePoint);
      Call(text, "EndValueDrag", sizePoint);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document[9].SizeFormula == "progress-current" && document[0].SizeFormula == "" && dock.Owner == "size" && sizeButton.Active,
        "the size takes a value for the selection and opens in the dock");
      Assert(sizeButton.Value == "48" && sizeButton.Computed, "the size shows its result, marked as calculated: " + sizeButton.Value);
      Call(text, "ClosePopovers");
      Call(text, "Undo");

      // Sizes: the keypad types pixels into the selection.
      SetField(text, "_anchor", 0); SetField(text, "_caretIndex", 3);
      Call(text, "Render");
      string shown = (25 * fps.EffectiveScale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
      Assert(sizeButton.Value == shown, "the size button shows the selection's size as drawn: " + shown);
      sizeButton.Button.onClick.Invoke();
      Assert(dock.Owner == "size", "the size opens in the dock");
      foreach (char key in "\b\b\b\b40")
        Call(text, "KeypadPress", key);
      Call(text, "ClosePopovers");
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(Mathf.Abs(document[0].Size * fps.EffectiveScale - 40) < .05f && document[3].Size == 0, "a size applies to the selection only");
      colorButton.Button.onClick.Invoke();
      SettingsColorPicker picker = Field<SettingsColorPicker>(text, "_picker");
      Rect pickerRect = ScreenRectOf(picker.Root);
      Assert(picker.Visible && colorButton.Active && picker.Column && dock.Keypad.Root.gameObject.activeInHierarchy
        && Mathf.Abs(pickerRect.xMax - ScreenRectOf(dock.Keypad.Root).xMax) < 1 && pickerRect.yMin >= ScreenRectOf(dock.Root).yMax - 1
        && InsideScreen(pickerRect), "the color picker opens right above the keypad: " + pickerRect);
      Field<SettingsSegmentedControl>(picker, "_source").Select(1, true);
      Field<TMP_InputField>(picker, "_hex").text = "#40C0FF";
      picker.Commit();
      SaveFrame(camera, texture, canvases, name + "-text-color");
      OverlayOpacityField textOpacity = Field<OverlayOpacityField>(text, "_opacity");
      Assert(textOpacity.Field.Root.gameObject.activeInHierarchy && textOpacity.Field.Text == "100", "the text color's opacity is a number in the picker");
      Vector2 alphaPoint = ScreenCenter(textOpacity.Field.Root);
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.SongTitle), alphaPoint);
      Call(text, "EndValueDrag", alphaPoint);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document[0].Color.AlphaFormula == "" && screen.NoticeText.Length > 0 && picker.Visible, "words are refused by the opacity");
      Call(text, "BeginValueDrag", OverlayValueCatalog.Get(OverlayValueId.ProgressCurrent), alphaPoint);
      Call(text, "EndValueDrag", alphaPoint);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(picker.Visible && document[0].Color.AlphaFormula == "progress-current" && document[3].Color.AlphaFormula == ""
        && dock.Owner == "opacity" && textOpacity.Field.Text == "48" && textOpacity.Field.Computed,
        "the picker's opacity takes a value for the selection");
      Assert(picker.Visible && dock.Keypad.Root.gameObject.activeInHierarchy, "its formula is typed on the keypad below, the picker staying open");
      Assert(picker.Commit() && Field<OverlayTextDocument>(text, "_document")[0].Color.AlphaFormula == "progress-current"
        && Field<OverlayTextDocument>(text, "_document")[0].Color.Hex == "#40C0FF", "confirming the color keeps the calculated opacity");
      picker.Hide();
      Assert(!colorButton.Active && dock.Owner == null && dock.Keypad.Root.gameObject.activeSelf,
        "Text color lets go once the picker closes, and so does its opacity; the keypad is back");

      // Where the text grows from.
      growButton.onClick.Invoke();
      OverlayGrowPopover grow = Field<OverlayGrowPopover>(text, "_growPopover");
      Assert(grow.Visible && InsideScreen(ScreenRectOf(grow.Root)), "the grow point opens its own popover, on screen");
      Call(text, "PickGrowth", OverlayGrowth.Right);
      Assert(Field<SettingsCapsuleGraphic>(text, "_growMark").gameObject.activeSelf, "the text marks the point that stays put");
      SaveFrame(camera, texture, canvases, name + "-text-grow");
      Call(text, "ClosePopovers");

      Call(text, "DeleteRange", 8, 9);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 9 && document[8].Value == "{combo}*2+{miss}", "deleting next to a value removes one step");
      Call(text, "Undo");
      Assert(Field<OverlayTextDocument>(text, "_document").Count == 10, "the text editor has its own undo");
      text.Close(true, true);
      Assert(!text.IsOpen && fps.Settings.Text.Runs.Any(run => run.Value == "{combo}*2+{miss}") && !fps.Settings.Text.Runs[0].Color.Highlight
        && fps.Settings.Text.Runs[0].Color.Hex == "#40C0FF" && fps.Settings.Text.Runs[0].Size > 0 && fps.Settings.GrowFrom == OverlayGrowth.Right,
        "Done saves the runs with their colors, sizes, formulas and grow point");
      Assert(Field<TextMeshProUGUI>(fps, "_text").text.Contains("240"), "the overlay calculates the saved formula with samples");
      Call(editor, "Undo");
      Assert(!fps.Settings.Text.Runs.Any(run => run.Value.Contains("combo")) && fps.Settings.GrowFrom == OverlayGrowth.Auto,
        "a text edit is one layout undo step");

      TextOverlayElement progress = host.LayoutTargets.OfType<TextOverlayElement>().Single(element => element.Id == "progress");
      Select(editor, IndexOf(editor, progress));
      Call(editor, "OpenTextEditor", progress, false);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(!document[0].IsValue && Enumerable.Range(0, document.Count).Any(i => document[i].Value == "progress-current"),
        "a converted label opens as plain words");
      text.Close(false, true);

      Call(editor, "AddText");
      Assert(text.IsOpen && Field<OverlayTextDocument>(text, "_document").Count == 0, "Add Element → Text opens an empty editor");
      RectTransform hint = Field<TextMeshProUGUI>(text, "_placeholder").rectTransform, emptyCaret = Field<Image>(text, "_emptyCaret").rectTransform;
      Assert(hint.gameObject.activeSelf && emptyCaret.gameObject.activeSelf && emptyCaret.anchoredPosition.x < hint.anchoredPosition.x
        && Mathf.Abs((emptyCaret.anchoredPosition.y - emptyCaret.sizeDelta.y * .5f) - (hint.anchoredPosition.y - hint.sizeDelta.y * .5f)) < 1,
        "an empty text shows its caret just before the hint, on one line");
      SaveFrame(camera, texture, canvases, name + "-text-new");
      TextOverlayElement added = host.LayoutTargets.OfType<TextOverlayElement>().Last();
      text.Close(false, true);
      Assert(!added.Enabled, "cancelling a new text removes it");
    }

    private static Rect ScreenRectOf(RectTransform rect)
    {
      Vector3[] corners = new Vector3[4];
      rect.GetWorldCorners(corners);
      Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]), max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
      return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Vector2 ScreenCenterOfItem(OverlayTextEditor text, int item)
    {
      object[] args = { item, null };
      text.GetType().GetMethod("ItemRect", Private).Invoke(text, args);
      Rect local = (Rect)args[1];
      TextMeshProUGUI preview = Field<TextMeshProUGUI>(text, "_preview");
      return RectTransformUtility.WorldToScreenPoint(null, preview.rectTransform.TransformPoint(local.center));
    }

    private static void CheckHighlightColor(OverlayLayoutEditor editor, ElementSettings settings, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      Call(editor, "ToggleColor");
      OverlayColorPopover popover = Field<OverlayColorPopover>(editor, "_colorPopover");
      SettingsColorPicker controls = Field<SettingsColorPicker>(popover, "_picker");
      Assert(popover.Visible && !Field<SettingsSegmentedControl>(controls, "_source").Root.gameObject.activeSelf,
        "the highlight color itself has no Highlight/Custom choice");
      TMP_InputField hex = Field<TMP_InputField>(controls, "_hex");
      string before = settings.HighlightColor;
      hex.text = "#12";
      Assert(settings.HighlightColor == before && !popover.Commit(), "an incomplete color cannot commit");
      hex.text = "#204060";
      Assert(popover.Commit() && settings.HighlightColor == "#204060", "a valid highlight commits");
      SaveFrame(camera, texture, canvases, name + "-color");
      popover.Hide();
    }

    private static bool PopoverInside(OverlayLayoutEditor editor, RectTransform popover, int width, int height)
    {
      Rect bounds = (Rect)editor.GetType().GetMethod("ScreenRect", Private).Invoke(editor, new object[] { popover });
      return bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= width + .5f && bounds.yMax <= height + .5f;
    }

    private static void Select(OverlayLayoutEditor editor, int index)
    {
      SetField(editor, "_selected", index);
      Call(editor, "UpdateFrames");
    }

    private static int IndexOf(OverlayLayoutEditor editor, IOverlayLayoutTarget target) =>
      (int)editor.GetType().GetMethod("IndexOf", Private).Invoke(editor, new object[] { target });

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    private static void Cleanup(OverlayLayoutEditor editor, GameObject root, GameObject eventsRoot, GameObject cameraRoot,
      RenderTexture texture, OverlayCanvasHost host)
    {
      if (editor.IsOpen)
        editor.Close(false);
      editor.Dispose();
      Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot);
      Object.DestroyImmediate(eventsRoot);
      if (host.Root != null)
        Object.DestroyImmediate(host.Root);
    }

    // Overlay canvases render through the camera only while a frame is captured,
    // so element geometry stays in the same screen pixels the editor measures.
    private static Action _refresh;
    private static void SaveFrame(Camera camera, RenderTexture target, Canvas[] canvases, string name)
    {
      _refresh?.Invoke();
      foreach (Canvas canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
      canvases[0].planeDistance = 2;
      Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
      Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      RenderTexture.active = null; Object.DestroyImmediate(image);
      foreach (Canvas canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      Canvas.ForceUpdateCanvases();
    }
    private static void SaveFrame(Camera camera, RenderTexture target, int width, int height, string name)
    {
      Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
      Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      RenderTexture.active = null; Object.DestroyImmediate(image);
    }
    private static Vector3 CenterIn(RectTransform child, Transform parent) => parent.InverseTransformPoint(child.TransformPoint(child.rect.center));

    // ElementNote's viewers without ElementNote: fixed rectangles that move as told.
    private sealed class PreviewKeyViewers : IKeyViewerElements
    {
      private readonly List<IOverlayLayoutTarget> _targets = new List<IOverlayLayoutTarget>();
      private readonly int _width;
      private int _count;
      public PreviewKeyViewers(int width) { _width = width; }
      public IReadOnlyList<IOverlayLayoutTarget> Targets => _targets;
      public IReadOnlyCollection<string> DefaultIds => new string[0];
      public KeyViewerAvailability Availability { get; set; } = KeyViewerAvailability.Ready;
      public IOverlayLayoutTarget Add(string templateId)
      {
        var target = new ExternalPreviewTarget(new Rect(_width * .5f - 105, 200, 210, 140), templateId == OverlayElementKinds.FootKeyViewer ? "foot" : "hand", ++_count);
        _targets.Add(target);
        return target;
      }
      public IOverlayLayoutTarget Switch(IOverlayLayoutTarget target, string templateId)
      {
        if (target.TemplateId == templateId) return null;
        var created = (ExternalPreviewTarget)Add(templateId);
        created.SetBounds(target.Bounds);
        target.SetEnabled(false);
        return created;
      }
    }
    private sealed class ExternalPreviewTarget : IOverlayLayoutTarget, IExternalLayoutOutline, IOverlayLayoutPointer
    {
      private readonly string _kind;
      public ExternalPreviewTarget(Rect bounds, string kind, int number)
      { Bounds = bounds; _kind = kind; Settings = new OverlayLayoutSettings { Id = "native-" + kind + ":" + number, TemplateId = "native-" + kind }; }
      public readonly OverlayLayoutSettings Settings;
      public string Id => Settings.Id;
      public string TemplateId => Settings.TemplateId;
      public string Label => _kind == "hand" ? "Hand viewer" : "Foot viewer";
      public Rect Bounds { get; private set; }
      public Rect SelectionBounds => new Rect(Bounds.x, Bounds.y + Bounds.height * .6f, Bounds.width, Bounds.height * .4f);
      public bool Available => true;
      public bool Enabled => Settings.Enabled;
      public bool OwnsSelectionOutline => true;
      public float EffectiveScale => 1;
      public bool Selected { get; private set; }
      public Vector2 PointerInLayoutSpace(Vector2 pointer) => pointer;
      public void SetSelected(bool selected) { Selected = selected; }
      public void SetEnabled(bool enabled) { Settings.Enabled = enabled; }
      public void SetBounds(Rect bounds) { Bounds = bounds; }
      public void CommitLayout(bool reselectAnchor, bool initialize = false) { }
      public void SetPreview(bool preview) { }
      public void RefreshLayout() { }
      public void ResetLayout() { }
    }
    private static void Assert(bool condition, string name)
    {
      if (!condition) throw new InvalidOperationException("UI preview assertion: " + name);
      _checks++;
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void CheckMigration()
    {
      var fresh = new ElementSettings();
      Assert(fresh.HighlightColor == "#BA712B" && OverlayTheme.DefaultHighlight == "#BA712B", "new settings and theme use brown default");
      var legacy = new ElementSettings { HighlightColor = " #7cce00 " };
      legacy.Migrate();
      Assert(legacy.HighlightColor == "#BA712B" && legacy.SettingsVersion >= 1, "old default migrates once");
      legacy.HighlightColor = "#7CCE00"; legacy.Migrate();
      Assert(legacy.HighlightColor == "#7CCE00", "explicit green after migration is preserved");
      var custom = new ElementSettings { HighlightColor = "#336699" };
      custom.Migrate(); Assert(custom.HighlightColor == "#336699", "migration preserves custom color");
      foreach (bool hidden in new[] { false, true })
      foreach (int version in new[] { 0, 1 })
      {
        var old = new ElementSettings { SettingsVersion = version, HidePerfectJudgmentText = hidden };
        old.Migrate();
        Assert(old.Gameplay.Judgement.HidePerfect == hidden && old.Gameplay.Judgement.HideXPerfect == hidden,
          "outer legacy Perfect visibility migrates to Perfect +/- and XP before normalization: " + version + " / " + hidden);
        old.Gameplay.Judgement.HidePerfect = !hidden;
        old.HidePerfectJudgmentText = !hidden;
        old.Migrate();
        Assert(old.Gameplay.Judgement.HidePerfect == !hidden && old.Gameplay.Judgement.HideXPerfect == hidden,
          "subsequent settings loads preserve independent XP visibility");
      }
      var released = new ElementSettings { SettingsVersion = 3 };
      released.OverlayLayouts.Add(new OverlayLayoutSettings
      {
        Id = "accuracy",
        TemplateId = OverlayElementKinds.Text,
        Text = new OverlayTextSettings { Runs = { OverlayTextRun.ValueRun("accuracy", OverlayColorSettings.HighlightColor()) } },
      });
      released.Migrate();
      released.Migrate();
      Assert(released.SettingsVersion == 4 && released.OverlayLayouts[0].Text.Runs.Count == 2 && released.OverlayLayouts[0].Text.Runs[1].Text == "%",
        "1.1.0 texts get their units back once");
      var presets = new ElementSettings { SettingsVersion = 2 };
      presets.Migrate();
      Assert(presets.OverlayLayouts.Single(layout => layout.Id == "accuracy").Text.Runs.Count(run => run.Text == "%") == 1,
        "presets from before 1.1 convert straight to the new form");
      foreach (bool hidden in new[] { false, true })
      {
        var grouped = new ElementSettings { SettingsVersion = 2 };
        grouped.Gameplay.Judgement.HidePerfect = hidden;
        grouped.Migrate();
        Assert(grouped.Gameplay.Judgement.HidePerfect == hidden && grouped.Gameplay.Judgement.HideXPerfect == hidden,
          "existing gameplay Perfect group migrates its XP visibility without consulting the obsolete outer switch");
      }
    }

    private static void CheckChatterBlocking(SettingsPanel panel, ElementSettings settings, int width)
    {
      ChatterBlockerOptions chatter = settings.Gameplay.Chatter;
      SettingsValueRow windowRow = Field<SettingsValueRow>(panel, "_chatterWindow");
      Slider window = windowRow.Slider;
      Button toggle = Field<Button>(panel, "_chatterSwitch");
      Assert(chatter.Enabled && window.interactable, "the chatter blocker starts on with its window ready");
      Assert(window.minValue == 5 && window.maxValue == 150 && window.wholeNumbers, "the window is whole milliseconds from 5 to 150");
      Assert(chatter.WindowMs == ChatterFilter.DefaultWindowMs, "building the page keeps the saved window");
      Assert(window.value == chatter.WindowMs, "the slider starts at the saved window");
      TMP_InputField input = windowRow.Field;
      CheckInputCaret(input, "chatter window", "", "5", "150");
      Assert(input.interactable && input.text == chatter.WindowMs.ToString(), "the typed window starts at the saved value");
      toggle.onClick.Invoke();
      Assert(!chatter.Enabled && !window.interactable && !input.interactable, "the window waits while blocking is off");
      toggle.onClick.Invoke();
      Assert(chatter.Enabled && window.interactable && input.interactable, "turning blocking on enables the window");
      window.value = 33;
      Assert(chatter.WindowMs == 33 && input.text == "33", "the slider saves and shows the window");
      input.onEndEdit.Invoke("72");
      Assert(chatter.WindowMs == 72 && window.value == 72, "a typed window saves and moves the slider");
      input.onEndEdit.Invoke("400");
      Assert(chatter.WindowMs == 150 && window.value == 150 && input.text == "150", "a window above the range becomes the maximum");
      input.onEndEdit.Invoke("2");
      Assert(chatter.WindowMs == 5 && window.value == 5 && input.text == "5", "a window below the range becomes the minimum");
      input.onEndEdit.Invoke("");
      Assert(chatter.WindowMs == 5 && input.text == "5", "clearing the field restores the saved window");
      window.SetValueWithoutNotify(chatter.WindowMs = ChatterFilter.DefaultWindowMs);
      input.SetTextWithoutNotify(chatter.WindowMs.ToString());
      Assert(chatter.Enabled, "the page ends in the default state, blocking on");
      RectTransform row = windowRow.Row;
      Assert(row.rect.width <= width, "the window row fits the page");
      RectTransform field = (RectTransform)input.transform, slider = (RectTransform)window.transform;
      Assert(field.anchoredPosition.x + field.rect.width <= row.rect.width, "the typed window fits inside the row");
      Assert(slider.anchoredPosition.x + slider.rect.width <= field.anchoredPosition.x, "the slider ends before the typed window");
    }

    private static void Render(int width, int height, SystemLanguage language, int page, string name)
    {
      RDString.language = language;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      Assert(font != null, "overlay font exists");
      ElementSettings settings = new ElementSettings { ReduceUiMotion = true };
      OverlayTheme.SetHighlight(settings.HighlightColor);
      bool overlayOpened = false;
      using var planetPreview = new PlanetPreviewProbe();
      SettingsPanel panel = new SettingsPanel(settings, font, () => {}, () => true, () => overlayOpened = true, _ => {}, planetPreview.Render, updates: new PreviewUpdates());
      Assert(planetPreview.Calls == 0, "constructing settings does not render a hidden planet preview");
      {
        GameObject root = Field<GameObject>(panel, "_root");
        root.SetActive(true);
        Canvas canvas = root.GetComponent<Canvas>();
        GameObject cameraRoot = new GameObject("UI preview camera", typeof(Camera));
        Camera camera = cameraRoot.GetComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .055f);
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Field<CanvasGroup>(panel, "_windowGroup").alpha = 1;
        Button[] cards = Field<Button[]>(panel, "_homeCards");
        Assert(cards.Length == 4, "home offers Overlay, Tweaks, Effects and General");
        cards[1].onClick.Invoke();
        Assert(Field<int>(panel, "_page") == 1 && Field<Button>(panel, "_back").gameObject.activeSelf, "Tweaks opens within settings");
        Button back = Field<Button>(panel, "_back");
        Assert(back.targetGraphic.color.a == 0 && back.transition == Selectable.Transition.None,
          "Back has no button background or hover fill");
        SettingsNavigationIcon backIcon = back.GetComponentInChildren<SettingsNavigationIcon>();
        Assert(backIcon != null && backIcon.Kind == 4 && !backIcon.raycastTarget,
          "Back uses a left chevron with the full button hit area");
        CheckNavigationControls(panel, settings);
        Call(panel, "Back");
        Assert(Field<int>(panel, "_page") == 0, "Back returns to home");
        cards[2].onClick.Invoke();
        Assert(Field<int>(panel, "_page") == 3, "Effects opens within settings");
        Call(panel, "Back");
        cards[3].onClick.Invoke();
        Assert(Field<int>(panel, "_page") == 2, "General opens within settings");
        Call(panel, "Back");
        cards[0].onClick.Invoke();
        Assert(overlayOpened && !root.activeSelf, "Overlay card hands off directly to overlay editor");
        root.SetActive(true);
        GameObject[] pages = Field<GameObject[]>(panel, "_pages");
        Assert(pages[1].GetComponentsInChildren<SettingsSwitchMotion>(true).Length ==
          (UnityEngine.Application.platform == RuntimePlatform.OSXEditor || UnityEngine.Application.platform == RuntimePlatform.OSXPlayer ? 2 : 1),
          "Tweaks contains trackpad only on macOS");
        Assert(pages[2].GetComponentsInChildren<SettingsSwitchMotion>(true).Length == 3
          && Field<Button>(panel, "_capture").transform.IsChildOf(pages[2].transform),
          "General contains shortcut, motion, ElementNote auto open and automatic update preferences");
        Assert(!root.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("Hand and foot viewers") || t.text.Contains("Connect & preview")),
          "legacy viewer switch and connection card are absent");
        Assert(planetPreview.Calls == 0, "home, Tweaks, Effects and General do not render planet textures");
        Call(panel, "SelectPage", page);
        if (page == 7) Call(panel, "EditPreset", settings.Effects.Presets[0]);
        panel.Resize(width, height);
        Canvas.ForceUpdateCanvases();
        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(false)) label.ForceMeshUpdate();
        if (page == 2) CheckShortcut(panel, settings);
        if (page == 3) CheckPresetEditing(panel, settings, width, height);
        if (page == 4) CheckEditorSections(panel, settings, width, height);
        if (page == 5) CheckJudgmentChoices(panel, settings);
        if (page == 6) CheckPlanetEditing(panel, settings, width, height, planetPreview);
        if (page == 8) CheckChatterBlocking(panel, settings, width);
        RectTransform window = Field<RectTransform>(panel, "_window");
        Assert(window.rect.width * canvas.scaleFactor <= width && window.rect.height * canvas.scaleFactor <= height, "window fits viewport");
        Assert(canvas.scaleFactor >= 1, "text is not reduced below authored size");
        if (page == 0)
        {
          RectTransform first = (RectTransform)cards[0].transform, second = (RectTransform)cards[1].transform,
            third = (RectTransform)cards[2].transform, fourth = (RectTransform)cards[3].transform;
          bool stacked = ((RectTransform)pages[0].transform).rect.width < 480;
          Assert(stacked ? second.anchoredPosition.y < first.anchoredPosition.y
            : second.anchoredPosition.y == first.anchoredPosition.y && third.anchoredPosition.x == first.anchoredPosition.x
              && fourth.anchoredPosition.y == third.anchoredPosition.y && third.anchoredPosition.y < first.anchoredPosition.y,
            "categories form a two-by-two desktop grid in the requested order and stack only on compact windows");
          foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(false))
            Assert(!label.isTextTruncated, "home label fits: " + label.text);
        }
        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(false)) Assert(label.font == font, "shared overlay font");
        foreach (RectTransform copy in root.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == "Switch copy"))
        {
          RectTransform row = (RectTransform)copy.parent;
          RectTransform toggle = (RectTransform)row.Find("Switch");
          Assert(Mathf.Abs(CenterIn(copy, row).y - row.rect.center.y) < .1f
            && Mathf.Abs(CenterIn(toggle, row).y - row.rect.center.y) < .1f, "switch copy and control share card center");
          foreach (TextMeshProUGUI text in copy.GetComponentsInChildren<TextMeshProUGUI>())
            Assert(!text.isTextTruncated, "switch copy fits intrinsic text height");
        }
        SettingsUiFactory ui = Field<SettingsUiFactory>(panel, "_ui");
        foreach (string sample in new[] { "#000000", "#FFFFFF", "#0000FF", "#FF0000", "#BA712B" })
        {
          OverlayTheme.SetHighlight(sample); ui.RefreshTheme();
          Color c = SettingsPalette.SoftAccent.linear;
          float lum = c.r * .2126f + c.g * .7152f + c.b * .0722f;
          Assert(lum >= .45f, "readable UI accent for " + sample);
          foreach (TextMeshProUGUI action in Field<TextMeshProUGUI[]>(panel, "_cardActions"))
            Assert(action.color == SettingsPalette.SoftAccent, "category action follows current highlight without tween lag");
        }
        OverlayTheme.SetHighlight(settings.HighlightColor); ui.RefreshTheme();
        if (name == "editor-editing-en-720")
          Field<System.Collections.Generic.List<Button>>(panel, "_editorTabButtons")[1].onClick.Invoke();
        // Force layout after checks; colors must be rendered using the same live components.
        panel.Resize(width, height); Canvas.ForceUpdateCanvases();
        Field<ScrollRect>(panel, "_scroll").SendMessage("LateUpdate");
        // ScrollRect intentionally forces scrollbars visible outside play mode.
        // Match its play-mode overflow rule for the offline render.
        ScrollRect scroll = Field<ScrollRect>(panel, "_scroll");
        scroll.verticalScrollbar.gameObject.SetActive(scroll.content.rect.height > scroll.viewport.rect.height + 1);
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(image); camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(cameraRoot);
        root.SetActive(false);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(Field<Sprite>(ui, "_rounded"));
        Object.DestroyImmediate(Field<Texture2D>(ui, "_texture"));
      }
    }

    private static void CheckPresetEditing(SettingsPanel panel, ElementSettings settings, int width, int height)
    {
      int originalCount = settings.Effects.Presets.Count;
      var name = Field<TMP_InputField>(panel, "_presetName");
      var save = Field<Button>(panel, "_savePreset");
      var error = Field<TextMeshProUGUI>(panel, "_presetError");
      Field<Button>(panel, "_newPreset").onClick.Invoke();
      Assert(Field<int>(panel, "_page") == 7 && !save.interactable
        && !Field<Button>(panel, "_deletePreset").gameObject.activeSelf,
        "new preset opens a dedicated draft with no destructive action");
      name.text = " " + settings.Effects.Presets[0].Name.ToLowerInvariant() + " ";
      Assert(!save.interactable && error.text.Length > 0 && settings.Effects.Presets.Count == originalCount,
        "preset names are unique after whitespace and case normalization");
      name.text = "Fixture calm";
      CheckInputCaret(name, "preset name", "", "Remove All", "한글 이름", "gjpq");
      var options = Field<List<Button>>(panel, "_effectRows");
      options.First(button => button.gameObject.activeInHierarchy).onClick.Invoke();
      Assert(save.interactable && Field<EffectPreset>(panel, "_presetDraft").Effects != RemovedEffects.None,
        "draft effect choices are editable before a preset exists");
      Call(panel, "Back");
      Assert(Field<int>(panel, "_page") == 3 && settings.Effects.Presets.Count == originalCount,
        "leaving an unsaved preset draft does not mutate the gallery");
      Field<Button>(panel, "_newPreset").onClick.Invoke();
      name.text = "Fixture calm";
      options.First(button => button.gameObject.activeInHierarchy).onClick.Invoke();
      RemovedEffects expected = Field<EffectPreset>(panel, "_presetDraft").Effects;
      SettingsValueRow zoom = Field<SettingsValueRow>(panel, "_cameraZoom");
      Assert((expected & RemovedEffects.CameraMotion) != 0 && zoom.Row.gameObject.activeSelf && zoom.Field.interactable
        && zoom.Field.text == "250", "removing camera movement offers its zoom, starting at 250%");
      RectTransform zoomRect = zoom.Row, cameraRect = (RectTransform)options[0].transform;
      float zoomTop = -zoomRect.anchoredPosition.y, zoomBottom = zoomTop + zoomRect.rect.height;
      Assert(Mathf.Approximately(cameraRect.rect.width, zoomRect.rect.width)
        && zoomTop >= -cameraRect.anchoredPosition.y + cameraRect.rect.height,
        "the zoom sits right under camera movement");
      Assert(options.Skip(1).Where(button => button.gameObject.activeSelf)
        .All(button => -((RectTransform)button.transform).anchoredPosition.y >= zoomBottom),
        "the other Screen effects start below the zoom instead of covering it");
      zoom.Field.onEndEdit.Invoke("1200");
      Assert(Field<EffectPreset>(panel, "_presetDraft").CameraZoom == 1000 && zoom.Field.text == "1000"
        && zoom.Slider.value == 1000, "a zoom above the range becomes the maximum");
      zoom.Field.onEndEdit.Invoke("50");
      Assert(Field<EffectPreset>(panel, "_presetDraft").CameraZoom == 100 && zoom.Field.text == "100",
        "a zoom below the range becomes the minimum");
      zoom.Field.onEndEdit.Invoke("300");
      Assert(zoom.Slider.value == 300, "a typed zoom moves the slider");
      save.onClick.Invoke();
      EffectPreset created = settings.Effects.Presets.Single(preset => preset.Name == "Fixture calm");
      Assert(created.CameraZoom == 300, "saving keeps the preset's zoom");
      Assert(settings.Effects.Presets.Count == originalCount + 1 && created.Effects == expected
        && Field<int>(panel, "_page") == 3, "saving creates the named preset and returns to its gallery");
      Field<List<Button>>(panel, "_presetCards").Single(button => button.name == "Preset: Fixture calm").onClick.Invoke();
      Assert(settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id,
        "selecting a preset applies it and enables removal");
      // The toggle rebuilds retained gallery controls. Their layout must survive
      // that callback itself, without Resize/Back/FitWindow rescuing the result.
      Rect[] cardRects = Field<List<Button>>(panel, "_presetCards")
        .Select(button => { var r = (RectTransform)button.transform; return new Rect(r.anchoredPosition, r.rect.size); }).ToArray();
      Field<Button>(panel, "_effectEnabled").onClick.Invoke();
      Assert(!settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id,
        "turning removal off preserves the selected preset");
      CheckPresetCardRects(panel, cardRects, "off");
      Field<Button>(panel, "_effectEnabled").onClick.Invoke();
      Assert(settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id,
        "turning removal back on preserves the active preset");
      CheckPresetCardRects(panel, cardRects, "on");
      Field<Button>(panel, "_effectEnabled").onClick.Invoke();
      CheckPresetCardRects(panel, cardRects, "off again");
      Call(panel, "EditPreset", created);
      name.text = settings.Effects.Presets[0].Name;
      Assert(!save.interactable && created.Name == "Fixture calm", "duplicate rename cannot mutate the existing preset");
      name.text = "Fixture renamed"; save.onClick.Invoke();
      Assert(created.Name == "Fixture renamed" && settings.Effects.ActivePresetId == created.Id,
        "renaming keeps stable identity and selected state");
      Call(panel, "EditPreset", created);
      Field<Button>(panel, "_deletePreset").onClick.Invoke();
      Assert(settings.Effects.Presets.Count == originalCount && settings.Effects.ActivePreset != null
        && !settings.Effects.Enabled && Field<int>(panel, "_page") == 3,
        "deleting the selected preset keeps a valid fallback without enabling removal");
      panel.Resize(width, height); Canvas.ForceUpdateCanvases();
      List<Button> cards = Field<List<Button>>(panel, "_presetCards");
      RectTransform first = (RectTransform)cards[0].transform;
      float galleryWidth = Field<RectTransform>(panel, "_presetGallery").rect.width;
      int columns = galleryWidth >= 600 ? 3 : galleryWidth >= 440 ? 2 : 1;
      Assert(cards.Take(columns).All(button => ((RectTransform)button.transform).anchoredPosition.y == first.anchoredPosition.y)
        && (cards.Count <= columns || ((RectTransform)cards[columns].transform).anchoredPosition.y < first.anchoredPosition.y),
        "preset gallery uses three desktop columns and adapts without overlap on narrow windows");
    }

    private static void CheckPresetCardRects(SettingsPanel panel, Rect[] expected, string state)
    {
      List<Button> cards = Field<List<Button>>(panel, "_presetCards");
      Assert(cards.Count == expected.Length, "effect toggle retains every preset card: " + state);
      var positions = new HashSet<Vector2>();
      for (int i = 0; i < cards.Count; i++)
      {
        RectTransform rect = (RectTransform)cards[i].transform;
        Assert(cards[i].gameObject.activeInHierarchy && new Rect(rect.anchoredPosition, rect.rect.size) == expected[i],
          "effect toggle preserves each visible card's rectangle before relayout: " + state);
        Assert(positions.Add(rect.anchoredPosition), "effect toggle cannot pile preset cards onto one origin: " + state);
      }
    }

    private static void CheckEditorSections(SettingsPanel panel, ElementSettings settings, int width, int height)
    {
      var tabs = Field<List<Button>>(panel, "_editorTabButtons");
      RectTransform selection = Field<RectTransform>(panel, "_selectionPane");
      RectTransform editing = Field<RectTransform>(panel, "_editingPane");
      Assert(selection.gameObject.activeSelf && !editing.gameObject.activeSelf,
        "editor suboptions use one focused section rather than one long list");
      tabs[1].onClick.Invoke(); panel.Resize(width, height);
      Assert(!selection.gameObject.activeSelf && editing.gameObject.activeSelf, "editor section navigation swaps its suboptions");
      var editor = settings.Gameplay.Editor;
      var rotation = Field<TMP_InputField>(panel, "_rotationInput");
      editor.FineRotation = false; Field<SettingsUiFactory>(panel, "_ui").RefreshTheme();
      Assert(!rotation.interactable, "rotation step is disabled when fine rotation is off");
      Field<List<Button>>(panel, "_editingOptions")[0].onClick.Invoke();
      Assert(editor.FineRotation && rotation.interactable, "enabling fine rotation activates its nested step control");
      CheckInputCaret(rotation, "rotation step", "", "22.5");
      float before = editor.RotationStep;
      Call(panel, "CommitRotationStep", "not an angle");
      Assert(editor.RotationStep == before && Field<TextMeshProUGUI>(panel, "_rotationError").text.Length > 0,
        "invalid custom rotation leaves the previous value intact");
      Call(panel, "CommitRotationStep", "7.5");
      Assert(Mathf.Abs(editor.RotationStep - 7.5f) < .001f, "valid custom rotation is applied");
      tabs[0].onClick.Invoke(); panel.Resize(width, height); Canvas.ForceUpdateCanvases();
    }

    private static void CheckJudgmentChoices(SettingsPanel panel, ElementSettings settings)
    {
      var rows = Field<List<Button>>(panel, "_judgementOptions");
      var judgment = settings.Gameplay.Judgement;
      Func<bool>[] hidden = {
        () => judgment.HideEarlyPerfect, () => judgment.HidePerfect, () => judgment.HideXPerfect,
        () => judgment.HideLatePerfect, () => judgment.HideTooEarly, () => judgment.HideEarly,
        () => judgment.HideLate, () => judgment.HideTooLate, () => judgment.HideMiss, () => judgment.HideOverload
      };
      Assert(rows.Count == hidden.Length, "ten judgment controls expose XP separately from Perfect +/-");
      Call(panel, "SetAllJudgements", false);
      Assert(hidden.All(read => !read()), "Show all includes XP and every other judgment");
      for (int i = 0; i < rows.Count; i++)
      {
        rows[i].onClick.Invoke();
        Assert(hidden.Select((read, index) => read() == (index == i)).All(value => value),
          "each judgment card changes only its own visibility: " + i);
        rows[i].onClick.Invoke();
        Assert(hidden.All(read => !read()), "each judgment can independently return to shown: " + i);
      }
      Call(panel, "SetAllJudgements", true);
      Assert(hidden.All(read => read()), "Hide all includes XP and every other judgment");
      rows[1].onClick.Invoke();
      Assert(!judgment.HidePerfect && judgment.HideXPerfect && judgment.HideMiss,
        "showing Perfect +/- leaves XP and failures hidden");
      rows[2].onClick.Invoke();
      Assert(!judgment.HidePerfect && !judgment.HideXPerfect && judgment.HideMiss,
        "showing XP leaves Perfect +/- and failures unchanged");
      Call(panel, "SetAllJudgements", false);
      Assert(hidden.All(read => !read()), "Show all restores all ten judgment types after independent editing");
    }

    private static void CheckPlanetEditing(SettingsPanel panel, ElementSettings settings, int width, int height, PlanetPreviewProbe preview)
    {
      var enabled = Field<Button>(panel, "_planetEnabled");
      var sources = Field<List<Button>>(panel, "_planetSourceButtons");
      var colors = Field<List<Button>>(panel, "_planetColorButtons");
      var tabs = Field<List<Button>>(panel, "_planetTabButtons");
      var ui = Field<SettingsUiFactory>(panel, "_ui");
      var image = Field<RawImage>(panel, "_planetImage");
      Assert(preview.Calls > 0 && preview.Index == 0 && ReferenceEquals(preview.Profile, settings.Gameplay.Planets.Red),
        "opening Planets immediately requests the Fire profile from the injected game renderer");
      CheckPlanetTexture(panel, preview);
      settings.Gameplay.Planets.Enabled = false; ui.RefreshTheme();
      Assert(colors.All(button => !button.interactable), "disabled planets cannot open appearance editors");
      int before = preview.Calls;
      enabled.onClick.Invoke();
      Assert(preview.Calls > before, "enabling customization refreshes the planet preview immediately");
      var profile = Field<Button>(panel, "_planetProfileEnabled");
      if (!colors[0].interactable) profile.onClick.Invoke();
      Assert(colors.All(button => button.interactable), "enabling the planet profile activates body, trail and ring controls");
      before = preview.Calls;
      sources[0].onClick.Invoke();
      Assert(preview.Calls > before && !preview.Snapshot.Body.OverrideColor,
        "switching Body to map colors immediately reaches the preview renderer");
      colors[0].onClick.Invoke();
      var picker = Field<SettingsColorPicker>(panel, "_planetPicker");
      var shade = Field<Button>(panel, "_pickerShade");
      Assert(picker.Visible && shade.gameObject.activeSelf, "planet color opens an anchored picker with a dismiss backdrop");
      before = preview.Calls;
      Field<TMP_InputField>(picker, "_opacity").text = "50";
      Assert(preview.Calls > before && preview.Snapshot.Body.Opacity == 50 && !preview.Snapshot.Body.OverrideColor,
        "Body opacity reaches the renderer while preserving its map color source");
      before = preview.Calls;
      Field<TMP_InputField>(picker, "_hex").text = "2468AC";
      Assert(preview.Calls > before && preview.Snapshot.Body.ColorHex == "#2468AC"
        && preview.Snapshot.Body.Opacity == 50 && preview.Snapshot.Body.OverrideColor,
        "Body RGB edits immediately select custom color without losing opacity");
      Assert(picker.Commit(), "planet opacity can be committed through the shared RGBA picker");
      Button close = picker.Root.GetComponentsInChildren<Button>(false).Single(button => button.name == "Close color picker");
      close.onClick.Invoke();
      Assert(!picker.Visible && !shade.gameObject.activeSelf, "closing the color picker also removes its input-blocking shade");
      string[] partHex = { "2468AC", "426688", "6842AA" };
      string[] partOpacity = { "50", "25", "0" };
      for (int i = 1; i < colors.Count; i++)
      {
        colors[i].onClick.Invoke();
        before = preview.Calls;
        Field<TMP_InputField>(picker, "_hex").text = partHex[i];
        Field<TMP_InputField>(picker, "_opacity").text = partOpacity[i];
        PlanetComponentAppearance part = i == 1 ? preview.Snapshot.Tail : preview.Snapshot.Ring;
        Assert(preview.Calls > before && part.ColorHex == "#" + partHex[i]
          && part.Opacity == float.Parse(partOpacity[i]), "each component routes RGB and alpha to the renderer immediately: " + i);
        Assert(preview.Snapshot.Body.ColorHex == "#2468AC" && preview.Snapshot.Body.Opacity == 50,
          "editing Trail or Ring leaves Body appearance independent");
        close.onClick.Invoke();
      }
      PlanetAppearanceProfile[] profiles = { settings.Gameplay.Planets.Red, settings.Gameplay.Planets.Blue, settings.Gameplay.Planets.Green };
      for (int i = 1; i < tabs.Count; i++)
      {
        before = preview.Calls;
        tabs[i].onClick.Invoke();
        Assert(preview.Calls > before && preview.Index == i && ReferenceEquals(preview.Profile, profiles[i]),
          "switching planet tabs immediately requests the corresponding profile: " + i);
        CheckPlanetTexture(panel, preview);
        string previousFire = settings.Gameplay.Planets.Red.Body.ColorHex;
        colors[0].onClick.Invoke();
        before = preview.Calls;
        string hex = i == 1 ? "80A0C0" : "20C080";
        Field<TMP_InputField>(picker, "_hex").text = hex;
        Assert(preview.Calls > before && preview.Index == i && preview.Snapshot.Body.ColorHex == "#" + hex
          && settings.Gameplay.Planets.Red.Body.ColorHex == previousFire,
          "editing the selected planet cannot mutate Fire or render a stale tab");
        close.onClick.Invoke();
      }
      before = preview.Calls;
      profile.onClick.Invoke();
      Assert(preview.Calls > before && !preview.Snapshot.Enabled && colors.All(button => !button.interactable),
        "profile enable state is immediately reflected without changing the selected planet");
      profile.onClick.Invoke();
      preview.Available = false;
      Call(panel, "RefreshPlanetPreview");
      Assert(!image.enabled && image.texture == null && Field<TextMeshProUGUI>(panel, "_planetPreviewLabel").text.Length > 0,
        "unavailable game artwork clears stale imagery and exposes its fallback message");
      preview.Available = true;
      Call(panel, "RefreshPlanetPreview");
      CheckPlanetTexture(panel, preview);
      colors[1].onClick.Invoke();
      Call(panel, "SelectPage", 1);
      Assert(!picker.Visible && !shade.gameObject.activeSelf, "leaving Planets cannot leave its picker or shade behind");
      before = preview.Calls;
      ui.RefreshTheme(); Call(panel, "RefreshPlanetPreview");
      Assert(preview.Calls == before, "leaving Planets stops rendering even when unrelated settings refresh");
      Call(panel, "SelectPage", 6);
      GameObject root = Field<GameObject>(panel, "_root");
      root.SetActive(false); before = preview.Calls;
      ui.RefreshTheme(); Call(panel, "RefreshPlanetPreview");
      Assert(preview.Calls == before, "closing settings suspends planet rendering while retaining the selected page");
      root.SetActive(true);
      tabs[0].onClick.Invoke();
      Call(panel, "SelectPage", 6); panel.Resize(width, height); Canvas.ForceUpdateCanvases();
      CheckPlanetTexture(panel, preview);
    }

    private static void CheckPlanetTexture(SettingsPanel panel, PlanetPreviewProbe preview)
    {
      RawImage image = Field<RawImage>(panel, "_planetImage");
      RectTransform card = Field<RectTransform>(panel, "_planetPreview");
      Texture texture = preview.Texture;
      Rect artwork = image.rectTransform.rect;
      Assert(image.enabled && image.texture == texture && image.color == Color.white && !image.raycastTarget,
        "planet artwork displays the renderer's actual texture without a schematic substitute or extra tint");
      Assert(Mathf.Abs(artwork.width / artwork.height - texture.width / (float)texture.height) < .001f
        && artwork.width <= card.rect.width - 32 + .1f && artwork.height <= card.rect.height - 64 + .1f,
        "wide, tall and square renderer textures fit the preview without stretching or covering its caption");
    }

    // This probe checks the UI/render-service boundary only. The deliberately
    // rectangular test textures are not ADOFAI artwork or a game-render test.
    // The longest status: a download that outlasted startup.
    private sealed class PreviewUpdates : IModUpdates
    {
      public ModUpdateStatus Status => new ModUpdateStatus(ModUpdateState.Downloading, "0.3.0", "0.3.1");
      public bool AutomaticUpdates { get; set; } = true;
    }

    private sealed class PlanetPreviewProbe : IDisposable
    {
      private readonly Texture2D[] _textures = new Texture2D[3];
      public int Calls, Index;
      public bool Available = true;
      public PlanetAppearanceProfile Profile, Snapshot;
      public Texture Texture => _textures[Index];

      public Texture Render(int index, PlanetAppearanceProfile profile)
      {
        Calls++; Index = index; Profile = profile;
        Snapshot = new PlanetAppearanceProfile {
          Enabled = profile.Enabled, Body = Copy(profile.Body), Tail = Copy(profile.Tail), Ring = Copy(profile.Ring)
        };
        if (!Available) return null;
        if (_textures[index] == null)
        {
          int width = index == 1 ? 48 : 96, height = index == 0 ? 48 : 96;
          var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
            name = "Fixture-only renderer texture " + index, filterMode = FilterMode.Point
          };
          var pixels = new Color[width * height];
          for (int y = 0; y < height; y++)
          for (int x = 0; x < width; x++)
            pixels[y * width + x] = (x / 12 + y / 12) % 2 == 0 ? new Color(.32f, .38f, .43f) : new Color(.15f, .19f, .23f);
          texture.SetPixels(pixels); texture.Apply(); _textures[index] = texture;
        }
        return _textures[index];
      }

      private static PlanetComponentAppearance Copy(PlanetComponentAppearance value) => new() {
        ColorHex = value.ColorHex, Opacity = value.Opacity, OverrideColor = value.OverrideColor
      };

      public void Dispose()
      {
        foreach (Texture2D texture in _textures) if (texture != null) Object.DestroyImmediate(texture);
      }
    }

    private static void CheckNavigationControls(SettingsPanel panel, ElementSettings settings)
    {
      var eventRoot = new GameObject("Navigation fixture events", typeof(EventSystem));
      var pointer = new PointerEventData(eventRoot.GetComponent<EventSystem>());
      Button back = Field<Button>(panel, "_back"), close = Field<Button>(panel, "_close");
      var icon = back.GetComponentInChildren<SettingsNavigationIcon>();
      var feedback = back.GetComponent<SettingsIconInteraction>();
      feedback.OnPointerEnter(pointer);
      Assert(icon.color == SettingsPalette.SoftAccent && back.targetGraphic.color.a == 0,
        "borderless back glyph gains the soft highlight on pointer hover");
      feedback.OnPointerDown(pointer);
      Assert(icon.color != SettingsPalette.SoftAccent && icon.rectTransform.localScale == Vector3.one,
        "pointer down responds immediately and respects reduced motion");
      feedback.OnPointerUp(pointer);
      feedback.OnPointerExit(pointer);
      Assert(icon.color == SettingsUiFactory.Foreground, "back glyph restores its neutral color on exit");
      Assert(close.targetGraphic.color.a == 0 && close.GetComponentInChildren<SettingsNavigationIcon>().Kind == 5
        && !close.GetComponentsInChildren<TextMeshProUGUI>(false).Any(), "Close uses the same borderless icon treatment");
      Assert(back.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Pointer
        && close.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Pointer,
        "navigation controls advertise their click cursor");
      CheckAntialias(icon);
      var iconRoot = new GameObject("Low resolution action icon", typeof(RectTransform), typeof(LayoutActionIcon));
      var action = iconRoot.GetComponent<LayoutActionIcon>();
      action.rectTransform.sizeDelta = new Vector2(16, 16);
      action.color = Color.white;
      action.Kind = LayoutAction.Reset;
      CheckAntialias(action);
      using (var mesh = new VertexHelper())
      {
        action.GetType().GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(action, new object[] { mesh });
        Assert(mesh.currentVertCount < 400, "curved icons use a joined strip rather than a disk at every segment");
      }
      Object.DestroyImmediate(iconRoot);

      var motion = Field<SettingsWindowMotion>(panel, "_windowMotion");
      var window = Field<RectTransform>(panel, "_window");
      var group = Field<CanvasGroup>(panel, "_windowGroup");
      Vector2 resting = window.anchoredPosition;
      settings.ReduceUiMotion = true;
      motion.Show();
      Assert(group.alpha == 1 && window.anchoredPosition == resting && !motion.enabled,
        "reduced-motion window opens immediately at its resting position");
      settings.ReduceUiMotion = false;
      motion.Show();
      Assert(window.localScale == Vector3.one && group.alpha == 0 && window.anchoredPosition.y < resting.y,
        "window entrance uses a small settle without scaling text");
      motion.GetType().GetField("_elapsed", Private).SetValue(motion, .3f);
      Call(motion, "Update");
      Assert(group.alpha == 1 && window.anchoredPosition == resting && !motion.enabled,
        "window entrance settles exactly and stops updating");
      motion.Show();
      settings.ReduceUiMotion = true;
      Call(motion, "Update");
      Assert(window.anchoredPosition == resting && group.alpha == 1, "enabling reduced motion interrupts the entrance cleanly");
      Object.DestroyImmediate(eventRoot);
    }

    private static void CheckAntialias(Graphic graphic)
    {
      using (var mesh = new VertexHelper())
      {
        graphic.GetType().GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
        bool clearEdge = false, solidCore = false;
        var vertex = new UIVertex();
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
          mesh.PopulateUIVertex(ref vertex, i);
          clearEdge |= vertex.color.a == 0;
          solidCore |= vertex.color.a == 255;
          Assert(!float.IsNaN(vertex.position.x) && !float.IsNaN(vertex.position.y), "icon geometry remains finite at low resolution");
        }
        Assert(clearEdge && solidCore, "icon includes a transparent coverage fringe around its solid stroke");
      }
    }

    private static UIVertex[] GenerateNativeCaret(TMP_InputField input, int position)
    {
      // TMP only creates its runtime selection renderer in play mode. Exercise
      // its actual mesh generator offline and skip only the OS IME setter.
      TMP_Text text = input.textComponent;
      typeof(TMP_InputField).GetField("m_CaretVisible", Private).SetValue(input, true);
      typeof(TMP_InputField).GetField("m_CaretPosition", Private).SetValue(input, position);
      typeof(TMP_InputField).GetField("m_CaretSelectPosition", Private).SetValue(input, position);
      typeof(TMP_InputField).GetField("m_forceRectTransformAdjustment", Private).SetValue(input, false);
      typeof(TMP_InputField).GetField("m_HasTextBeenRemoved", Private).SetValue(input, false);
      typeof(TMP_InputField).GetField("m_ShouldUpdateIMEWindowPosition", Private).SetValue(input, false);
      typeof(TMP_InputField).GetField("m_PreviousIMEInsertionLine", Private).SetValue(input, text.textInfo.characterInfo[position].lineNumber);
      using (var mesh = new VertexHelper())
      {
        typeof(TMP_InputField).GetMethod("GenerateCaret", Private).Invoke(input, new object[] { mesh, Vector2.zero });
        Assert(mesh.currentVertCount == 4, "native TMP caret produces a complete quad");
        var vertices = new UIVertex[4];
        for (int i = 0; i < 4; i++) mesh.PopulateUIVertex(ref vertices[i], i);
        return vertices;
      }
    }

    private static void CheckInputCaret(TMP_InputField input, string context, params string[] samples)
    {
      string original = input.text;
      Vector2 originalPosition = input.textComponent.rectTransform.anchoredPosition;
      FieldInfo focus = typeof(TMP_InputField).GetField("m_AllowInput", Private);
      bool wasFocused = (bool)focus.GetValue(input);
      focus.SetValue(input, false);
      foreach (string sample in samples)
      {
        input.SetTextWithoutNotify(sample);
        Canvas.ForceUpdateCanvases(); input.textComponent.ForceMeshUpdate();
        TMP_Text text = input.textComponent;
        Assert(text.textInfo.characterCount >= sample.Length + 1 && !text.isTextTruncated,
          "editable characters and the terminal caret position survive rendering: " + context);
        int end = text.textInfo.characterCount - 1;
        foreach (int position in new[] { 0, end / 2, end }.Distinct())
        {
          UIVertex[] caret = GenerateNativeCaret(input, position);
          TMP_CharacterInfo character = text.textInfo.characterInfo[position == 0 ? 0 : position - 1];
          float bottom = caret.Min(vertex => vertex.position.y), top = caret.Max(vertex => vertex.position.y);
          Assert(Mathf.Abs(bottom - character.descender) < .05f && Mathf.Abs(top - character.ascender) < .05f,
            "native caret shares the displayed text's line metrics: " + context + " / " + sample + " / " + position);
          Vector3 lower = input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(new Vector3(0, bottom)));
          Vector3 upper = input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(new Vector3(0, top)));
          Assert(lower.y >= input.textViewport.rect.yMin - .1f && upper.y <= input.textViewport.rect.yMax + .1f
            && top - bottom >= text.fontSize * .5f,
            "the complete caret stays inside the viewport instead of clipping above the text: " + context);
          float left = caret.Min(vertex => input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(vertex.position)).x);
          float right = caret.Max(vertex => input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(vertex.position)).x);
          Assert(left >= input.textViewport.rect.xMin - .1f && right <= input.textViewport.rect.xMax + .1f,
            "the caret stays inside the field sideways too: " + context + " / " + sample + " / " + position);
        }
        if (sample.Length > 0)
        {
          typeof(TMP_InputField).GetField("m_StringPosition", Private).SetValue(input, 0);
          typeof(TMP_InputField).GetField("m_StringSelectPosition", Private).SetValue(input, sample.Length);
          using (var selection = new VertexHelper())
          {
            typeof(TMP_InputField).GetMethod("GenerateHighlight", Private).Invoke(input, new object[] { selection, Vector2.zero });
            Assert(selection.currentVertCount == 4, "single-line text selection remains a native quad: " + context);
            var low = new UIVertex(); var high = new UIVertex();
            selection.PopulateUIVertex(ref low, 0); selection.PopulateUIVertex(ref high, 2);
            Assert(Mathf.Abs(low.position.y - text.textInfo.lineInfo[0].descender) < .05f
              && Mathf.Abs(high.position.y - text.textInfo.lineInfo[0].ascender) < .05f,
              "selection uses the same vertical line as the caret: " + context);
          }
          TMP_CharacterInfo first = text.textInfo.characterInfo[0];
          Vector3 local = new(Mathf.Lerp(first.origin, first.xAdvance, .2f), (first.ascender + first.descender) * .5f);
          Camera camera = text.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : text.canvas.worldCamera;
          Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, text.rectTransform.TransformPoint(local));
          int index = TMP_TextUtilities.GetCursorIndexFromPosition(text, point, camera, out CaretPosition side);
          Assert(index == 0 && side == CaretPosition.Left, "pointer hit testing still resolves the visible first character: " + context);
        }
        input.textComponent.rectTransform.anchoredPosition = originalPosition;
      }
      input.SetTextWithoutNotify(original);
      input.textComponent.rectTransform.anchoredPosition = originalPosition;
      typeof(TMP_InputField).GetField("m_CaretVisible", Private).SetValue(input, false);
      typeof(TMP_InputField).GetField("m_ShouldUpdateIMEWindowPosition", Private).SetValue(input, true);
      focus.SetValue(input, wasFocused);
      Canvas.ForceUpdateCanvases(); input.textComponent.ForceMeshUpdate();
    }

    private static void RenderInputCarets()
    {
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      var root = new GameObject("Input caret fixture", typeof(RectTransform), typeof(Canvas));
      var cameraRoot = new GameObject("Input caret camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = SettingsUiFactory.Background;
      var texture = new RenderTexture(640, 280, 24); camera.targetTexture = texture;
      Canvas canvas = root.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      var ui = new SettingsUiFactory(font);
      ui.Text("Input caret", root.transform, 28, 18, 300, 32, 20);
      string[] samples = { "Remove All", "한글 이름", "" };
      for (int i = 0; i < samples.Length; i++)
      {
        TMP_InputField input = ui.Input("Caret sample", samples[i], root.transform, 28, 64 + i * 64, 584, null, characterLimit: 40);
        CheckInputCaret(input, "visual caret sample", samples[i]);
        TMP_Text text = input.textComponent;
        UIVertex[] vertices = GenerateNativeCaret(input, text.textInfo.characterCount - 1);
        RectTransform rect = ui.Rect("Native TMP caret", input.textViewport, 0, 0, input.textViewport.rect.width, input.textViewport.rect.height);
        SettingsUiFactory.Stretch(rect, 0, 0, 0, 0);
        rect.gameObject.AddComponent<CanvasRenderer>();
        var graphic = rect.gameObject.AddComponent<PreviewNativeCaret>();
        graphic.Vertices = vertices; graphic.raycastTarget = false;
      }
      Canvas.ForceUpdateCanvases();
      SaveFrame(camera, texture, 640, 280, "input-carets");
      ui.Dispose();
      RenderTexture.active = null; camera.targetTexture = null;
      Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(root);
    }

    private static void CheckColorPicker()
    {
      RDString.language = SystemLanguage.English;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      var root = new GameObject("Color picker fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
      var events = new GameObject("Color picker events", typeof(EventSystem));
      var cameraRoot = new GameObject("Color picker camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.05f, .05f, .05f);
      var texture = new RenderTexture(512, 480, 24); camera.targetTexture = texture;
      Canvas canvas = root.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      Color current = new Color(.73f, .44f, .17f, .5f), original = current, theme = OverlayTheme.Accent;
      int changed = 0;
      var picker = new SettingsColorPicker(root.transform, font, () => true);
      picker.Show("Planet color", current, true, value => { current = value; changed++; });
      SettingsUiFactory.Place(picker.Root, 112, 52, SettingsColorPicker.Width, picker.Height);
      var opacity = Field<TMP_InputField>(picker, "_opacity");
      var hex = Field<TMP_InputField>(picker, "_hex");
      var plane = Field<SettingsColorPlane>(picker, "_plane");
      var hue = Field<Slider>(picker, "_hue");
      var alpha = Field<Slider>(picker, "_alpha");
      var error = Field<TextMeshProUGUI>(picker, "_error");
      Assert(changed == 0 && current == original, "opening an RGBA picker does not mutate its consumer");
      Assert(picker.Height == 358 && opacity.gameObject.activeSelf && alpha.gameObject.activeSelf,
        "RGBA mode adds an opacity track and a numeric field beside HEX");
      Assert(plane.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Crosshair
        && hue.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Horizontal
        && alpha.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Horizontal
        && hex.GetComponent<SettingsCursor>().Kind == SettingsCursorKind.Text,
        "color controls expose crosshair, adjustment and text cursor affordances");
      foreach (int percent in new[] { 0, 50, 100 })
      {
        opacity.text = percent.ToString();
        Assert(Mathf.Abs(current.a - percent / 100f) < .001f && picker.Commit(),
          "numeric opacity accepts and commits " + percent + "%");
        Assert(Mathf.Abs(alpha.value - percent / 100f) < .001f, "numeric opacity updates its slider");
      }
      opacity.text = "101";
      Assert(current.a == 1 && error.text == "", "out-of-range partial opacity leaves the last valid color untouched");
      Assert(!picker.Commit() && error.text.Length > 0, "invalid opacity produces a focused inline correction on commit");
      opacity.text = "50";
      Assert(picker.Commit() && error.text == "", "valid opacity clears its validation error");
      hex.text = "204060";
      Assert(Mathf.Abs(current.a - .5f) < .001f && ColorUtility.ToHtmlStringRGB(current) == "204060",
        "HEX changes preserve the independent alpha channel");
      alpha.value = .25f;
      Assert(opacity.text == "25" && Mathf.Abs(current.a - .25f) < .001f,
        "opacity dragging updates the numeric field continuously");
      plane.Changed(.8f, 0); hue.value = .77f; plane.Changed(.8f, .9f);
      Color.RGBToHSV(current, out float h, out float s, out float v);
      Assert(Mathf.Abs(h - .77f) < .01f && Mathf.Abs(current.a - .25f) < .001f,
        "HSV gestures preserve hue through black and retain opacity");
      Assert(OverlayTheme.Accent == theme, "planet color editing cannot change the shared overlay highlight");
      hex.text = "BA712B"; opacity.text = "50"; picker.Commit();
      CheckInputCaret(hex, "HEX color", "", "BA712B");
      CheckInputCaret(opacity, "opacity", "", "0", "50", "100");
      hex.DeactivateInputField(); opacity.DeactivateInputField();
      Canvas.ForceUpdateCanvases();
      foreach (var label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "RGBA picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, 512, 480, "rgba-color-picker");
      picker.Show("Highlight color", current, false, value => current = value);
      Assert(picker.Height == 318 && !alpha.gameObject.activeSelf && !opacity.gameObject.activeSelf,
        "RGB mode stays compact and hides both opacity controls");
      hex.text = "BA712C";
      Assert(current.a == 1, "RGB mode always emits an opaque highlight color");
      picker.Hide();
      Assert(!picker.Visible && !picker.IsTyping && picker.Commit(), "hidden picker releases its editing state");
      picker.Dispose();
      RenderTexture.active = null; camera.targetTexture = null;
      Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(root); Object.DestroyImmediate(events);
    }

    private static void CheckSpacingGuides()
    {
      var root = new GameObject("Spacing fixture", typeof(RectTransform), typeof(LayoutSpacingGraphic));
      var graphic = root.GetComponent<LayoutSpacingGraphic>();
      graphic.rectTransform.sizeDelta = new Vector2(1000, 600);
      graphic.raycastTarget = false;
      var horizontal = new LayoutSpacingGuide(180, 220, 340, 380, 100, 140, 120);
      var vertical = new LayoutSpacingGuide(180, 220, 340, 380, 500, 540, 520);
      graphic.SetGuides(horizontal, vertical, 1);
      using (var mesh = new VertexHelper())
      {
        graphic.GetType().GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
        Assert(mesh.currentVertCount == 80 && root.activeSelf && !graphic.raycastTarget,
          "both axes render two gap markers with end caps and equality ticks without blocking input");
        var vertex = new UIVertex();
        mesh.PopulateUIVertex(ref vertex, 0);
        Assert(Mathf.Approximately(vertex.position.x, -320) && Mathf.Approximately(vertex.position.y, 181),
          "spacing guides map top-left screen coordinates into the Canvas");
      }
      graphic.SetGuides(default, default, 1);
      Assert(!root.activeSelf, "spacing guides disappear when the snap is released or bypassed");
      Object.DestroyImmediate(root);
    }

    private static void CheckShortcut(SettingsPanel panel, ElementSettings settings)
    {
      var capture = (SettingsButton)Field<Button>(panel, "_capture");
      var reset = Field<Button>(panel, "_resetShortcut");
      var frame = Field<RectTransform>(panel, "_captureFrame");
      RectTransform section = Field<RectTransform>(panel, "_shortcutSection");
      Assert(frame.anchoredPosition.x == 16 && frame.rect.height == 44,
        "shortcut control is inset from the card with a consistent hit height");
      Assert(((RectTransform)reset.transform).anchoredPosition.y == frame.anchoredPosition.y
        || ((RectTransform)reset.transform).anchoredPosition.y < frame.anchoredPosition.y - frame.rect.height,
        "Reset shares the control baseline or stacks below on compact windows");
      var keycaps = Field<RectTransform>(panel, "_keycaps");
      foreach (KeyCode key in new[] { KeyCode.BackQuote, KeyCode.Slash, KeyCode.LeftBracket, KeyCode.Backslash, KeyCode.Backspace, KeyCode.PageDown })
      {
        settings.SettingsShortcutKey = key;
        Call(panel, "UpdateShortcutLabel"); Canvas.ForceUpdateCanvases();
        var main = Field<TextMeshProUGUI>(panel, "_mainKey"); main.ForceMeshUpdate();
        RectTransform cap = (RectTransform)main.transform.parent;
        Assert(cap.anchoredPosition.x + cap.rect.width <= keycaps.rect.width + .1f,
          "keycap fits inside the shortcut field: " + key);
        Assert(!main.isTextTruncated && main.textInfo.lineCount == 1,
          "shortcut key stays readable on one line: " + key);
      }
      Assert(SettingsPanel.KeyLabel(KeyCode.BackQuote) == "`" && SettingsPanel.KeyLabel(KeyCode.Backslash) == "\\"
        && SettingsPanel.KeyLabel(KeyCode.Slash) == "/" && SettingsPanel.KeyLabel(KeyCode.Alpha1) == "1",
        "printable keys use their actual characters instead of enum names");
      SettingsKeySymbol symbol = Field<SettingsKeySymbol>(panel, "_modifierSymbol");
      foreach (KeyCode modifier in new[] { KeyCode.LeftCommand, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightShift })
      {
        settings.SettingsShortcutModifier = modifier;
        Call(panel, "UpdateShortcutLabel");
        using (var mesh = new VertexHelper())
        {
          symbol.GetType().GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(symbol, new object[] { mesh });
          Assert(mesh.currentVertCount > 0 && !symbol.raycastTarget,
            "modifier symbol renders without font-atlas dependency: " + modifier);
        }
        Assert(Field<TextMeshProUGUI>(panel, "_modifierSide").text == (modifier == KeyCode.RightAlt || modifier == KeyCode.RightShift ? "R" : "L"),
          "modifier side remains distinguishable");
      }
      var events = new GameObject("Shortcut fixture events", typeof(EventSystem));
      var pointer = new PointerEventData(events.GetComponent<EventSystem>());
      var keyboard = new BaseEventData(events.GetComponent<EventSystem>());
      capture.OnPointerEnter(pointer);
      capture.OnSelect(pointer);
      capture.SetPalette(capture.colors);
      Assert(capture.targetGraphic.canvasRenderer.GetColor() == capture.colors.highlightedColor,
        "actual pointer hover is visibly highlighted");
      capture.OnPointerExit(pointer); capture.SetPalette(capture.colors);
      Assert(capture.targetGraphic.canvasRenderer.GetColor() == capture.colors.normalColor,
        "clicked selection cannot leave hover fill behind after pointer exit");
      var focus = capture.GetComponent<SettingsFocusRing>();
      focus.OnSelect(pointer);
      Assert(!Field<Outline>(focus, "_outline").enabled, "pointer selection does not leave a keyboard focus ring");
      focus.OnSelect(keyboard);
      Assert(Field<Outline>(focus, "_outline").enabled, "keyboard selection retains a visible focus ring");
      focus.OnDeselect(keyboard);
      capture.OnPointerEnter(pointer);
      capture.gameObject.SetActive(false); capture.gameObject.SetActive(true); capture.SetPalette(capture.colors);
      Assert(capture.targetGraphic.canvasRenderer.GetColor() == capture.colors.normalColor,
        "reopening a control clears cached hover state");
      capture.interactable = false; capture.SetPalette(capture.colors);
      Assert(capture.targetGraphic.canvasRenderer.GetColor() == capture.colors.disabledColor,
        "hover fix preserves disabled appearance");
      Assert(capture.GetComponent<CanvasGroup>().alpha <= .5f,
        "disabled controls dim their text and glyphs rather than resembling hover");
      capture.interactable = true; capture.OnDeselect(keyboard); capture.SetPalette(capture.colors);
      Object.DestroyImmediate(events);
      settings.SettingsShortcutKey = KeyCode.Tab; settings.SettingsShortcutModifier = KeyCode.LeftAlt;
      Call(panel, "UpdateShortcutLabel");
    }
  }

  internal sealed class PreviewNativeCaret : MaskableGraphic
  {
    public UIVertex[] Vertices;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
      mesh.Clear();
      if (Vertices != null) mesh.AddUIVertexQuad(Vertices);
    }
  }
}
