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
      RectTransform popup = Field<RectTransform>(editor, "_settingsPanel");
      Assert(popup != null && popup.gameObject.activeSelf, "selecting an element opens its own toolbar");
      Assert(!toolbarRect.GetComponentsInChildren<Button>(true).Any(button => button.name.Contains("settings")),
        "the toolbar has no settings button");
      Assert(popup.GetComponentsInChildren<Button>(true).Any(button => button.name == "Edit text"), "a text's toolbar opens the text editor");
      Assert(popup.Find("Drag element toolbar") != null, "the element toolbar has a grip to move it");
      Rect barBefore = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Vector2 grab = barBefore.position + new Vector2(14, 20);
      Call(editor, "BeginElementBarDrag", grab);
      Call(editor, "MoveElementBar", grab + new Vector2(-60, -40));
      SetField(editor, "_elementBarDragging", false);
      Rect barAfter = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Assert(Mathf.Abs(barAfter.x - (barBefore.x - 60)) < .5f && Mathf.Abs(barAfter.y - (barBefore.y - 40)) < .5f,
        "the element toolbar follows its grip one to one");
      Select(editor, -1);
      Select(editor, fpsIndex);
      popup = Field<RectTransform>(editor, "_settingsPanel");
      Rect barKept = (Rect)editor.GetType().GetProperty("ElementBarBounds", Private).GetValue(editor);
      Assert(barKept.position == barAfter.position, "the element toolbar keeps its place for the next selection");
      Assert(Field<TMP_InputField>(editor, "_textSizeInput").text == "25", "a text's toolbar shows its size in pixels");
      foreach (TextMeshProUGUI label in popup.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "text toolbar label fits: " + label.text);
      }
      Assert(root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 1, "only the selected element has an outline");
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
      IOverlayLayoutTarget fps = host.LayoutTargets.Single(target => target.Id == "fps");
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

      TextOverlayElement fpsText = host.LayoutTargets.OfType<TextOverlayElement>().Single(text => text.Id == "fps");
      Call(editor, "CommitTextSize", fpsText, "30");
      Assert(fpsText.Settings.Text.Runs.All(run => Mathf.Abs(run.Size * fpsText.EffectiveScale - 30) < .05f),
        "the toolbar's size sets the whole text in pixels");
      Call(editor, "CommitTextSize", fpsText, "900");
      Assert(Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("400"), "a size outside 4–400px is refused with the range");
      Call(editor, "Undo");
      CheckShapes(editor, host, camera, texture, canvases, name, screenWidth, screenHeight);
      CheckKeyViewers(editor, viewers);
      CheckTextEditor(editor, host, camera, texture, canvases, name);
      CheckHighlightColor(editor, settings, camera, texture, canvases, name);

      var beforeReset = new LayoutSnapshot(host.LayoutSettings);
      Call(editor, "OpenResetConfirmation");
      Assert(Field<GameObject>(editor, "_modalRoot") != null && editor.NativeExclusionBounds == new Rect(0, 0, screenWidth, screenHeight),
        "Reset all asks first, covering native viewers");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "opening the confirmation changes nothing");
      Call(editor, "CloseModal");
      Call(editor, "RestoreDefaultOverlays");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled)
        && host.LayoutTargets.Single(target => target.Id == "fps").Enabled, "Reset all removes added elements and restores built-ins");
      Call(editor, "Undo");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "Reset all is one undo step");
      editor.Close(false);
      Assert(!editor.IsOpen && settings.HighlightColor == "#BA712B", "cancel restores the opening highlight and exits");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled), "cancel discards added shapes");
      Cleanup(editor, root, eventsRoot, cameraRoot, texture, host);
    }

    private static void CheckShapes(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      Call(editor, "ToggleAddMenu");
      Field<RectTransform>(editor, "_addMenu").GetComponentsInChildren<Button>(true).Single(button => button.name == "Add Shape").onClick.Invoke();
      ShapeOverlayElement shape = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      Assert(shape.Enabled && Field<int>(editor, "_selected") == IndexOf(editor, shape), "a new shape is added and selected");
      Assert(shape.Shape.Kind == OverlayShapeKind.Square, "a new shape starts as a square");
      Call(editor, "UpdateFrames");
      ShapeHandlesGraphic handles = Field<ShapeHandlesGraphic>(editor, "_shapeHandles");
      Assert(handles.gameObject.activeSelf, "a selected shape shows its corner handles");
      SaveFrame(camera, texture, canvases, name + "-shape");

      // Drag the top-left corner outward: the box grows and the other corners stay.
      Rect before = shape.Bounds;
      Vector2 corner = before.position;
      object[] hit = { corner, null, 0 };
      Assert((bool)editor.GetType().GetMethod("TryShapeHandleAt", Private).Invoke(editor, hit), "a corner handle can be grabbed");
      Call(editor, "BeginShapeDrag", hit[1], hit[2], corner);
      SetField(editor, "_pressMoved", true);
      Call(editor, "DragShapeHandle", corner + new Vector2(-40, -20));
      Call(editor, "FinishDrag");
      Rect after = shape.Bounds;
      Assert(Mathf.Abs(after.xMax - before.xMax) < 1 && Mathf.Abs(after.yMax - before.yMax) < 1 && after.width > before.width + 30,
        "moving a corner grows the box toward it and keeps the others");
      Assert(OverlayShapeGeometry.IsEdited(shape.Shape), "a moved corner counts as an edit");

      // Round one corner from its handle.
      List<Vector2> radii = Field<List<Vector2>>(editor, "_handleRadii");
      Call(editor, "CollectHandles", shape);
      Vector2 radiusHandle = radii[2];
      Call(editor, "BeginShapeDrag", Enum.Parse(hit[1].GetType(), "Radius"), 2, radiusHandle);
      SetField(editor, "_pressMoved", true);
      Vector2 inward = (shape.Bounds.center - radiusHandle).normalized;
      Call(editor, "DragShapeHandle", radiusHandle + inward * 30);
      Call(editor, "FinishDrag");
      Assert(shape.Shape.Points[2].Radius > 5, "dragging a radius handle rounds only that corner");
      Assert(shape.Shape.Points[0].Radius == 0 && shape.Shape.Points[1].Radius == 0, "other corners stay sharp");
      SaveFrame(camera, texture, canvases, name + "-shape-edited");

      // A click on a handle without moving opens its exact values.
      Call(editor, "OpenShapeValues", shape, Enum.Parse(hit[1].GetType(), "Corner"), 1, LayoutHandle.None);
      LayoutValuePopover values = Field<LayoutValuePopover>(editor, "_valuePopover");
      Assert(values.Visible && PopoverInside(editor, values.Root, width, height), "corner values open on screen");
      SaveFrame(camera, texture, canvases, name + "-shape-values");
      TMP_InputField[] inputs = Field<TMP_InputField[]>(values, "_inputs");
      inputs[0].text = "";
      Assert(!values.Commit() && Field<TextMeshProUGUI>(values, "_error").text.Length > 0, "an invalid number explains the range");
      Rect sized = shape.Bounds;
      values.Hide();
      Call(editor, "OpenShapeValues", shape, Enum.Parse(hit[1].GetType(), "None"), -1, LayoutHandle.Right);
      inputs[0].text = Mathf.Round(shape.Shape.Width * shape.EffectiveScale + 50).ToString(System.Globalization.CultureInfo.InvariantCulture);
      Assert(values.Commit(), "a valid width applies");
      float grown = 50 * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      Assert(Mathf.Abs(shape.Bounds.x - sized.x) < 1 && Mathf.Abs(shape.Bounds.width - sized.width - grown) < 1.5f
        && Mathf.Abs(shape.Bounds.height - sized.height) < 1,
        "an exact width stretches from the left edge: " + sized + " -> " + shape.Bounds + " typed " + inputs[0].text);

      // Settings: kind, fill and border.
      Call(editor, "UpdateFrames");
      RectTransform panel = Field<RectTransform>(editor, "_settingsPanel");
      Assert(panel != null && PopoverInside(editor, panel, width, height), "shape settings open on screen");
      foreach (TextMeshProUGUI label in panel.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "shape settings label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-settings");
      Call(editor, "ChooseShapeKind", shape, OverlayShapeKind.Triangle);
      Assert(Field<GameObject>(editor, "_modalRoot") != null && shape.Shape.Kind == OverlayShapeKind.Square,
        "changing an edited shape asks before dropping its edits");
      SaveFrame(camera, texture, canvases, name + "-shape-confirm");
      Field<GameObject>(editor, "_modalRoot").GetComponentsInChildren<Button>().Last().onClick.Invoke();
      Assert(shape.Shape.Kind == OverlayShapeKind.Triangle && shape.Shape.Points.Count == 3 && !OverlayShapeGeometry.IsEdited(shape.Shape),
        "confirming changes the kind and resets its corners");
      Call(editor, "Undo");
      Assert(shape.Shape.Kind == OverlayShapeKind.Square && OverlayShapeGeometry.IsEdited(shape.Shape), "the kind change is undoable");
      Call(editor, "UpdateFrames");
      Assert(Field<SettingsSegmentedControl>(editor, "_kindControl").Selected == 0, "the shape toolbar follows an undone kind");
      Call(editor, "OpenShapeColor", shape, false);
      SettingsColorPicker picker = Field<SettingsColorPicker>(editor, "_elementPicker");
      Assert(picker.Visible && Field<SettingsSegmentedControl>(picker, "_source").Selected == 0, "fill starts on the highlight color");
      Assert(!Field<TMP_InputField>(picker, "_hex").gameObject.activeSelf && Field<TMP_InputField>(picker, "_opacity").gameObject.activeSelf,
        "highlight colors edit only their opacity");
      SaveFrame(camera, texture, canvases, name + "-shape-fill-highlight");
      Field<SettingsSegmentedControl>(picker, "_source").Select(1, true);
      Assert(Field<TMP_InputField>(picker, "_hex").gameObject.activeSelf && !shape.Shape.Fill.Highlight, "Custom Color edits the full color");
      Field<TMP_InputField>(picker, "_hex").text = "#2050A0";
      Assert(picker.Commit() && shape.Shape.Fill.Hex == "#2050A0", "a custom fill applies");
      SaveFrame(camera, texture, canvases, name + "-shape-fill-custom");
      picker.Hide();
      Call(editor, "CommitStrokeWidth", shape, "500");
      Assert(shape.Shape.StrokeWidth == 0 && Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("100"), "a border width outside 0–100 is refused");
      Call(editor, "CommitStrokeWidth", shape, "6");
      Assert(shape.Shape.StrokeWidth == 6, "a border width applies");
      Call(editor, "ClosePopovers");
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
    }

    private static void CheckTextEditor(OverlayLayoutEditor editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      TextOverlayElement fps = host.LayoutTargets.OfType<TextOverlayElement>().Single(text => text.Id == "fps");
      Select(editor, IndexOf(editor, fps));
      ElementSettings preferences = Field<ElementSettings>(editor, "_preferences");
      preferences.ReduceUiMotion = false;
      Field<RectTransform>(editor, "_settingsPanel").GetComponentsInChildren<Button>(true).Single(button => button.name == "Edit text").onClick.Invoke();
      OverlayTextEditor text = Field<OverlayTextEditor>(editor, "_textEditor");
      Assert(text.IsOpen && editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "Edit text opens the editor over everything");
      Assert(Field<RectTransform>(editor, "_settingsPanel") == null, "the element toolbar steps aside while editing text");
      GameObject editorRoot = Field<GameObject>(text, "_root");
      Assert(!editorRoot.GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text == "텍스트 편집" || label.text == "Edit text"),
        "the editor has no title card");
      Assert(Field<CanvasGroup>(text, "_fade").alpha < 1, "the editor fades in instead of cutting in");
      SetField(text, "_openedAt", -10f); Call(text, "UpdateFade");
      Assert(Field<CanvasGroup>(text, "_fade").alpha == 1, "the fade finishes");
      preferences.ReduceUiMotion = true;
      RectTransform values = Field<RectTransform>(text, "_valuesArea");
      Button[] chips = values.GetComponentsInChildren<Button>(true);
      Assert(chips.Any(chip => chip.name == "Value progress-start") && chips.Any(chip => chip.name == "Value progress-current"),
        "start and current progress are offered on their own");
      Assert(!chips.Any(chip => chip.name.EndsWith("-label")), "labels are not offered");
      Assert(chips.Length == OverlayValueCatalog.All.Count(value => !OverlayValueCatalog.IsLegacy(value)), "every category is listed at once");
      foreach (TextMeshProUGUI label in values.GetComponentsInChildren<TextMeshProUGUI>(true))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "value chip fits: " + label.text);
      }
      RectTransform chipContent = values.GetComponent<ScrollRect>().content;
      TextMeshProUGUI[] categories = chipContent.GetComponentsInChildren<TextMeshProUGUI>(true)
        .Where(label => label.transform.parent == chipContent).ToArray();
      Assert(categories.Length == OverlayValueCatalog.Categories.Length && categories.All(label => label.rectTransform.anchoredPosition.x == 0),
        "category names line up in one column");
      float column = categories.Max(label => label.rectTransform.sizeDelta.x);
      Assert(chips.All(chip => ((RectTransform)chip.transform).anchoredPosition.x >= column - .5f), "values wrap to the right of the names");
      RectTransform format = Field<RectTransform>(text, "_formatBar");
      Assert(Field<TMP_InputField>(text, "_sizeInput").transform.IsChildOf(format) && Field<Button>(text, "_colorButton").transform.IsChildOf(format)
        && format.GetComponent<SettingsCapsuleGraphic>() != null, "size and color share one capsule");
      float actual = fps.EffectiveScale * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      Assert(Mathf.Abs(Field<float>(text, "_previewScale") - actual) < .001f, "the edited text is shown at its game size");
      OverlayTextDocument document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 7 && document[6].IsValue, "the editor reads FPS | {fps}");
      List<int> lengths = Field<List<int>>(text, "_length");
      Assert(lengths[6] >= 3, "the value shows its live or sample number");
      SaveFrame(camera, texture, canvases, name + "-text");
      SetField(text, "_caretIndex", 7); SetField(text, "_anchor", 7);
      Call(text, "Type", " ms");
      Call(text, "InsertValue", OverlayValueCatalog.Get(OverlayValueId.KeysPerSecond));
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 11 && document[10].Value == "kps", "typing and values insert at the caret");
      SetField(text, "_anchor", 0); SetField(text, "_caretIndex", 3);
      Call(text, "Render");
      string shown = (25 * fps.EffectiveScale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
      Assert(Field<TMP_InputField>(text, "_sizeInput").text == shown, "the size field shows the selection's size as drawn: " + shown);
      Call(text, "CommitSize", "40");
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(Mathf.Abs(document[0].Size * fps.EffectiveScale - 40) < .05f && document[3].Size == 0, "a size applies to the selection only");
      Button color = Field<Button>(text, "_colorButton");
      color.onClick.Invoke();
      SettingsColorPicker picker = Field<SettingsColorPicker>(text, "_picker");
      Field<SettingsSegmentedControl>(picker, "_source").Select(1, true);
      Field<TMP_InputField>(picker, "_hex").text = "#40C0FF";
      picker.Commit();
      SaveFrame(camera, texture, canvases, name + "-text-color");
      picker.Hide();
      Call(text, "DeleteRange", 9, 10);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(document.Count == 10 && document[9].Value == "kps", "deleting next to a value removes one step");
      Call(text, "Undo");
      Assert(Field<OverlayTextDocument>(text, "_document").Count == 11, "the text editor has its own undo");
      text.Close(true, true);
      Assert(!text.IsOpen && fps.Settings.Text.Runs.Any(run => run.Value == "kps") && !fps.Settings.Text.Runs[0].Color.Highlight
        && fps.Settings.Text.Runs[0].Color.Hex == "#40C0FF" && fps.Settings.Text.Runs[0].Size > 0, "Done saves the runs with their colors and sizes");
      Call(editor, "Undo");
      Assert(!fps.Settings.Text.Runs.Any(run => run.Value == "kps"), "a text edit is one layout undo step");

      TextOverlayElement progress = host.LayoutTargets.OfType<TextOverlayElement>().Single(element => element.Id == "progress");
      Select(editor, IndexOf(editor, progress));
      Call(editor, "OpenTextEditor", progress, false);
      document = Field<OverlayTextDocument>(text, "_document");
      Assert(!document[0].IsValue && document[document.Count - 1].Value == "progress", "a converted label opens as plain words");
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
