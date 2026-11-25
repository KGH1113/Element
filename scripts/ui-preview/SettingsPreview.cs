using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.Canvas.Confirm;
using Element.Presentation.OverlayEditor.Canvas.ElementBar;
using Element.Presentation.OverlayEditor.Canvas.Toolbar;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.ShapeEditor;
using Element.Presentation.OverlayEditor.TextEditor;
using Element.Presentation.Settings;
using Element.Presentation.Settings.Window;
using DropState = ElementUI.DropState;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Elements;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Fonts;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Tweaks;
using Element.Domain.Layout;
using Element.Application.Updates;
using Element.Application.Fonts;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Application.Files;
using Element.Application.Profiles;
using Element.Application.Settings;
using Element.Configuration.Profiles;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Element.Infrastructure.Platform;
using Element.Infrastructure.Storage;
using Object = UnityEngine.Object;
using Element.Configuration.Gameplay;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Widgets;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Shape;
using Element.Presentation.Overlay.Text;
using ElementUI.Ugui;

namespace Element.Editor
{
  public static partial class SettingsPreview
  {
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int _checks;
    private static string _output;
    private static string _fonts;
    private const string GeorgiaFile = "/System/Library/Fonts/Supplemental/Georgia.ttf",
      TypewriterFile = "/System/Library/Fonts/Supplemental/AmericanTypewriter.ttc",
      VerdanaFile = "/System/Library/Fonts/Supplemental/Verdana.ttf";
    public static void RenderAll()
    {
      _output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../build/ui-preview"));
      Directory.CreateDirectory(_output);
      // A failed run must not leave a previous run's success marker behind.
      File.Delete(Path.Combine(_output, "checks.txt"));
      _fonts = Path.Combine(_output, "Fonts");
      if (Directory.Exists(_fonts))
        Directory.Delete(_fonts, true);
      Directory.CreateDirectory(_fonts);
      foreach (string font in new[] { GeorgiaFile, TypewriterFile })
        File.Copy(font, Path.Combine(_fonts, Path.GetFileName(font)));
      CheckMigration();
      CheckSpacingGuides();
      _checks += PlanetPreviewChecks.Run(_output);
      _checks += OverlayTargetChecks.Run(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset"),
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset").material);
      _checks += NativeLayoutChecks.Run(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset"));
      Render(1280, 720, SystemLanguage.Korean, "_home", "home-ko-720");
      Render(1920, 1080, SystemLanguage.Korean, "_home", "home-ko-1080");
      Render(760, 720, SystemLanguage.English, "_home", "home-en-narrow");
      Render(540, 720, SystemLanguage.English, "_home", "home-en-compact");
      Render(1280, 720, SystemLanguage.English, "_tweaks", "tweaks-en-720");
      Render(1280, 720, SystemLanguage.Korean, "_general", "general-ko-720");
      Render(760, 720, SystemLanguage.English, "_general", "general-en-narrow");
      Render(360, 720, SystemLanguage.English, "_general", "general-en-compact");
      Render(1280, 720, SystemLanguage.English, "_effects", "effects-en-720");
      Render(760, 720, SystemLanguage.Korean, "_effects", "effects-ko-narrow");
      Render(1280, 720, SystemLanguage.English, "_editorTweaks", "editor-tweaks-en-720");
      Render(1280, 720, SystemLanguage.English, "_judgment", "judgment-en-720");
      Render(1280, 720, SystemLanguage.Korean, "_planets", "planets-ko-720");
      Render(760, 720, SystemLanguage.English, "_planets", "planets-en-narrow");
      Render(1280, 720, SystemLanguage.English, "_presetEditor", "preset-editor-en-720");
      Render(760, 720, SystemLanguage.Korean, "_presetEditor", "preset-editor-ko-narrow");
      Render(1280, 720, SystemLanguage.English, "_presetEditor", "preset-shortcut-en-720");
      Render(760, 720, SystemLanguage.Korean, "_presetEditor", "preset-shortcut-ko-narrow");
      Render(360, 720, SystemLanguage.English, "_presetEditor", "preset-shortcut-en-compact");
      Render(1280, 720, SystemLanguage.English, "_editorTweaks", "editor-editing-en-720");
      Render(1280, 720, SystemLanguage.Korean, "_chatter", "chatter-blocker-ko-720");
      Render(360, 720, SystemLanguage.English, "_chatter", "chatter-blocker-en-compact");
      Render(1280, 720, SystemLanguage.Korean, "_profiles", "profiles-ko-720");
      Render(760, 720, SystemLanguage.English, "_profiles", "profiles-en-narrow");
      Render(360, 720, SystemLanguage.Korean, "_profiles", "profiles-ko-compact");
      Render(1280, 720, SystemLanguage.English, "_profiles", "profiles-en-rename");
      Render(1280, 720, SystemLanguage.Korean, "_profiles", "profiles-ko-fonts");
      Render(360, 720, SystemLanguage.English, "_profiles", "profiles-en-compact-fonts");
      Render(1280, 720, SystemLanguage.English, "_profiles", "profiles-en-new");
      Render(760, 720, SystemLanguage.Korean, "_profiles", "profiles-ko-narrow-new");
      Render(360, 720, SystemLanguage.English, "_profiles", "profiles-en-compact-new");
      Render(2880, 1800, SystemLanguage.Korean, "_home", "home-ko-retina");
      Debug.Log("UI_PREVIEW_SCREEN " + Screen.width + "x" + Screen.height);
      RenderLayout(Screen.width, Screen.height, SystemLanguage.Korean, "layout-ko");
      RenderLayout(Screen.width, Screen.height, SystemLanguage.English, "layout-en");
      RenderLayout(760, 720, SystemLanguage.English, "layout-en-narrow");
      RenderValueDocks();
      CheckColorPicker();
      RenderInputCarets();
      RenderNotice(1280, 720, SystemLanguage.Korean, "notice-ko-720");
      RenderNotice(360, 720, SystemLanguage.English, "notice-en-compact");
      UiTidyAudit.Write(Path.Combine(_output, "ui-audit.txt"));
      string[] untidy = UiTidyAudit.Broken.ToArray();
      Assert(untidy.Length == 0, "every screen is tidy (ui-audit.txt): " + string.Join(" | ", untidy.Take(6)));
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
      SettingsMigrator.Migrate(settings);
      UiTheme.SetHighlight(settings.HighlightColor);
      var host = new OverlayCanvasHost();
      var catalog = new OverlayFontCatalog(_fonts, new PreviewFontPicker(VerdanaFile), Debug.LogWarning,
        (action, exception) => Debug.LogException(exception));
      host.SetFonts(new OverlayFonts(font, font.material, catalog, catalog));
      host.ConfigureLayouts(settings.OverlayLayouts);
      var viewers = new PreviewKeyViewers(screenWidth);
      var editor = new OverlayEditorScreen(host, viewers, font, _ => {}, settings);
      var eventsRoot = new GameObject("Fixture events", typeof(EventSystem));
      // Batch edit mode does not run EventSystem's play-mode lifecycle.
      Call(eventsRoot.GetComponent<EventSystem>(), "OnEnable");
      editor.Open();
      GameObject root = Session(editor).Root; root.SetActive(true);
      var cameraRoot = new GameObject("Layout preview camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .05f, .07f);
      var texture = new RenderTexture(screenWidth, screenHeight, 24); camera.targetTexture = texture;
      var canvases = new[] { host.Root.GetComponent<Canvas>(), root.GetComponent<Canvas>() };
      _refresh = () =>
      {
        Call(editor, "UpdateFrames");
        Settle();
      };
      editor.Resize(width, height); Canvas.ForceUpdateCanvases();
      Rect toolbar = editor.ToolbarBounds;
      Assert(toolbar.xMin >= 0 && toolbar.xMax <= width && toolbar.yMax <= height, "layout toolbar fits viewport");
      Assert(toolbar.width <= 760.1f && toolbar.height <= 120.1f, "overlay toolbar stays compact with element actions");
      RectTransform toolbarRect = Toolbar(editor).Root;
      Call(editor, "UpdateFrames");
      Assert(ElementBar(editor).Root == null, "no element toolbar without a selection");
      int fpsIndex = IndexOf(editor, host.LayoutTargets.Single(target => target.Id == "fps"));
      Select(editor, fpsIndex);
      Assert(ElementBar(editor).Root == null, "texts have no element toolbar");
      Assert(!toolbarRect.GetComponentsInChildren<Pressable>(true).Any(button => button.name.Contains("settings")),
        "the toolbar has no settings button");
      Pressable editContent = ToolbarButton(editor, "Edit element");
      Assert(ToolbarModel(editor).SizedInEditor.Value && Shown(editContent) && !Shown(ScaleField(editor))
        && !Shown(ToolbarButton(editor, "Increase size")) && !Shown(ToolbarButton(editor, "Decrease size")),
        "a text shows Edit text where the scale was");
      TextMeshProUGUI editLabel = editContent.GetComponentInChildren<TextMeshProUGUI>();
      editLabel.ForceMeshUpdate();
      Assert(editLabel.text == L("텍스트 편집", "Edit text") && !editLabel.isTextTruncated, "Edit text fits: " + editLabel.text);
      Rect fpsRect = host.LayoutTargets.Single(target => target.Id == "fps").Bounds;
      object[] hit = { fpsRect.max, null };
      int hitIndex = (int)typeof(CanvasDragInput).GetMethod("FindTarget").Invoke(Drag(editor), hit);
      Assert(hitIndex == fpsIndex && (LayoutHandle)hit[1] == LayoutHandle.Move, "a text's corner moves it instead of scaling it");
      // JRP's progress bar: a black bar centered under the top, the light
      // track 2 units inside it, the fill over the track as wide as the
      // progress, drawn in that order and clear of the title.
      ShapeOverlayElement Built(string id) => host.LayoutTargets.OfType<ShapeOverlayElement>().Single(shape => shape.Id == id);
      ShapeOverlayElement edge = Built(OverlayElementDefaults.ProgressBarId), track = Built(OverlayElementDefaults.ProgressTrackId),
        fill = Built(OverlayElementDefaults.ProgressFillId);
      Rect edgeRect = edge.Bounds, trackRect = track.Bounds, fillRect = fill.Bounds;
      float unit = edgeRect.width / 640f;
      Assert(edge.Enabled && track.Enabled && fill.Enabled && Mathf.Abs(edgeRect.center.x - Screen.width * .5f) < 1
        && Mathf.Abs(edgeRect.y - 11 * unit) < 1 && Mathf.Abs(edgeRect.height - 16 * unit) < 1, "the progress bar sits centered under the top: " + edgeRect);
      Assert(Mathf.Abs(trackRect.x - (edgeRect.x + 2 * unit)) < 1 && Mathf.Abs(trackRect.y - (edgeRect.y + 2 * unit)) < 1
        && Mathf.Abs(trackRect.width - 636 * unit) < 1 && Mathf.Abs(trackRect.height - 12 * unit) < 1, "the track sits 2 units inside the bar: " + trackRect);
      Assert(Mathf.Abs(fillRect.x - trackRect.x) < 1 && Mathf.Abs(fillRect.y - trackRect.y) < 1
        && Mathf.Abs(fillRect.width - 636 * .48f * unit) < 1.5f && Mathf.Abs(fillRect.height - 12 * unit) < 1,
        "the fill starts at the track's left edge and follows the progress: " + fillRect);
      Func<ShapeOverlayElement, RectTransform> rectOf = shape => (RectTransform)typeof(OverlayElementTarget).GetProperty("Rect", Private).GetValue(shape);
      Assert(rectOf(edge).GetSiblingIndex() < rectOf(track).GetSiblingIndex() && rectOf(track).GetSiblingIndex() < rectOf(fill).GetSiblingIndex()
        && edge.Label == (Korean(name) ? "진행도 바" : "Progress bar") && track.Label == (Korean(name) ? "진행도 바 배경" : "Progress bar track"),
        "the bar, track and fill draw in order, and the editor names them");
      Rect titleRect = host.LayoutTargets.Single(target => target.Id == "song-title").SelectionBounds;
      Assert(titleRect.yMin >= edgeRect.yMax, "the song title sits under the bar: " + titleRect + " / " + edgeRect);
      var cutouts = editor.NativeExclusions;
      float toolbarScale = Session(editor).ToolbarScale;
      Assert(cutouts.Count == 1 && cutouts.All(cutout => Mathf.Abs(cutout.Radius - 8 * toolbarScale) < .01f),
        "native viewers get the toolbar as a rounded cutout: " + cutouts.Count);
      Assert(root.GetComponentsInChildren<CanvasOutlineGraphic>(false).Length == 1, "only the selected element has an outline");
      Assert(!Field<bool>(root.GetComponentsInChildren<CanvasOutlineGraphic>(false).Single(), "_handles"),
        "a text's outline has no resize handles");
      // Without a selection the scale shows again; key viewers use it.
      Select(editor, -1);
      Assert(Shown(ScaleField(editor)) && !Shown(ToolbarButton(editor, "Edit element")),
        "the scale returns when no text or shape is selected");
      TextMeshProUGUI title = ByName<TextMeshProUGUI>(toolbarRect, "Selection");
      Assert(title.text == L("요소 선택", "Select overlay"), "with nothing selected the toolbar asks for a selection: " + title.text);
      RectTransform grip = ByName<RectTransform>(toolbarRect, "Drag panel");
      Assert(Mathf.Abs(CenterIn(grip, toolbarRect).y - CenterIn(title.rectTransform, toolbarRect).y) < .5f,
        "move grip and title share the same vertical center");
      Pressable plus = ToolbarButton(editor, "Increase size"), minus = ToolbarButton(editor, "Decrease size");
      TMP_InputField sizeInput = ScaleField(editor);
      Assert(!plus.IsInteractable() && !minus.IsInteractable() && !sizeInput.interactable && sizeInput.text == "100",
        "without a selection the scale shows 100% and can't be changed");
      Assert(Mathf.Abs(CenterIn((RectTransform)plus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f
        && Mathf.Abs(CenterIn((RectTransform)minus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f,
        "plus, minus and percentage field share one vertical center");
      CheckInputCaret(sizeInput, "overlay scale", "", "100", "99.9");
      foreach (UiIcon icon in root.GetComponentsInChildren<UiIcon>(true))
        using (var mesh = new VertexHelper())
        {
          typeof(UiIcon).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(icon, new object[] { mesh });
          Assert(mesh.currentVertCount > 0, "vector icon renders: " + icon.Kind);
        }
      foreach (TextMeshProUGUI label in toolbarRect.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(label.font == font && !label.isTextTruncated, "toolbar label fits: " + label.text);
      }
      Assert(ToolbarButton(editor, "Save").GetComponent<RoundedRect>().color.maxColorComponent < .5f, "save uses a dark tinted fill");
      if (width != screenWidth || height != screenHeight)
      {
        Cleanup(editor, root, eventsRoot, cameraRoot, texture, host);
        return;
      }

      // Everything below edits real elements on the fixture's screen.
      // The scale itself, as key viewers use it, measured on a real element.
      IOverlayLayoutTarget fps = host.LayoutTargets.Single(target => target.Id == "fps");
      Select(editor, fpsIndex);
      ToolbarViewModel bar = ToolbarModel(editor);
      // Typing writes the field's value; it is applied when typing ends, and
      // one out of range keeps Save from closing the editor.
      bar.Scale.Value = "130";
      Assert(bar.FinishTyping.TryExecute() && Mathf.Abs(fps.EffectiveScale - 1.3f) < .01f, "letting go of the field applies the typed scale");
      Call(editor, "Undo");
      bar.Scale.Value = "5";
      Assert(!Toolbar(editor).CommitPendingScale() && editor.IsOpen && Status(editor).Showing
        && Status(editor).Text.Value.Contains("20"), "Save waits while the typed scale is out of range: " + Status(editor).Text.Value);
      bar.DropTypedScale();
      Assert(Toolbar(editor).CommitPendingScale() && Mathf.Abs(fps.EffectiveScale - 1) < .01f, "a dropped typed scale no longer stops Save");
      Assert(bar.CommitScale("150") && Mathf.Abs(fps.EffectiveScale - 1.5f) < .01f, "precise layout scale commits");
      Call(editor, "Undo"); Assert(Mathf.Abs(fps.EffectiveScale - 1) < .01f, "layout undo restores the scale");
      Call(editor, "Redo"); Assert(Mathf.Abs(fps.EffectiveScale - 1.5f) < .01f, "layout redo restores the edit");
      CheckToolbarChrome(editor);
      Assert(!bar.CommitScale("1999"), "a scale past the limit is refused");
      Toolbar(editor).Refresh();
      TextMeshProUGUI status = StatusLabel(editor);
      Assert(Status(editor).Text.Value.Contains("1000") && status.text.Contains("1000") && status.color == UiPalette.Error,
        "scale validation names the 1000% limit, warm: " + status.text);
      SaveFrame(camera, texture, canvases, name);

      // Add Element: three kinds; key viewers explain why they're unavailable.
      viewers.Availability = KeyViewerAvailability.AppClosed;
      Call(editor, "ToggleAddMenu");
      RectTransform menu = AddMenu(editor);
      Settle();
      Pressable[] kinds = Rows(menu);
      Assert(kinds.Select(row => row.name).SequenceEqual(new[] { "Add Text", "Add KeyViewer", "Add Shape" }),
        "Add Element offers Text, Key viewer and Shape");
      Assert(kinds[0].IsInteractable() && !kinds[1].IsInteractable() && kinds[2].IsInteractable(), "a closed ElementNote disables only key viewers");
      Assert(kinds[1].GetComponentsInChildren<TextMeshProUGUI>(true).Any(label => label.text.Contains("ElementNote")
        && label.color == UiPalette.Error), "the disabled key viewer says how to enable it, warm");
      foreach (TextMeshProUGUI label in menu.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(label.font == font && !label.isTextTruncated, "add menu label fits: " + label.text);
      }
      Assert(PopoverInside(editor, menu, screenWidth, screenHeight), "the add menu stays on screen");
      Rect menuRect = PopoverPlacement.ScreenRect(menu), addRect0 = ScreenRectOf((RectTransform)ToolbarButton(editor, "Add element").transform);
      Assert(menuRect.yMax <= editor.ToolbarBounds.y + .5f
        && Mathf.Abs(menuRect.x - Mathf.Min(addRect0.x, screenWidth - PopoverPlacement.Margin - menuRect.width)) < 1,
        "the add menu opens above the toolbar from Add Element, as far as the view lets it: " + menuRect + " / " + addRect0);
      Assert(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == kinds[0].gameObject,
        "the first row has the keyboard");
      SaveFrame(camera, texture, canvases, name + "-add");
      Call(editor, "ToggleAddMenu");
      Assert(AddMenu(editor) == null, "Add Element closes its menu again");

      CheckShapes(editor, host, camera, texture, canvases, name, screenWidth, screenHeight);
      CheckKeyViewers(editor, viewers, camera, texture, canvases, name);
      CheckTextEditor(editor, host, camera, texture, canvases, name);
      CheckMultiSelect(editor, host, settings, camera, texture, canvases, name);
      CheckSettingsKeepSpaces();
      CheckHighlightColor(editor, settings, camera, texture, canvases, name);
      CheckFonts(editor, host, settings, camera, texture, canvases, name);
      CheckGradients(editor, host, camera, texture, canvases, name);
      CheckIf(editor, host, camera, texture, canvases, name);
      CheckModElements(editor, host, camera, texture, canvases, name, screenWidth, screenHeight);

      var beforeReset = new LayoutSnapshot(host.LayoutSettings);
      List<OverlayLayoutSettings> beforeCopies = host.LayoutSettings.Select(layout => layout.Clone()).ToList();
      ConfirmPanel modal = Field<ConfirmPanel>(editor, "_modal");
      Press(ToolbarButton(editor, "Reset all"));
      Assert(modal.IsOpen && editor.NativeExclusionBounds == new Rect(0, 0, screenWidth, screenHeight),
        "Reset all asks first, covering native viewers");
      Settle();
      Assert(modal.Dialog.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == L("오버레이를 처음 상태로 되돌릴까요?", "Reset all overlays?"))
        && ByName<Pressable>(modal.Dialog, "Confirm").GetComponentInChildren<TextMeshProUGUI>().text == L("전체 초기화", "Reset all"),
        "the dialog says what Reset all does, with Cancel and Reset all");
      foreach (TextMeshProUGUI label in modal.Dialog.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "dialog label fits: " + label.text);
      }
      Assert(PopoverInside(editor, modal.Dialog, screenWidth, screenHeight), "the dialog fits the view");
      Assert(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == ByName<Pressable>(modal.Dialog, "Cancel").gameObject,
        "Cancel has the keyboard, so Enter keeps everything");
      SaveFrame(camera, texture, canvases, name + "-reset");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "opening the confirmation changes nothing");
      Press(ByName<Pressable>(modal.Dialog, "Cancel"));
      Assert(!modal.IsOpen && beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "Cancel closes the dialog and changes nothing");
      Field<CanvasReset>(editor, "_reset").Confirm();
      Press(ByName<Pressable>(modal.Dialog, "Confirm"));
      Assert(!modal.IsOpen, "Reset all closes the dialog");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled || OverlayElementDefaults.TryGet(shape.Id, out _))
        && host.LayoutTargets.Single(target => target.Id == "fps").Enabled, "Reset all removes added elements and restores built-ins");
      Call(editor, "Undo");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "Reset all is one undo step: "
        + string.Join(", ", host.LayoutSettings.Where(layout => !(beforeCopies.Find(copy => copy.Id == layout.Id)?.SameAs(layout) ?? !layout.Enabled))
          .Select(layout => layout.Id + (layout.Enabled ? "" : " (hidden)"))));
      editor.Close(false);
      Assert(!editor.IsOpen && settings.HighlightColor == "#BA712B", "cancel restores the opening highlight and exits");
      Assert(settings.OverlayFont == "" && host.Fonts.DefaultKey == "", "cancel restores the opening default font");
      Assert(host.LayoutTargets.OfType<ShapeOverlayElement>().All(shape => !shape.Enabled || OverlayElementDefaults.TryGet(shape.Id, out _)), "cancel discards added shapes");
      Cleanup(editor, root, eventsRoot, cameraRoot, texture, host);
    }

    private static void CheckShapes(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name, int width, int height)
    {
      // Add → Shape asks for the kind beside the add menu.
      Call(editor, "ToggleAddMenu");
      Press(Rows(AddMenu(editor)).Single(button => button.name == "Add Shape"));
      RectTransform kindMenu = Field<ListMenu>(Kind<ShapeKind>(editor), "_menu").Root;
      Assert(kindMenu != null && AddMenu(editor) != null, "Shape opens a kind menu next to the add menu");
      string[] kindNames = Rows(kindMenu).Select(button => button.name).ToArray();
      Assert(kindNames.SequenceEqual(new[] { "Add Square", "Add Triangle", "Add Circle" }), "the kinds are square, triangle and circle");
      Rect addRect = PopoverPlacement.ScreenRect(AddMenu(editor));
      Rect kindRect = PopoverPlacement.ScreenRect(kindMenu);
      Assert(PopoverInside(editor, kindMenu, width, height) && (kindRect.x >= addRect.xMax - .5f || kindRect.xMax <= addRect.x + .5f),
        "the kind menu sits beside the add menu, on screen");
      Assert(!kindRect.Overlaps(editor.ToolbarBounds), "the kind menu leaves the toolbar uncovered");
      foreach (TextMeshProUGUI label in kindMenu.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "shape kind label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-kinds");
      Press(Rows(kindMenu).Single(button => button.name == "Add Square"));
      Assert(AddMenu(editor) == null && Field<ListMenu>(Kind<ShapeKind>(editor), "_menu").Root == null, "adding closes the menu and its list");
      ShapeOverlayElement shape = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      ShapeEditorScreen shapes = (ShapeEditorScreen)Kind<ShapeKind>(editor).Editor;
      Assert(shape.Enabled && shape.Shape.Kind == OverlayShapeKind.Square && shapes.IsOpen, "a new square opens its editor at once");
      Assert(editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "the shape editor covers native viewers");
      float scale = shape.EffectiveScale;
      DockProbe dock = DockOf(shapes);
      Assert(dock.Root.gameObject.activeInHierarchy && dock.Owner == null && dock.HintText.Length > 0
        && DockKeys(dock).Length == 18 && DockKeys(dock).All(key => !key.IsInteractable()),
        "the input dock is always there, resting with a hint until a number is clicked");
      Press(DockChip(dock, "Value combo"));
      Assert(Field<EditorFramePanel>(shapes, "_frame").ViewModel.NoticeText.Length > 0, "a value with no number picked says to pick one first");
      FormulaFieldView widthField = Measures(shapes).WidthField, heightField = Measures(shapes).HeightField;
      Assert(widthField.Text == "160" && heightField.Text == "160", "W and H show the size in pixels");
      Rect restingDock = ScreenRectOf(dock.Root);
      Assert(InsideScreen(restingDock) && restingDock.yMax < ScreenRectOf(widthField.Root).yMin, "the shape and its numbers stay above the dock");
      Rect shapeRect = ScreenRectOf(ShapeRect(shapes));
      Rect wRect = ScreenRectOf(widthField.Root), hRect = ScreenRectOf(heightField.Root);
      Assert(wRect.yMax < shapeRect.yMin && Mathf.Abs(wRect.center.x - shapeRect.center.x) < 1, "W sits under the shape");
      Assert(hRect.xMin > shapeRect.xMax && Mathf.Abs(hRect.center.y - shapeRect.center.y) < 1, "H sits to its right");
      Assert(new[] { typeof(ShapeEditorScreen), typeof(ShapeMeasures) }.All(type => type.GetField("_widthLine", Private) == null), "no dimension lines");
      List<Button> arcs = Field<List<Button>>(Measures(shapes), "_arcs");
      Assert(arcs.Count == 4 && arcs.All(arc => arc.gameObject.activeSelf), "a square has an arc at each corner");
      Assert(Field<List<FormulaFieldView>>(Measures(shapes), "_radiusFields").All(field => !field.Root.gameObject.activeSelf),
        "rounding values stay hidden until an arc is clicked");
      foreach (Button arc in arcs)
      {
        Rect arcRect = ScreenRectOf((RectTransform)arc.transform);
        Vector2 corner = arcRect.center;
        Assert(Mathf.Abs(corner.x - shapeRect.xMin) < 1 || Mathf.Abs(corner.x - shapeRect.xMax) < 1, "each arc is centered on a corner");
        using (var mesh = new VertexHelper())
        {
          typeof(ShapeCornerArcGraphic).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(arc.GetComponent<ShapeCornerArcGraphic>(), new object[] { mesh });
          List<UIVertex> vertices = new List<UIVertex>();
          mesh.GetUIVertexStream(vertices);
          Vector3 middle = vertices.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex.position) / vertices.Count;
          RectTransform arcTransform = (RectTransform)arc.transform;
          Vector2 outward = (Vector2)arcTransform.TransformPoint(middle) - (Vector2)arcTransform.TransformPoint(arcTransform.rect.center);
          Vector2 away = corner - shapeRect.center;
          Assert(Vector2.Dot(outward, away) > 0, "an arc faces away from the shape");
        }
      }
      PropertyButtonViewModel fillButton = ShapeColorsOf(shapes).FillButton, strokeButton = ShapeColorsOf(shapes).StrokeButton,
        strokeWidth = ShapeNumbersOf(shapes).StrokeWidth, shapeGrow = Grow(shapes).Button;
      CheckEditorTopLine(shapes, "Shape editor", L("도형 편집", "Edit shape"));
      Assert(shapeGrow.Value.Contains(Korean(name) ? "자동" : "Auto"), "the grow button says where the shape grows from");
      Assert(new[] { typeof(ShapeEditorScreen), typeof(ShapeColors) }.All(type => type.GetField("_fillOpacity", Private) == null && type.GetField("_strokeOpacity", Private) == null),
        "opacity lives in the color picker, not the bar");
      PropertyButtonViewModel[] shapeBar = { fillButton, strokeButton, strokeWidth, shapeGrow };
      string[] shapeLabels = Korean(name) ? new[] { "채우기", "테두리", "테두리 두께", "기준점" } : new[] { "Fill", "Border", "Border width", "Grow from" };
      for (int i = 0; i < shapeBar.Length; i++)
      {
        Pressable drawn = BarButton(shapes, shapeBar[i]);
        Assert(drawn.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == shapeLabels[i]),
          "each row says what it is: " + shapeLabels[i]);
        Assert(drawn.targetGraphic.transform == drawn.transform, "a row lights up where it is drawn");
        if (i > 0)
          Assert(BarButtonRect(shapes, shapeBar[i]).yMax <= BarButtonRect(shapes, shapeBar[i - 1]).yMin + .5f, "rows stack one under another");
      }
      Assert(strokeWidth.Value.Length > 0 && !strokeWidth.IsComputed
        && BarButton(shapes, strokeWidth).GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == strokeWidth.Value),
        "the border width shows its number");
      foreach (FormulaFieldView measure in new[] { widthField, heightField })
        Assert(measure.Root.GetComponent<Button>().targetGraphic.transform == measure.Root,
          "W and H light up exactly where the pill is drawn: " + measure.Name);
      Assert(!EditorBarRoot(shapes).GetComponentsInChildren<Pressable>(true).Any(button => button.name.EndsWith("smaller") || button.name.EndsWith("larger")),
        "numbers have no − and + buttons");
      RectTransform bar = EditorBarRoot(shapes);
      foreach (TextMeshProUGUI label in bar.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "shape bar label fits: " + label.text);
      }
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-editor");

      // Clicking W opens the input dock; the shape moves up to stay in sight.
      Click(widthField.Root, "Width");
      FormulaEditing editing = ShapeNumbersOf(shapes).Editing;
      Assert(dock.Owner == "width" && editing.Active && editing.Working.Serialize() == "160",
        "W opens the input dock with its number");
      Rect dockRect = ScreenRectOf(dock.Root);
      shapeRect = ScreenRectOf(ShapeRect(shapes));
      Assert(InsideScreen(dockRect) && dockRect.yMax < shapeRect.yMin && dockRect.yMax < ScreenRectOf(widthField.Root).yMin,
        "the dock sits below the shape and its numbers, never over them");
      Assert(dockRect.width > Screen.width * .7f, "the dock spans the screen");
      Assert(DockChips(dock).Length == dock.Values.OpenFolder.Chips.Count,
        "the dock lays out the open place's values");
      Pressable shortChip = DockChips(dock).FirstOrDefault(chip => Mathf.Abs(((RectTransform)chip.transform).rect.height - ValueBrowserView.ChipHeight) > .5f);
      Assert(shortChip == null, "every value chip, its capsule and its press are as tall as a chip: "
        + (shortChip == null ? "" : shortChip.name + " " + ((RectTransform)shortChip.transform).rect.height));
      Assert(DockKeys(dock).Any(key => key.name == "Key clear") && DockKeys(dock).Any(key => key.name == "Key done"), "the keypad has AC and Done");
      Assert(DockKeys(dock).All(key => key.IsInteractable()), "with a number open every key can be pressed");
      foreach (TextMeshProUGUI label in dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        if (label.GetComponentInParent<ScrollRect>() != null || label.name == "Formula" || label.richText)
          continue;
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "dock label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-dock");
      foreach (char key in "\b\b\b240")
        ShapeNumbersOf(shapes).KeypadPress(key);
      OverlayShapeSettings working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(Mathf.Abs(working.Width * scale - 240) < .01f && widthField.Text == "240", "keypad digits set the width as they are typed");
      ShapeNumbersOf(shapes).KeypadPress('0');
      ShapeNumbersOf(shapes).KeypadPress('0');
      Press(DockKey(dock, "Key done"));
      Assert(dock.Owner == null && Mathf.Abs(working.Width * scale - 10000) < .01f
        && Field<EditorFramePanel>(shapes, "_frame").ViewModel.NoticeText.Contains("10000"),
        "Done lets the dock rest; a width past the limit is kept in range and says so");
      ShapeHistory(shapes).Undo();
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(Mathf.Abs(working.Width * scale - 160) < .01f, "keypad typing undoes as one step");

      // A value picked in the dock: the formula in the dock, the result on W.
      Click(widthField.Root, "Width");
      Press(DockChip(dock, "Value progress-current"));
      ShapeNumbersOf(shapes).KeypadPress('*');
      ShapeNumbersOf(shapes).KeypadPress('3');
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.WidthFormula == "{progress-current}*3", "the keypad calculates with the value: " + working.WidthFormula);
      Assert(dock.FormulaText.Contains("×") && dock.ResultText.Contains("144"), "the dock shows the formula and its result: " + dock.FormulaText + " / " + dock.ResultText);
      Assert(widthField.Text == "144" && widthField.Computed, "W shows the result, marked as calculated");
      // A level playing behind the editor (autoplay) moves the numbers live.
      host.Values.Set(OverlayValueId.ProgressCurrent, new System.Text.StringBuilder("60"), 60);
      Call(shapes, "FollowLiveValues");
      Assert(widthField.Text == "180" && dock.ResultText.Contains("180"), "a live value moves W and the dock's result: " + widthField.Text);
      host.Values.Clear(OverlayValueId.ProgressCurrent);
      Call(shapes, "FollowLiveValues");
      Assert(widthField.Text == "144", "without live data the example comes back");
      Press(DockKey(dock, "Key clear"));
      Assert(editing.Working.IsEmpty && working.WidthFormula == "{progress-current}*3", "AC starts over without losing the width yet");
      Assert(DockKey(dock, "Key 7").IsInteractable() && DockKey(dock, "Key −").IsInteractable() && !DockKey(dock, "Key +").IsInteractable()
        && !DockKey(dock, "Key ×").IsInteractable() && !DockKey(dock, "Key clear").IsInteractable() && DockKey(dock, "Key done").IsInteractable(),
        "after AC only what can start a number can be pressed");
      Color plusInk = DockKey(dock, "Key +").GetComponentInChildren<TextMeshProUGUI>().color, minusInk = DockKey(dock, "Key −").GetComponentInChildren<TextMeshProUGUI>().color;
      Assert(plusInk.a < minusInk.a * .5f, "a key that would do nothing reads dimmed: " + plusInk + " / " + minusInk);
      ShapeNumbersOf(shapes).KeypadPress('9');
      ShapeNumbersOf(shapes).KeypadPress('0');
      Assert(working.WidthFormula == "" && Mathf.Abs(working.Width * scale - 90) < .01f, "typing after AC replaces the formula");
      ShapeHistory(shapes).Undo();
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.WidthFormula == "{progress-current}*3", "undo brings the formula back");
      Click(heightField.Root, "Height");
      Assert(dock.Owner == "height", "another number switches the dock to it");
      SaveFrame(camera, texture, canvases, name + "-shape-values");
      Call(shapes, "CloseOpen");

      // Dragging still works: words are refused, numbers land on the number.
      Vector2 heightPoint = ScreenCenter(heightField.Root);
      new Carry(dock, OverlayValueId.SongTitle).LetGoAt(heightPoint);
      Assert(working.HeightFormula == "" && Field<EditorFramePanel>(shapes, "_frame").ViewModel.NoticeText.Length > 0,
        "the song title is refused by numbers, with a reason");
      Vector2 strokePoint = BarButtonRect(shapes, strokeWidth).center;
      Carry toStroke = new Carry(dock, OverlayValueId.Accuracy).MoveTo(strokePoint);
      Assert(strokeWidth.Drop == DropState.Accepts && !strokeWidth.Fill.Value.Equals(fillButton.Fill.Value), "the border width lights up for a number dragged over it");
      Assert(strokeWidth.Fill.Value.Literal.a == 1 && fillButton.Fill.Value.Literal.a == 1,
        "property rows stay opaque, lit or not, so the overlay never shows through them");
      toStroke.LetGoAt(strokePoint);
      Assert(working.StrokeWidthFormula == "accuracy" && dock.Owner == "stroke" && strokeWidth.Active && strokeWidth.Drop == DropState.None,
        "a value dropped on the border width opens it in the dock");
      Assert(strokeWidth.IsComputed && strokeWidth.Value == "99.5" && strokeWidth.ValueInk.Value.Equals((ElementUI.UiColor)UiPalette.SoftAccent),
        "the border width shows the result, marked as calculated: " + strokeWidth.Value);
      ShapeHistory(shapes).Undo();
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Call(shapes, "CloseOpen");

      // A corner's arc opens its rounding, shown beside the arc.
      arcs[2].onClick.Invoke();
      FormulaFieldView radius = Field<List<FormulaFieldView>>(Measures(shapes), "_radiusFields")[2];
      Assert(dock.Owner == "radius:2" && radius.Root.gameObject.activeSelf, "an arc opens its corner's rounding with its value");
      foreach (char key in "\b\b\b20")
        ShapeNumbersOf(shapes).KeypadPress(key);
      Assert(radius.Text == "20", "the rounding value follows the keypad");
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-corner");
      Call(shapes, "CloseOpen");
      Call(shapes, "Render");
      Assert(Mathf.Abs(working.Points[2].Radius * scale - 20) < .01f && working.Points[0].Radius == 0 && !radius.Root.gameObject.activeSelf,
        "one corner rounds; its value hides again");

      // Colors open right above the keypad: the keypad below types the
      // opacity and the values beside it can be dropped on it.
      PressBar(shapes, fillButton);
      UiColorPicker picker = ShapeColorsOf(shapes).Picker;
      Rect pickerRect = ScreenRectOf(picker.Root), dockArea = ScreenRectOf(dock.Root);
      Assert(picker.Visible && fillButton.Active && !strokeButton.Active, "Fill opens its picker and stays lit");
      Rect keypadRect = KeypadRect(dock);
      // Following the highlight it is one column, right over the keypad.
      Assert(picker.Column && Shown(DockKey(dock, "Key done")) && Mathf.Abs(pickerRect.xMin - keypadRect.xMin) < 1
        && Mathf.Abs(pickerRect.xMax - keypadRect.xMax) < 1 && pickerRect.yMin >= dockArea.yMax - 1
        && InsideScreen(pickerRect), "the picker opens above the keypad, ending with it: " + pickerRect + " / " + keypadRect);
      AssertInColumn(shapes, picker.Root, L("채우기 색", "Fill color"));
      Assert(!pickerRect.Overlaps(ScreenRectOf(ShapeRect(shapes))) && dock.HintText.Contains(Korean(name) ? "채우기 색" : "fill color"),
        "the shape stays in sight, and the dock's line says which color is picked: " + dock.HintText);
      OpacityFieldView opacity = ShapeColorsOf(shapes).Opacity;
      Assert(picker.ExternalOpacity && picker.OpacitySlot.gameObject.activeInHierarchy && opacity.Field.Root.IsChildOf(picker.OpacitySlot)
        && !ColorPickerProbe.Shown(ColorPickerProbe.Opacity(picker.Root)) && opacity.Field.Text == "100",
        "the picker carries the opacity as a number like W and H");
      Assert(picker.Model.FollowsHighlight && !ColorPickerProbe.Shown(ColorPickerProbe.Plane(picker.Root))
        && ColorPickerProbe.Shown(ColorPickerProbe.Alpha(picker.Root)) && ColorPickerProbe.Shown(ColorPickerProbe.Segment(picker.Root, 0)),
        "a new shape follows the highlight: only its opacity shows, with Highlight/Custom");
      SaveFrame(camera, texture, canvases, name + "-shape-color");
      Press(ColorPickerProbe.Segment(picker.Root, 1));
      Settle();
      Call(shapes, "Render");
      Settle();
      Assert(!picker.Model.FollowsHighlight && working.Fill.Highlight == false, "Custom gives the fill a color of its own");
      RectTransform plane = (RectTransform)ColorPickerProbe.Plane(picker.Root).transform;
      pickerRect = ScreenRectOf(picker.Root);
      Rect planeRect = ScreenRectOf(plane), hueRect = ScreenRectOf((RectTransform)ColorPickerProbe.Hue(picker.Root).transform),
        alphaRect = ScreenRectOf((RectTransform)ColorPickerProbe.Alpha(picker.Root).transform),
        hexRect = ScreenRectOf((RectTransform)ColorPickerProbe.Hex(picker.Root).transform);
      Assert(Mathf.Abs(plane.rect.width - 256) < .01f && Mathf.Abs(plane.rect.height - 218) < .01f, "the color square takes its column's height above its two bars");
      Assert(planeRect.center.y > hueRect.center.y && hueRect.center.y > alphaRect.center.y && hexRect.xMin >= planeRect.xMax
        && pickerRect.Contains(planeRect.min) && pickerRect.Contains(hexRect.min) && ColorPickerProbe.Title(picker.Root) == null
        && ColorPickerProbe.Close(picker.Root) == null,
        "the square, hue and opacity on the left, the color's numbers on the right, without a title or card");
      Assert(ColorPickerProbe.Plane(picker.Root).GetComponent<UiCursor>().Kind == UiCursorKind.Crosshair
        && ColorPickerProbe.Hue(picker.Root).GetComponent<UiCursor>().Kind == UiCursorKind.Horizontal
        && ColorPickerProbe.Hex(picker.Root).GetComponent<UiCursor>().Kind == UiCursorKind.Text,
        "the square and the bars keep their crosshair and slider cursors in the editor's panel");
      float topGap = ((RectTransform)picker.Root).anchoredPosition.y;
      Assert(pickerRect.yMin >= ScreenRectOf(dock.Root).yMax - 1 || Mathf.Abs(-topGap - (EditorFramePanel.TopBar + 8)) < 1,
        "with the color square, it still sits above the dock, or right under the header on a short screen: " + pickerRect + " top " + topGap);
      AssertInColumn(shapes, picker.Root, L("채우기 색", "Fill color"));
      foreach (TextMeshProUGUI label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "docked picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, canvases, name + "-shape-color-custom");
      ColorPickerProbe.Hex(picker.Root).text = "#2050A0";
      Assert(picker.Commit() && working.Fill.Hex == "#2050A0", "the fill takes the new color");
      Slider alphaSlider = ColorPickerProbe.Alpha(picker.Root);
      opacity.Field.Root.GetComponent<Button>().onClick.Invoke();
      Assert(picker.Visible && dock.Owner == "fill-opacity" && ShapeNumbersOf(shapes).Editing.Working.Serialize() == "100",
        "clicking the opacity types it in the dock, with the picker still open");
      Assert(picker.Visible && Shown(DockKey(dock, "Key done")), "the keypad below types the opacity, the picker staying open");
      foreach (char key in "\b\b\b50")
        ShapeNumbersOf(shapes).KeypadPress(key);
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Settle();
      Assert(Mathf.Abs(working.Fill.Alpha - .5f) < .001f && working.Fill.AlphaFormula == "" && Mathf.Abs(alphaSlider.value - .5f) < .001f
        && opacity.Field.Text == "50", "a typed opacity moves the slider");
      ShapeNumbersOf(shapes).FinishEditing();
      Assert(picker.Visible && dock.Owner == null, "done typing, the picker stays open for the color");
      Vector2 opacityPoint = ScreenCenter(opacity.Field.Root);
      Carry toOpacity = new Carry(dock, OverlayValueId.Accuracy).MoveTo(opacityPoint);
      Assert(picker.Visible && Field<DropState?>(opacity.Field, "_drop") == DropState.Accepts,
        "the picker stays open while a value is dragged, and its opacity lights up");
      toOpacity.LetGoAt(opacityPoint);
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.AlphaFormula == "accuracy" && dock.Owner == "fill-opacity" && opacity.Field.Computed && opacity.Field.Text == "99.5",
        "a value dropped on the opacity calculates it: " + working.Fill.AlphaFormula + " / " + opacity.Field.Text);
      Settle();
      Assert(Mathf.Abs(alphaSlider.value - .995f) < .001f && Mathf.Abs(working.Fill.Alpha - .5f) < .001f,
        "the slider shows the result; the plain opacity stays underneath");
      Assert(dock.ResultText.Contains("99.5%"), "the dock shows the opacity in percent: " + dock.ResultText);
      SaveFrame(camera, texture, canvases, name + "-shape-opacity");
      ColorPickerProbe.Hex(picker.Root).text = "#3060B0";
      // A frame passes after typing, as it does between keys and undo.
      Settle();
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.Hex == "#3060B0" && working.Fill.AlphaFormula == "accuracy", "a new color keeps the calculated opacity");
      Assert(picker.Commit() && Field<OverlayShapeSettings>(shapes, "_shape").Fill.AlphaFormula == "accuracy", "confirming the picker keeps it too");
      ShapeHistory(shapes).Undo();
      Settle();
      Assert(picker.Visible && Field<OverlayShapeSettings>(shapes, "_shape").Fill.Hex == "#2050A0"
        && ColorPickerProbe.Hex(picker.Root).text == "2050A0", "undo keeps the picker open, showing the restored color: "
        + ColorPickerProbe.Hex(picker.Root).text);
      ShapeHistory(shapes).Redo();
      alphaSlider.value = .3f;
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      Assert(working.Fill.AlphaFormula == "" && Mathf.Abs(working.Fill.Alpha - .3f) < .01f && !opacity.Field.Computed && opacity.Field.Text == "30",
        "moving the slider makes it a plain number again: " + opacity.Field.Text);
      Call(shapes, "CloseOpen");
      ColorPickerProbe.Hex(picker.Root).text = "#2050A0";
      working = Field<OverlayShapeSettings>(shapes, "_shape");
      PressBack(shapes);
      Assert(!picker.Visible && !fillButton.Active, "back closes the fill's picker");
      PressBar(shapes, strokeButton);
      Assert(picker.Visible && strokeButton.Active && !fillButton.Active && ScreenRectOf(picker.Root) == pickerRect
        && dock.HintText.Contains(Korean(name) ? "테두리 색" : "border color"), "Border opens the picker on its color, in the same place");
      AssertInColumn(shapes, picker.Root, L("테두리 색", "Border color"));
      PressBack(shapes);
      Assert(!picker.Visible && !strokeButton.Active && Shown(DockKey(dock, "Key done")), "back closes the picker; the rows and the keypad stay");

      // Where the shape grows from.
      PressBar(shapes, shapeGrow);
      GrowPopoverPanel grow = Field<GrowPopoverPanel>(Grow(shapes), "_popover");
      Assert(grow.Visible && InsideScreen(ScreenRectOf(grow.Root)) && shapeGrow.Active, "the grow point opens its own popover, on screen");
      AssertInColumn(shapes, grow.Root, L("기준점", "Grow from"));
      CheckGrowPopover(Grow(shapes));
      Call(Grow(shapes), "Pick", OverlayGrowth.TopLeft);
      Assert(Field<UiCapsuleGraphic>(Grow(shapes), "_mark").gameObject.activeSelf, "the preview marks the point that stays put");
      Assert(!shapeGrow.Value.Contains("Auto") && !shapeGrow.Value.Contains("자동"), "the button names the chosen point");
      SaveFrame(camera, texture, canvases, name + "-shape-grow");
      Call(shapes, "CloseOpen");
      Call(shapes, "Render");
      SaveFrame(camera, texture, canvases, name + "-shape-edited");

      PressEditorButton(shapes, "Shape editor done");
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
      Pressable editContent = ToolbarButton(editor, "Edit element");
      Assert(ElementBar(editor).Root == null && Shown(editContent)
        && editContent.GetComponentInChildren<TextMeshProUGUI>().text == (Korean(name) ? "도형 편집" : "Edit shape"),
        "a selected shape offers Edit shape where the scale was");
      Press(editContent);
      Assert(shapes.IsOpen, "Edit shape opens the shape editor");
      heightField = Measures(shapes).HeightField;
      Click(heightField.Root, "Height");
      ShapeNumbersOf(shapes).KeypadPress('9');
      Call(Grow(shapes), "Pick", OverlayGrowth.Center);
      PressEditorButton(shapes, "Shape editor cancel");
      Assert(shape.Enabled && shape.Shape.HeightFormula == "" && Mathf.Abs(shape.Shape.Height - OverlayShapeGeometry.DefaultSize) < .01f
        && shape.Settings.GrowFrom == OverlayGrowth.TopLeft, "cancel leaves the shape as it was");
      // Only Cancel throws an edit away: one open when the editor is torn down
      // (the mod turned off) is kept.
      Press(editContent);
      Click(Measures(shapes).HeightField.Root, "Height");
      ShapeNumbersOf(shapes).KeypadPress('9');
      shapes.Dispose();
      Assert(!shapes.IsOpen && Mathf.Abs(shape.Shape.Height * shape.EffectiveScale - 1609) < .01f,
        "an edit open when the editor is torn down is kept: " + shape.Shape.Height * shape.EffectiveScale);
      shape.Shape.Height = OverlayShapeGeometry.DefaultSize;
      shape.Apply();

      // Triangles and circles.
      Kind<ShapeKind>(editor).Add(OverlayShapeKind.Triangle);
      Assert(Field<List<Button>>(Measures(shapes), "_arcs").Count == 3, "a triangle has three corners");
      ShapeOverlayElement triangle = host.LayoutTargets.OfType<ShapeOverlayElement>().Last();
      CheckShapeLayout(shapes, width, height);
      SaveFrame(camera, texture, canvases, name + "-shape-triangle");
      shapes.Close(false, true);
      Assert(!triangle.Enabled, "cancelling a new shape removes it");
      Kind<ShapeKind>(editor).Add(OverlayShapeKind.Circle);
      Assert(Field<List<Button>>(Measures(shapes), "_arcs").Count == 0, "circles have only W and H");
      SaveFrame(camera, texture, canvases, name + "-shape-circle");
      shapes.Close(false, true);
      Call(editor, "ClosePopovers");
    }

    private static bool Korean(string name) => name.Contains("-ko");

    // The shape editor is made of parts; the checks reach each through its field.
    private static ShapeMeasures Measures(ShapeEditorScreen shapes) => Field<ShapeMeasures>(shapes, "_measures");
    private static ShapeNumbers ShapeNumbersOf(ShapeEditorScreen shapes) => Field<ShapeNumbers>(shapes, "_numbers");
    private static ShapeColors ShapeColorsOf(ShapeEditorScreen shapes) => Field<ShapeColors>(shapes, "_colors");
    private static ShapeGradient ShapeGradientOf(ShapeEditorScreen shapes) => Field<ShapeGradient>(shapes, "_gradient");
    private static EditorHistory<OverlayShapeSettings> ShapeHistory(ShapeEditorScreen shapes) =>
      Field<EditorHistory<OverlayShapeSettings>>(shapes, "_history");
    private static RectTransform ShapeRect(ShapeEditorScreen shapes) => Field<RectTransform>(Field<ShapePreview>(shapes, "_preview"), "_shapeRect");
    private static GrowFrom Grow(object editor) => Field<GrowFrom>(editor, "_grow");

    // So is the text editor.
    private static TextDocumentEditing TextDocumentOf(TextEditorScreen text) => Field<TextDocumentEditing>(text, "_text");
    private static TextPreview TextPreviewOf(TextEditorScreen text) => Field<TextPreview>(text, "_preview");
    private static TextMarks TextMarksOf(TextEditorScreen text) => Field<TextMarks>(text, "_marks");
    private static TextFormulas TextFormulasOf(TextEditorScreen text) => Field<TextFormulas>(text, "_formulas");
    private static TextColor TextColorOf(TextEditorScreen text) => Field<TextColor>(text, "_color");

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
    private static void CheckShapeLayout(ShapeEditorScreen shapes, int width, int height)
    {
      GameObject root = Field<EditorFramePanel>(shapes, "_frame").Root;
      foreach (RectTransform rect in root.GetComponentsInChildren<UiCapsuleGraphic>(false).Select(graphic => graphic.rectTransform))
      {
        if (rect.name == "ElementUI drag preview" || rect.name == "Value tooltip" || rect.GetComponentInParent<ScrollRect>() != null)
          continue;
        Assert(InsideScreen(ScreenRectOf(rect)), "shape editor control stays on screen: " + rect.name);
      }
      List<FormulaFieldView> measures = new List<FormulaFieldView>
      {
        Measures(shapes).WidthField, Measures(shapes).HeightField,
      };
      Rect shapeRect = ScreenRectOf(ShapeRect(shapes));
      foreach (FormulaFieldView field in measures)
        Assert(!ScreenRectOf(field.Root).Overlaps(shapeRect), "W and H stay off the shape: " + field.Name);
      measures.AddRange(Field<List<FormulaFieldView>>(Measures(shapes), "_radiusFields"));
      List<FormulaFieldView> shown = measures.Where(field => field.Root.gameObject.activeInHierarchy).ToList();
      for (int i = 0; i < shown.Count; i++)
      for (int j = i + 1; j < shown.Count; j++)
        Assert(!ScreenRectOf(shown[i].Root).Overlaps(ScreenRectOf(shown[j].Root)),
          "shape measures stay apart: " + shown[i].Name + " / " + shown[j].Name);
    }

    private static string FieldText(FormulaFieldView field) => Field<TextMeshProUGUI>(field, "_text").text;

    private static Vector2 ScreenCenter(RectTransform rect) =>
      RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

    // Unity's runtime is Mono: the settings file must keep a space that stands
    // alone between two values, as in the judgment row.
    private static void CheckSettingsKeepSpaces()
    {
      List<OverlayLayoutSettings> layouts = new List<OverlayLayoutSettings>
      {
        new OverlayLayoutSettings { Id = "judgement", Customized = true, Text = new OverlayTextSettings() },
      };
      layouts[0].Text.Runs.AddRange(OverlayDefaultRuns.Judgements(OverlayColorSettings.Custom("#D958FF"), 0));
      var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<OverlayLayoutSettings>));
      using (var file = new MemoryStream())
      {
        using (var writer = new StreamWriter(file, new System.Text.UTF8Encoding(false), 1024, true))
          serializer.Serialize(writer, layouts);
        file.Position = 0;
        List<OverlayLayoutSettings> loaded = SettingsXml.Read<List<OverlayLayoutSettings>>(file);
        Assert(loaded[0].Text.Runs.Count == layouts[0].Text.Runs.Count && loaded[0].Text.Runs[1].Text == " ",
          "spaces between judgments come back after a restart: '" + loaded[0].Text.Runs[1].Text + "'");
      }
    }

    // Several elements: modifier clicks, the marquee, moving, arrows, align,
    // distribute, delete and select all, each one undo step.
    private static void CheckMultiSelect(OverlayEditorScreen editor, OverlayCanvasHost host, ElementSettings settings, Camera camera,
      RenderTexture texture, Canvas[] canvases, string name)
    {
      var before = new LayoutSnapshot(host.LayoutSettings);
      List<IOverlayLayoutTarget> selection = Selection(editor).Targets;
      GameObject root = Session(editor).Root;
      // Three texts spread across the screen: the leftmost, the one nearest the middle, the rightmost.
      List<IOverlayLayoutTarget> shown = host.LayoutTargets.OfType<TextOverlayElement>().Where(text => text.Available && text.Enabled)
        .Cast<IOverlayLayoutTarget>().ToList();
      IOverlayLayoutTarget leftmost = shown.OrderBy(text => text.Bounds.xMin).First(), rightmost = shown.OrderBy(text => text.Bounds.xMax).Last();
      IOverlayLayoutTarget middle = shown.Where(text => text != leftmost && text != rightmost)
        .OrderBy(text => Mathf.Abs(text.Bounds.center.x - Screen.width * .5f)).First();
      IOverlayLayoutTarget[] texts = { leftmost, middle, rightmost };
      Assert(leftmost.Bounds.xMax < middle.Bounds.xMin && middle.Bounds.xMax < rightmost.Bounds.xMin, "three texts apart from each other");
      int a = IndexOf(editor, texts[0]), b = IndexOf(editor, texts[1]), c = IndexOf(editor, texts[2]);
      Selection(editor).SelectOnly(a);
      Selection(editor).Toggle(b);
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 2 && selection.Contains(texts[0]) && selection.Contains(texts[1]) && Selection(editor).Selected == b,
        "Cmd/Ctrl + click adds an element");
      Assert(root.GetComponentsInChildren<CanvasOutlineGraphic>(false).Length == 2, "each selected element has its outline");
      Assert(root.GetComponentsInChildren<CanvasOutlineGraphic>(false).All(outline => !Field<bool>(outline, "_handles")),
        "several selected show no resize handles");
      Settle();
      Assert(ByName<TextMeshProUGUI>(Toolbar(editor).Root, "Selection").text == (Korean(name) ? "2개 선택됨" : "2 selected"), "the toolbar counts the selection");
      TMP_InputField scaleInput = ScaleField(editor);
      Assert(!Shown(ToolbarButton(editor, "Edit element")) && Shown(scaleInput) && !scaleInput.interactable
        && scaleInput.text == "—" && !Shown(ByName<TextMeshProUGUI>(Toolbar(editor).Root, "%"))
        && ToolbarButton(editor, "Delete element").IsInteractable() && ToolbarButton(editor, "Reset element").IsInteractable()
        && !ToolbarButton(editor, "Increase size").IsInteractable() && !ToolbarButton(editor, "Decrease size").IsInteractable(),
        "several selected: no Edit, the scale greys out without its percent sign, delete and reset stay");
      RectTransform bar = ElementBar(editor).Root;
      Assert(bar != null && bar.gameObject.activeSelf && ElementBar(editor).Model is ArrangeViewModel, "several selected get the align toolbar");
      Func<UiIconKind, Pressable> arrange = kind =>
        bar.GetComponentsInChildren<Pressable>(true).Single(button => button.GetComponentInChildren<UiIcon>()?.Kind == kind);
      Assert(arrange(UiIconKind.AlignLeft).IsInteractable() && !arrange(UiIconKind.DistributeHorizontal).IsInteractable()
        && !arrange(UiIconKind.DistributeVertical).IsInteractable()
        && arrange(UiIconKind.DistributeVertical).GetComponentInChildren<UiIcon>().color == UiPalette.Disabled,
        "two can be aligned; spacing waits for three, greyed");
      Vector2 barGrip = PointerOn(ByName<RectTransform>(bar, "Drag element toolbar"));
      Assert(!ElementBar(editor).ControlAt(barGrip) && ElementBar(editor).ControlAt(PointerOn((RectTransform)arrange(UiIconKind.AlignLeft).transform)),
        "the align toolbar drags from its grip; its buttons take their presses");
      Assert(PopoverInside(editor, bar, Screen.width, Screen.height), "the align toolbar stays on screen");
      object[] hit = { texts[0].Bounds.center, null };
      Assert((int)typeof(CanvasDragInput).GetMethod("FindTarget").Invoke(Drag(editor), hit) == a && (LayoutHandle)hit[1] == LayoutHandle.Move,
        "each selected element moves the group");
      Selection(editor).Toggle(b);
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 1 && selection[0] == texts[0] && Selection(editor).Selected == a && ElementBar(editor).Root == null,
        "a second modifier click takes it out again");

      // The marquee: only what lies fully inside.
      Rect covered = Rect.MinMaxRect(texts.Min(t => t.SelectionBounds.xMin) - 4, texts.Min(t => t.SelectionBounds.yMin) - 4,
        texts.Max(t => t.SelectionBounds.xMax) + 4, texts.Max(t => t.SelectionBounds.yMax) + 4);
      Selection(editor).SelectOnly(-1);
      Marquee(editor).Begin(covered.min, -1, false);
      SetField(Marquee(editor), "_active", true);
      Call(Marquee(editor), "SelectInside", covered.max);
      Call(Marquee(editor), "Draw", covered.max);
      Call(editor, "UpdateFrames");
      Assert(texts.All(selection.Contains) && selection.All(t => covered.Contains(t.SelectionBounds.min) && covered.Contains(t.SelectionBounds.max)),
        "dragging over the canvas selects what lies fully inside");
      Assert(Field<Image>(Marquee(editor), "_fill").gameObject.activeSelf, "the selected area is drawn while dragging");
      SaveFrame(camera, texture, canvases, name + "-multi-marquee");
      Marquee(editor).End();
      Assert(!Field<Image>(Marquee(editor), "_fill").gameObject.activeSelf, "the area disappears on release");
      Rect inner = texts[0].SelectionBounds;
      Marquee(editor).Begin(inner.min + Vector2.one, -1, false);
      SetField(Marquee(editor), "_active", true);
      Call(Marquee(editor), "SelectInside", inner.max + Vector2.one * 4);
      Assert(!selection.Contains(texts[0]), "an element only partly inside stays out");
      Marquee(editor).End();
      Selection(editor).SelectOnly(a);
      Rect third = texts[2].SelectionBounds;
      Marquee(editor).Begin(third.min - Vector2.one * 3, -1, true);
      SetField(Marquee(editor), "_active", true);
      Call(Marquee(editor), "SelectInside", third.max + Vector2.one * 3);
      Assert(selection.Contains(texts[0]) && selection.Contains(texts[2]), "Cmd/Ctrl + drag adds to the selection, even starting on an element");
      Marquee(editor).End();
      // Released without moving: a modifier click toggles, a plain one on empty space clears.
      Marquee(editor).Begin(texts[1].Bounds.center, b, true);
      Marquee(editor).Release();
      Assert(selection.Count == 3 && selection.Contains(texts[1]), "a modifier click without moving adds what it hit");
      Marquee(editor).Begin(new Vector2(2, 2), -1, false);
      Marquee(editor).Release();
      Assert(selection.Count == 0 && Selection(editor).Selected == -1, "a click on empty space clears the selection");

      // Moving all three, as one undo step.
      foreach (int index in new[] { a, b, c })
        Selection(editor).Toggle(index);
      bool snapping = settings.LayoutSnapping;
      settings.LayoutSnapping = false;
      Rect[] start = texts.Select(t => t.Bounds).ToArray();
      Vector2 grab = texts[1].Bounds.center;
      SetField(Drag(editor), "_lastClickTime", -10f);
      Call(editor, "PressElement", b, LayoutHandle.Move, grab);
      Assert(selection.Count == 3 && Drag(editor).Dragging == b, "pressing one of several keeps them all");
      SetField(Drag(editor), "_pressMoved", true);
      Call(Drag(editor), "DragGroup", grab + new Vector2(30, 20));
      Call(editor, "FinishDrag");
      settings.LayoutSnapping = snapping;
      for (int i = 0; i < 3; i++)
        Assert(Vector2.Distance(texts[i].Bounds.position, start[i].position + new Vector2(30, 20)) < 1.5f,
          "dragging one moves every selected element the same: " + texts[i].Id);
      Call(editor, "Undo");
      Assert(texts.Select((t, i) => Vector2.Distance(t.Bounds.position, start[i].position)).All(d => d < 1.5f), "one undo puts them all back");
      SetField(Drag(editor), "_lastClickTime", -10f);
      Call(editor, "PressElement", b, LayoutHandle.Move, grab);
      Drag(editor).Release();
      Assert(selection.Count == 1 && selection[0] == texts[1], "a click on one of several, without moving, selects it alone");
      foreach (int index in new[] { a, c })
        Selection(editor).Toggle(index);
      Drag(editor).Nudge(10f, 0f);
      Assert(texts.Select((t, i) => t.Bounds.x - start[i].x).All(d => Mathf.Abs(d - 10) < 1.5f), "arrow keys move every selected element");
      Session(editor).History.Commit(host.LayoutSettings);
      Call(editor, "Undo");

      // Align and distribute.
      Call(editor, "UpdateFrames");
      bar = ElementBar(editor).Root;
      Settle();
      Assert(arrange(UiIconKind.DistributeHorizontal).IsInteractable(), "three can be spaced evenly");
      Press(arrange(UiIconKind.AlignLeft));
      float left = start.Min(r => r.x);
      Assert(texts.All(t => Mathf.Abs(t.Bounds.x - left) < 1.5f), "align left lines them up on the leftmost edge");
      Assert(selection.Count == 3, "aligning keeps the selection");
      SaveFrame(camera, texture, canvases, name + "-multi");
      Call(editor, "Undo");
      Call(editor, "UpdateFrames");
      bar = ElementBar(editor).Root;
      Settle();
      Press(arrange(UiIconKind.AlignMiddle));
      Assert(texts.Select(t => t.Bounds.center.y).Max() - texts.Select(t => t.Bounds.center.y).Min() < 1.5f, "align middle shares one vertical center");
      Call(editor, "Undo");
      Call(editor, "UpdateFrames");
      bar = ElementBar(editor).Root;
      Settle();
      Press(arrange(UiIconKind.DistributeHorizontal));
      Rect[] spaced = texts.Select(t => t.Bounds).OrderBy(r => r.x).ToArray();
      Assert(Mathf.Abs((spaced[1].x - spaced[0].xMax) - (spaced[2].x - spaced[1].xMax)) < 1.5f, "distribute leaves the same gaps: "
        + string.Join(" | ", start.Select(r => r.ToString())) + " -> " + string.Join(" | ", spaced.Select(r => r.ToString()))
        + " ids " + string.Join(",", texts.Select(t => t.Id)));
      Call(editor, "Undo");
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "undo after arranging restores every element");

      // A column already evenly spaced: the press says so and changes nothing.
      IOverlayLayoutTarget[] column = new[] { "progress", "accuracy", "time" }
        .Select(id => host.LayoutTargets.First(t => t.Id == id)).ToArray();
      Selection(editor).SelectOnly(IndexOf(editor, column[0]));
      Selection(editor).Toggle(IndexOf(editor, column[1]));
      Selection(editor).Toggle(IndexOf(editor, column[2]));
      Call(editor, "UpdateFrames");
      bar = ElementBar(editor).Root;
      Settle();
      Rect[] columnStart = column.Select(t => t.Bounds).ToArray();
      bool couldUndo = Session(editor).History.CanUndo;
      Press(arrange(UiIconKind.DistributeVertical));
      Toolbar(editor).Refresh();
      TextMeshProUGUI help = StatusLabel(editor);
      Assert(column.Select((t, i) => t.Bounds == columnStart[i]).All(same => same) && help.text.Length > 0
        && help.color == UiPalette.Foreground && Session(editor).History.CanUndo == couldUndo,
        "spacing an evenly spaced column says it already is, without an undo step: " + help.text);
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "nothing moved");
      foreach (int index in new[] { a, b, c })
        Selection(editor).Toggle(index);
      foreach (IOverlayLayoutTarget member in column)
        Selection(editor).Toggle(IndexOf(editor, member));
      Call(editor, "UpdateFrames");
      Assert(selection.Count == 3 && texts.All(selection.Contains), "back to the three spread texts");

      // Delete and select all.
      Call(editor, "DeleteSelected");
      Assert(texts.All(t => !t.Enabled) && selection.Count == 0, "delete removes every selected element");
      Call(editor, "Undo");
      Assert(texts.All(t => t.Enabled), "one undo brings them all back");
      Selection(editor).SelectAll();
      Call(editor, "UpdateFrames");
      int selectable = host.LayoutTargets.Count(t => t.Available && t.Enabled);
      Assert(selection.Count >= selectable && selection.All(t => t.Available && t.Enabled), "Cmd/Ctrl + A selects every element shown");
      Selection(editor).SelectOnly(-1);
      Call(editor, "UpdateFrames");
      Assert(before.Matches(new LayoutSnapshot(host.LayoutSettings)), "multi-select leaves the layout as it was");
    }

    private static void CheckKeyViewers(OverlayEditorScreen editor, PreviewKeyViewers viewers, Camera camera, RenderTexture texture, Canvas[] canvases, string name)
    {
      viewers.Availability = KeyViewerAvailability.Ready;
      Call(editor, "ToggleAddMenu");
      Press(Rows(AddMenu(editor)).Single(button => button.name == "Add KeyViewer"));
      IOverlayLayoutTarget hand = viewers.Targets.Last();
      Assert(hand.TemplateId == OverlayElementKinds.HandKeyViewer && Selection(editor).Selected == IndexOf(editor, hand),
        "a key viewer is added at once, as a hand viewer");
      Call(editor, "UpdateFrames");
      Settle();
      KeyViewerBarViewModel kind = (KeyViewerBarViewModel)ElementBar(editor).Model;
      Assert(kind.Viewer == 0 && kind.Target == hand, "key viewer settings show hand selected");
      Assert(SegmentShown(ElementBar(editor).Root) == 0, "the hand/foot control shows hand: " + SegmentShown(ElementBar(editor).Root));
      AssertCutOut(editor, ElementBar(editor).Root, "the key viewer's toolbar");
      List<Exception> uiErrors = new List<Exception>();
      ElementUI.Runtime.ErrorReported += uiErrors.Add;
      // As the player clicks: the click lands, then the editor's frame runs.
      ClickOption(ElementBar(editor).Root, L("발", "Foot"));
      IOverlayLayoutTarget foot = viewers.Targets.Last();
      Assert(foot.TemplateId == OverlayElementKinds.FootKeyViewer && !hand.Enabled && foot.Bounds.center == hand.Bounds.center,
        "switching to foot replaces the viewer in place");
      Assert(Selection(editor).Selected == IndexOf(editor, foot), "the switched viewer stays selected");
      // Frames as the game runs them: the editor's tick, then ElementUI's own driver, which sleeps when idle.
      Frames(() => Call(editor, "UpdateFrames"), 3);
      Assert(ElementBar(editor).Model == kind && kind.Target == foot && kind.Viewer == 1,
        "the bar follows the switched viewer instead of being made again");
      Assert(SegmentShown(ElementBar(editor).Root) == 1, "the hand/foot control moves to foot: " + SegmentShown(ElementBar(editor).Root));
      AssertCutOut(editor, ElementBar(editor).Root, "the switched viewer's toolbar");
      // And back: the same control keeps taking clicks after a switch.
      ClickOption(ElementBar(editor).Root, L("손", "Hand"));
      IOverlayLayoutTarget handAgain = viewers.Targets.Last();
      // Frames as the game runs them: the editor's tick, then ElementUI's own driver, which sleeps when idle.
      Frames(() => Call(editor, "UpdateFrames"), 3);
      Assert(handAgain != foot && handAgain.TemplateId == OverlayElementKinds.HandKeyViewer && handAgain.Enabled && !foot.Enabled
        && Selection(editor).Selected == IndexOf(editor, handAgain) && kind.Target == handAgain && kind.Viewer == 0,
        "switching back to hand works from the same toolbar");
      Assert(SegmentShown(ElementBar(editor).Root) == 0, "the hand/foot control moves back to hand: " + SegmentShown(ElementBar(editor).Root));
      ClickOption(ElementBar(editor).Root, L("발", "Foot"));
      foot = viewers.Targets.Last();
      // Frames as the game runs them: the editor's tick, then ElementUI's own driver, which sleeps when idle.
      Frames(() => Call(editor, "UpdateFrames"), 3);
      Assert(foot.TemplateId == OverlayElementKinds.FootKeyViewer && foot.Enabled && kind.Target == foot && kind.Viewer == 1
        && SegmentShown(ElementBar(editor).Root) == 1, "and to foot again");
      ElementUI.Runtime.ErrorReported -= uiErrors.Add;
      Assert(uiErrors.Count == 0, "switching hand and foot reports no ElementUI error" + (uiErrors.Count > 0 ? ": " + uiErrors[0] : ""));
      Assert(ToolbarModel(editor).Selection.Value == L("발 키뷰어", "Foot key viewer"), "the toolbar names the foot viewer");
      Pressable openInApp = ByName<Pressable>(ElementBar(editor).Root, "Edit key viewer");
      TextMeshProUGUI openLabel = openInApp.GetComponentInChildren<TextMeshProUGUI>();
      openLabel.ForceMeshUpdate();
      Assert(!openLabel.isTextTruncated, "Edit key viewer fits its button: " + openLabel.text);
      SaveFrame(camera, texture, canvases, name + "-keyviewer");
      Press(openInApp);
      Assert(viewers.OpenedInApp.Count == 1 && viewers.OpenedInApp[0] == foot,
        "Edit key viewer opens the viewer it shows now, after switching to foot");
      viewers.EditResult = KeyViewerEditResult.AppOutdated;
      Press(openInApp);
      Assert(StatusLabel(editor).text.Contains("ElementNote") && StatusLabel(editor).color == UiPalette.Error,
        "a refused open says what to do with ElementNote: " + StatusLabel(editor).text);
      viewers.EditResult = KeyViewerEditResult.Opened;
      Status(editor).Clear();
      PointerArea openArea = openInApp.GetComponentInParent<PointerArea>();
      openArea.Hover(true);
      Assert(Status(editor).Text.Value.Contains("ElementNote"), "hovering Edit key viewer says what it opens");
      openArea.Hover(false);
      Call(editor, "UpdateFrames");
      Settle();
      Assert(!Shown(ToolbarButton(editor, "Edit element")) && Shown(ScaleField(editor)) && ScaleField(editor).interactable
        && ToolbarButton(editor, "Increase size").IsInteractable(), "key viewers keep their scale");
      // The field and the view model share the words: typed ones come in, dropped ones go back.
      TMP_InputField viewerScale = ScaleField(editor);
      viewerScale.text = "130";
      Settle();
      Assert(ToolbarModel(editor).Scale.Value == "130", "typing in the scale field writes the typed scale");
      ToolbarModel(editor).DropTypedScale();
      Settle();
      Assert(viewerScale.text == "100", "a dropped typed scale shows the viewer's again: " + viewerScale.text);
      RectTransform popup = ElementBar(editor).Root;
      Assert(popup != null && popup.gameObject.activeSelf && ByName<RectTransform>(popup, "Drag element toolbar") != null,
        "a key viewer's toolbar has a grip to move it");
      Assert(!ElementBar(editor).ControlAt(PointerOn(ByName<RectTransform>(popup, "Drag element toolbar")))
        && ElementBar(editor).ControlAt(PointerOn((RectTransform)openInApp.transform))
        && ElementBar(editor).ControlAt(PointerOn((RectTransform)ByName<Pressable>(popup, "Option " + L("손", "Hand")).transform)),
        "the key viewer's toolbar drags from its grip; hand, foot and Edit take their presses");
      Rect barBefore = (Rect)typeof(ElementBarPanel).GetProperty("Bounds", Private).GetValue(ElementBar(editor));
      Vector2 grab = barBefore.position + new Vector2(14, 20);
      ElementBar(editor).BeginDrag(grab);
      ElementBar(editor).Move(grab + new Vector2(-60, -40));
      ElementBar(editor).Dragging = false;
      Rect barAfter = (Rect)typeof(ElementBarPanel).GetProperty("Bounds", Private).GetValue(ElementBar(editor));
      Assert(Mathf.Abs(barAfter.x - (barBefore.x - 60)) < .5f && Mathf.Abs(barAfter.y - (barBefore.y - 40)) < .5f,
        "the element toolbar follows its grip one to one");
      int footIndex = IndexOf(editor, foot);
      Select(editor, -1);
      Select(editor, footIndex);
      Rect barKept = (Rect)typeof(ElementBarPanel).GetProperty("Bounds", Private).GetValue(ElementBar(editor));
      Assert(barKept.position == barAfter.position, "the element toolbar keeps its place for the next selection");
      var cutouts = editor.NativeExclusions;
      Assert(cutouts.Count == 2, "native viewers get each panel as its own cutout: " + cutouts.Count);
    }

    private static void CheckTextEditor(OverlayEditorScreen editor, OverlayCanvasHost host, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      TextOverlayElement fps = host.LayoutTargets.OfType<TextOverlayElement>().Single(text => text.Id == "fps");
      Select(editor, IndexOf(editor, fps));
      ElementSettings preferences = Session(editor).Preferences;
      preferences.ReduceUiMotion = false;
      // Another mod's values, as ElementKit adds them, and one whose mod is missing.
      OverlayValueGroup sample = new OverlayValueGroup("Sample", "예시", "Sample");
      OverlayValueCatalog.Register("Sample/hand", sample, OverlayValueKind.Text, "지금 손", "Hand", double.NaN, "왼손");
      OverlayValueCatalog.Register("Sample/left-keys", sample, OverlayValueKind.Number, "왼손 키", "Left keys", 5, null);
      OverlayValueCatalog.Register("Sample/pattern", sample, OverlayValueKind.Text, "지금 패턴", "Pattern", double.NaN, "2 + 2 + 1");
      OverlayValueCatalog.TryResolve("Gone/value", out _);
      // A mod that adds values in three groups, attached through ElementKit as a mod's Load does.
      AttachPreviewMods(host.Values);
      PressEdit(editor);
      TextEditorScreen text = (TextEditorScreen)Kind<TextKind>(editor).Editor;
      Assert(text.IsOpen && editor.NativeExclusionBounds == new Rect(0, 0, Screen.width, Screen.height), "Edit text opens the editor over everything");
      EditorFramePanel screen = Field<EditorFramePanel>(text, "_frame");
      TextMeshProUGUI[] titles = screen.Root.GetComponentsInChildren<TextMeshProUGUI>(true).Where(label => label.text == "텍스트 편집" || label.text == "Edit text").ToArray();
      Assert(titles.Length == 1 && titles[0].transform.IsChildOf(ByName<RectTransform>(screen.Frame, "Header")),
        "the editor's name shows once, in its header: no title card");
      CanvasGroup editorFade = screen.Root.GetComponent<CanvasGroup>();
      Assert(editorFade.alpha < 1, "the editor fades in instead of cutting in");
      ElementUI.Runtime.Flush(EditorFramePanel.FadeSeconds * .5f);
      Assert(editorFade.alpha > 0 && editorFade.alpha < 1, "the editor fade progresses through a visible intermediate state");
      ElementUI.Runtime.Flush(EditorFramePanel.FadeSeconds);
      screen.Tick();
      Assert(editorFade.alpha == 1, "the fade finishes");
      DockProbe dock = DockOf(text);
      Assert(dock.Root.gameObject.activeInHierarchy && dock.Owner == null && dock.HintText.Length > 0,
        "the input dock is always there, resting with a hint");
      CheckEditorTopLine(text, "Text editor", L("텍스트 편집", "Edit text"));
      RectTransform format = EditorBarRoot(text);
      PropertyButtonViewModel sizeButton = Field<TextSize>(text, "_size").Button, colorButton = TextColorOf(text).Button,
        textGrow = Grow(text).Button, fontButton = Field<TextFonts>(text, "_fonts").Button,
        animateButton = Field<TextAnimate>(text, "_animate").Button, ifButton = Field<TextIf>(text, "_if").Button;
      PropertyButtonViewModel[] textBar = { fontButton, sizeButton, colorButton, ifButton, animateButton, textGrow };
      Pressable[] barButtons = format.GetComponentsInChildren<Pressable>(true);
      Assert(textBar.All(button => BarButton(text, button) != null && BarButton(text, button).transform.IsChildOf(format)) && barButtons.Length == 6
        && new[] { typeof(TextEditorScreen), typeof(TextColor) }.All(type => type.GetField("_opacityField", Private) == null) && textGrow.Value.Length > 0 && fontButton.Value.Length > 0,
        "font, size, color, If, animation and grow point share one column; no opacity, no + Value");
      Assert(BarButtonRect(text, animateButton).y < BarButtonRect(text, colorButton).y
        && BarButtonRect(text, animateButton).y > BarButtonRect(text, textGrow).y, "animation sits between the color and the grow point");
      Rect formatRect = ScreenRectOf(format), keypadColumn = KeypadRect(DockOf(text));
      Assert(Mathf.Abs(formatRect.xMin - keypadColumn.xMin) < 1 && Mathf.Abs(formatRect.xMax - keypadColumn.xMax) < 1 && formatRect.yMin > keypadColumn.yMax,
        "the properties stand in a column above the keypad, as wide as it: " + formatRect + " / " + keypadColumn);
      Assert(ScreenRectOf(TextPreviewOf(text).Text.rectTransform).xMax < formatRect.xMin, "the text stays left of the column");
      string[] textLabels = Korean(name)
        ? new[] { "폰트", "크기", "글자 색", "만약", "애니메이션", "기준점" }
        : new[] { "Font", "Size", "Text color", "If", "Animate", "Grow from" };
      for (int i = 0; i < textBar.Length; i++)
      {
        Pressable drawn = BarButton(text, textBar[i]);
        Assert(drawn.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == textLabels[i]),
          "each row says what it is: " + textLabels[i]);
        Assert(drawn.targetGraphic.transform == drawn.transform, "a row lights up where it is drawn");
        if (i > 0)
          Assert(BarButtonRect(text, textBar[i]).yMax <= BarButtonRect(text, textBar[i - 1]).yMin + .5f
            && Mathf.Abs(BarButtonRect(text, textBar[i]).width - BarButtonRect(text, textBar[0]).width) < .5f, "rows stack one under another, all as wide");
      }
      RoundedRect[] swatchCircles = BarButton(text, colorButton).GetComponentsInChildren<RoundedRect>(false);
      RoundedRect swatchRing = swatchCircles.Single(graphic => Mathf.Abs(graphic.rectTransform.rect.width - 18) < .01f
        && Mathf.Abs(graphic.rectTransform.rect.height - 18) < .01f && Mathf.Abs(graphic.Radius - 9) < .01f);
      RoundedRect swatchFill = swatchCircles.Single(graphic => Mathf.Abs(graphic.rectTransform.rect.width - 14) < .01f
        && Mathf.Abs(graphic.rectTransform.rect.height - 14) < .01f && Mathf.Abs(graphic.Radius - 7) < .01f);
      Assert((CenterIn(swatchFill.rectTransform, swatchRing.transform) - (Vector3)swatchRing.rectTransform.rect.center).magnitude < .01f
        && !swatchRing.raycastTarget && !swatchFill.raycastTarget
        && BarButton(text, fontButton).GetComponentInChildren<UiIcon>().Kind == UiIconKind.Font
        && BarButton(text, sizeButton).GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == L("가", "Aa"))
        && BarButton(text, sizeButton).GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "px"),
        "the color leads with its swatch, the font with its icon, the size with letters and ends in px");
      Assert(!barButtons.Any(button => button.name.EndsWith("smaller") || button.name.EndsWith("larger")), "numbers have no − and + buttons");
      foreach (TextMeshProUGUI label in format.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "property row label fits: " + label.text);
      }
      float actual = fps.EffectiveScale * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      Assert(Mathf.Abs(Field<float>(TextPreviewOf(text), "_previewScale") - actual) < .001f, "the edited text is shown at its game size");
      OverlayTextDocument document = TextDocumentOf(text).Document;
      Assert(document.Count == 7 && document[6].IsValue, "the editor reads FPS | {fps}");
      Assert(sizeButton.Value.Length > 0 && !sizeButton.IsComputed, "the size button shows the size");
      SaveFrame(camera, texture, canvases, name + "-text");

      // Animate opens a tool in the column: growth acts on values, the whole
      // text's with nothing selected. Pressed as the player does, with the
      // screen catching up only at the end of each frame, as in the game.
      OverlayTextPulse previewPulse = TextPreviewOf(text).Pulse;
      Assert(!document[6].Animate, "FPS starts still");
      TextAnimate animateTool = Field<TextAnimate>(text, "_animate");
      Assert(animateButton.Opens && !animateButton.Toggle && animateButton.Value == (Korean(name) ? "꺼짐" : "Off"),
        "the Animate row opens a tool and says nothing is on: " + animateButton.Value);
      PressBar(text, animateButton);
      Assert(animateTool.Visible, "the Animate row opens its tool");
      AssertInColumn(text, animateTool.Root, Korean(name) ? "애니메이션" : "Animate");
      Pressable drawnAnimate = ByName<Pressable>(animateTool.Root, "Grow on change");
      Assert(drawnAnimate != null, "the tool has a switch to grow on each change");
      RoundedRect[] switchShapes = drawnAnimate.GetComponentsInChildren<RoundedRect>(false);
      RoundedRect animateTrack = switchShapes.Single(graphic => Mathf.Abs(graphic.rectTransform.rect.width - 30) < .01f
        && Mathf.Abs(graphic.rectTransform.rect.height - 18) < .01f && Mathf.Abs(graphic.Radius - 9) < .01f);
      RoundedRect animateThumb = switchShapes.Single(graphic => Mathf.Abs(graphic.rectTransform.rect.width - 10) < .01f
        && Mathf.Abs(graphic.rectTransform.rect.height - 10) < .01f && Mathf.Abs(graphic.Radius - 5) < .01f);
      Func<float> thumbX = () => CenterIn(animateThumb.rectTransform, animateTrack.transform).x - animateTrack.rectTransform.rect.xMin - 5;
      Func<Color, Color, bool> sameColor = (a, b) => ((Vector4)(a - b)).magnitude < .01f;
      Assert(Mathf.Abs(thumbX() - 4) < .01f
        && Mathf.Abs(CenterIn(animateThumb.rectTransform, animateTrack.transform).y - animateTrack.rectTransform.rect.center.y) < .01f
        && !animateTrack.raycastTarget && !animateThumb.raycastTarget && sameColor(animateThumb.color, UiPalette.Foreground),
        "the 30 by 18 capsule starts with its centered 10-point thumb inset by 4; the row owns clicks");
      // Notices show right above the dock's formula line.
      TextMeshProUGUI topLine = ByName<TextMeshProUGUI>(DockOf(text).Root, "Notice");
      string animateNotice = Korean(name) ? "값이 바뀔 때마다" : "Each time the value changes";
      // The frame before drew a change that took layout (a notice), and the
      // driver went back to sleep after it.
      screen.ViewModel.Notice(L("미리보기를 바꿨어요", "The preview changed"), false);
      Frames(() => QuietFrame(text), 3);
      Press(drawnAnimate);
      document = TextDocumentOf(text).Document;
      Assert(document[6].Animate && previewPulse.Playing, "the switch makes FPS grow on each change and shows it once");
      Frames(() => QuietFrame(text), 1);
      Assert(thumbX() > 4 && thumbX() < 16 && sameColor(animateThumb.color, UiPalette.SoftAccent),
        "the thumb moves partway in one frame while its on color changes immediately: " + thumbX());
      Frames(() => QuietFrame(text), 39);
      Assert(screen.ViewModel.NoticeText.Contains(animateNotice) && topLine.text.Contains(animateNotice),
        "turning it on says what it does, on the screen: " + topLine.text);
      Assert(Mathf.Abs(thumbX() - 16) < .01f && sameColor(animateThumb.color, UiPalette.SoftAccent),
        "the switch slides over to the exact on inset and lights up in the game's frames: " + thumbX());
      Assert(animateButton.Value == (Korean(name) ? "커지기" : "Grow"), "the row says what is on: " + animateButton.Value);
      SaveFrame(camera, texture, canvases, name + "-text-animate");
      previewPulse.Stop();
      SetField(TextDocumentOf(text), "_caretIndex", 3); SetField(TextDocumentOf(text), "_anchor", 0);
      Call(text, "Render");
      Press(drawnAnimate);
      Settle();
      document = TextDocumentOf(text).Document;
      Assert(document[6].Animate && screen.ViewModel.NoticeText.Contains(Korean(name) ? "값이 없어요" : "no value"),
        "letters alone stay as they are and say why: " + screen.ViewModel.NoticeText);
      SetField(TextDocumentOf(text), "_caretIndex", 7); SetField(TextDocumentOf(text), "_anchor", 7);
      Call(text, "Render");
      screen.ViewModel.Notice(L("커서를 옮겼어요", "The caret moved"), false);
      Frames(() => QuietFrame(text), 3);
      Press(drawnAnimate);
      Frames(() => QuietFrame(text), 1);
      Assert(thumbX() > 4 && thumbX() < 16 && sameColor(animateThumb.color, UiPalette.Foreground),
        "the thumb moves back partway in one frame while its off color changes immediately: " + thumbX());
      Frames(() => QuietFrame(text), 39);
      document = TextDocumentOf(text).Document;
      Assert(!document[6].Animate && !previewPulse.Playing, "a second press turns it off without moving anything");
      Assert(Mathf.Abs(thumbX() - 4) < .01f && sameColor(animateThumb.color, UiPalette.Foreground),
        "the switch slides back to the exact off inset in the game's frames: " + thumbX());
      PressBack(text);
      Assert(!animateTool.Visible, "back closes the Animate tool");
      preferences.ReduceUiMotion = true;

      // With nothing being calculated, a value from the dock goes in at the caret.
      SetField(TextDocumentOf(text), "_caretIndex", 7); SetField(TextDocumentOf(text), "_anchor", 7);
      TextDocumentOf(text).Type(" x");
      Call(text, "Render");
      TextMeshProUGUI preview = TextPreviewOf(text).Text;
      Rect dockRect = ScreenRectOf(dock.Root);
      Assert(InsideScreen(dockRect) && dockRect.yMax < ScreenRectOf(preview.rectTransform).yMin, "the text stays above the dock");
      Vector2 chipPoint = ScreenCenter((RectTransform)DockChip(dock, "Value fps").transform);
      int itemsBefore = TextDocumentOf(text).Document.Count;
      Carry carried = new Carry(dock, OverlayValueId.Fps, open: true);
      Assert(dock.Dragging && GameObject.Find("ElementUI drag preview") != null, "a chip carries its value out of the dock");
      carried.LetGoAt(chipPoint);
      Assert(!dock.Dragging && TextDocumentOf(text).Document.Count == itemsBefore, "let go over the dock, it lands nowhere");
      CheckValueDock(text, dock, host, camera, texture, canvases, name);
      DockChip(dock, "Value combo");
      SaveFrame(camera, texture, canvases, name + "-text-values");
      Press(DockChip(dock, "Value combo"));
      document = TextDocumentOf(text).Document;
      Assert(document.Count == 10 && document[9].Value == "combo" && Field<int>(TextDocumentOf(text), "_caretIndex") == 10 && Field<int>(TextDocumentOf(text), "_anchor") == 10,
        "a clicked value lands at the caret, and typing goes on after it");
      Press(DockChip(dock, "Value miss"));
      document = TextDocumentOf(text).Document;
      Assert(document.Count == 11 && document[9].Value == "combo" && document[10].Value == "miss", "a second value follows the first");
      TextDocumentOf(text).Undo();
      TextFormulasOf(text).OpenChip(9);
      FormulaEditing editing = TextFormulasOf(text).Editing;
      Assert(dock.Owner == "value:9" && editing.Active && editing.Slot.IsText, "clicking a value in the text calculates it in the dock");

      // The dock shows the formula; the text shows the result.
      TextFormulasOf(text).KeypadPress('*');
      TextFormulasOf(text).KeypadPress('2');
      document = TextDocumentOf(text).Document;
      Assert(document[9].Value == "{combo}*2" && preview.text.Contains("240") && !preview.text.Contains("×"),
        "the text shows the result: " + document[9].Value);
      Assert(dock.FormulaText.Contains("×") && dock.ResultText.Contains("240"), "the dock shows the formula and its result");
      host.Values.Set(OverlayValueId.Combo, new System.Text.StringBuilder("50"), 50);
      Call(text, "FollowLiveValues");
      Assert(preview.text.Contains("100") && dock.ResultText.Contains("100"), "a live combo moves the text and the dock's result: " + dock.ResultText);
      host.Values.Clear(OverlayValueId.Combo);
      Call(text, "FollowLiveValues");
      Assert(preview.text.Contains("240"), "without live data the example comes back");
      TextFormulasOf(text).KeypadPress('+');
      Vector2 line = ScreenCenter(ByName<RectTransform>(dock.Root, "Formula line"));
      new Carry(dock, OverlayValueId.Miss).LetGoAt(line);
      document = TextDocumentOf(text).Document;
      Assert(document[9].Value == "{combo}*2+{miss}", "a value dropped on the formula line joins it: " + document[9].Value);
      Assert(dockRect.yMax < ScreenRectOf(preview.rectTransform).yMin, "the dock never covers the text");
      // The screenshot shows the dock's caret on: settling moves the blink along, so start it over after.
      Action settle = _refresh;
      _refresh = () =>
      {
        settle?.Invoke();
        DockOf(text).Blink(true);
        ElementUI.Runtime.Flush(0);
      };
      SaveFrame(camera, texture, canvases, name + "-text-keypad");
      _refresh = settle;
      Press(DockKey(dock, "Key done"));
      Assert(dock.Owner == null, "Done lets the dock rest and leaves the text alone");
      Field<TextPointerInput>(text, "_pointer").UpdateHover(ScreenCenterOfItem(text, 9));
      Assert(screen.TipText.Contains("×") && screen.TipText.Contains("+"), "hovering a value names it: " + screen.TipText);
      TextFormulasOf(text).OpenChip(9);
      Assert(dock.Owner == "value:9" && TextFormulasOf(text).Editing.Working.Serialize() == "{combo}*2+{miss}",
        "clicking a value reopens its formula");
      Call(text, "CloseOpen");
      Call(text, "Render");

      // As the player does it: a click on the value in the text, then the
      // keypad's keys, with the screen catching up only at the end of each
      // frame, as in the game.
      Vector2 valuePoint = ScreenCenterOfItem(text, 9);
      Assert(Field<TextLayoutMap>(text, "_map").TryValueAt(valuePoint, out int clicked) && clicked == 9 && !dock.Contains(valuePoint)
        && (bool)typeof(TextEditorScreen).GetMethod("InTextArea", Private).Invoke(text, new object[] { valuePoint }),
        "a click on the value in the text lands on it");
      Frames(() => QuietFrame(text), 3);
      Pressable times = DockKeyAsIs(dock, "Key ×"), three = DockKeyAsIs(dock, "Key 3"), back = DockKeyAsIs(dock, "Key backspace");
      Assert(!times.IsInteractable(), "a resting keypad takes no key");
      // The frame before drew a change that took layout (a notice), and the
      // driver went back to sleep after it.
      screen.ViewModel.Notice(L("값을 골랐어요", "A value was picked"), false);
      Frames(() => QuietFrame(text), 3);
      TextFormulasOf(text).OpenChip(9);
      Frames(() => QuietFrame(text), 2);
      Assert(dock.Owner == "value:9" && times.IsInteractable() && !three.IsInteractable(),
        "the clicked value's keypad takes an operator by the next frame, a digit only after one");
      Press(times);
      Frames(() => QuietFrame(text), 2);
      Assert(three.IsInteractable(), "after × the digits can be pressed");
      Press(three);
      Frames(() => QuietFrame(text), 2);
      TextMeshProUGUI drawnFormula = ByName<TextMeshProUGUI>(dock.Root, "Formula text");
      document = TextDocumentOf(text).Document;
      Assert(document[9].Value == "{combo}*2+{miss}*3" && drawnFormula.text.EndsWith("×  3"),
        "pressed keys calculate the value, and the dock shows it: " + document[9].Value + " / " + drawnFormula.text);
      Press(back);
      Frames(() => QuietFrame(text), 2);
      Press(back);
      Frames(() => QuietFrame(text), 2);
      Assert(TextDocumentOf(text).Document[9].Value == "{combo}*2+{miss}", "backspace takes the keys back off: " + TextDocumentOf(text).Document[9].Value);
      Call(text, "CloseOpen");
      Call(text, "Render");

      // Dragging a value into the text marks where it lands.
      Vector3[] previewCorners = new Vector3[4];
      preview.rectTransform.GetWorldCorners(previewCorners);
      Vector2 end = RectTransformUtility.WorldToScreenPoint(null, previewCorners[2]) + new Vector2(20, -4);
      Carry intoText = new Carry(dock, OverlayValueId.Fps).MoveTo(end);
      Assert(Field<Image>(TextMarksOf(text), "_dropCaret").enabled, "a dragged value shows where it will land");
      Assert(GameObject.Find("ElementUI drag preview") != null, "the value follows the pointer");
      SaveFrame(camera, texture, canvases, name + "-text-drag");
      intoText.LetGoAt(end);
      document = TextDocumentOf(text).Document;
      Assert(document.Count == 11 && document[10].Value == "fps", "a dropped value lands in the text");
      TextDocumentOf(text).Undo();
      Call(text, "CloseOpen");

      // The size in the bar: words are refused; values size the selection.
      SetField(TextDocumentOf(text), "_anchor", 9); SetField(TextDocumentOf(text), "_caretIndex", 10);
      Call(text, "Render");
      Vector2 sizePoint = BarButtonRect(text, sizeButton).center;
      new Carry(dock, OverlayValueId.SongTitle).LetGoAt(sizePoint);
      document = TextDocumentOf(text).Document;
      Assert(screen.ViewModel.NoticeText.Length > 0 && document[9].SizeFormula == "", "the song title is refused by the size");
      new Carry(dock, OverlayValueId.ProgressCurrent).LetGoAt(sizePoint);
      document = TextDocumentOf(text).Document;
      Assert(document[9].SizeFormula == "progress-current" && document[0].SizeFormula == "" && dock.Owner == "size" && sizeButton.Active
        && BarButton(text, sizeButton).GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "48"),
        "the size takes a value for the selection and opens in the dock");
      Assert(sizeButton.Value == "48" && sizeButton.IsComputed, "the size shows its result, marked as calculated: " + sizeButton.Value);
      Call(text, "CloseOpen");
      TextDocumentOf(text).Undo();

      // Sizes: the keypad types pixels into the selection.
      SetField(TextDocumentOf(text), "_anchor", 0); SetField(TextDocumentOf(text), "_caretIndex", 3);
      Call(text, "Render");
      string shown = (25 * fps.EffectiveScale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
      Assert(sizeButton.Value == shown, "the size button shows the selection's size as drawn: " + shown);
      PressBar(text, sizeButton);
      Assert(dock.Owner == "size", "the size opens in the dock");
      foreach (char key in "\b\b\b\b40")
        TextFormulasOf(text).KeypadPress(key);
      Call(text, "CloseOpen");
      document = TextDocumentOf(text).Document;
      Assert(Mathf.Abs(document[0].Size * fps.EffectiveScale - 40) < .05f && document[3].Size == 0, "a size applies to the selection only");
      PressBar(text, colorButton);
      UiColorPicker picker = TextColorOf(text).Picker;
      Rect pickerRect = ScreenRectOf(picker.Root);
      Assert(picker.Visible && colorButton.Active && picker.Column && Shown(DockKey(dock, "Key done"))
        && Mathf.Abs(pickerRect.xMax - KeypadRect(dock).xMax) < 1 && pickerRect.yMin >= ScreenRectOf(dock.Root).yMax - 1
        && InsideScreen(pickerRect), "the color picker opens above the keypad: " + pickerRect);
      AssertInColumn(text, picker.Root, L("선택한 글자 색", "Selected text color"));
      Press(ColorPickerProbe.Segment(picker.Root, 1));
      Settle();
      ColorPickerProbe.Hex(picker.Root).text = "#40C0FF";
      picker.Commit();
      SaveFrame(camera, texture, canvases, name + "-text-color");
      OpacityFieldView textOpacity = TextColorOf(text).Opacity;
      Assert(textOpacity.Field.Root.gameObject.activeInHierarchy && textOpacity.Field.Text == "100", "the text color's opacity is a number in the picker");
      Vector2 alphaPoint = ScreenCenter(textOpacity.Field.Root);
      new Carry(dock, OverlayValueId.SongTitle).LetGoAt(alphaPoint);
      document = TextDocumentOf(text).Document;
      Assert(document[0].Color.AlphaFormula == "" && screen.ViewModel.NoticeText.Length > 0 && picker.Visible, "words are refused by the opacity");
      new Carry(dock, OverlayValueId.ProgressCurrent).LetGoAt(alphaPoint);
      document = TextDocumentOf(text).Document;
      Assert(picker.Visible && document[0].Color.AlphaFormula == "progress-current" && document[3].Color.AlphaFormula == ""
        && dock.Owner == "opacity" && textOpacity.Field.Text == "48" && textOpacity.Field.Computed,
        "the picker's opacity takes a value for the selection");
      Assert(picker.Visible && Shown(DockKey(dock, "Key done")), "its formula is typed on the keypad below, the picker staying open");
      Assert(picker.Commit() && TextDocumentOf(text).Document[0].Color.AlphaFormula == "progress-current"
        && TextDocumentOf(text).Document[0].Color.Hex == "#40C0FF", "confirming the color keeps the calculated opacity");
      // Done waits while the picker holds a color it can't read; Esc closes the picker first.
      ColorPickerProbe.Hex(picker.Root).text = "#40";
      PressEditorButton(text, "Text editor done");
      Assert(text.IsOpen && picker.Visible, "Done waits while the picker holds a color it can't read");
      Settle();
      Assert(ColorPickerProbe.Error(picker.Root).text.Length > 0 && picker.Model.FocusHex.Value > 0,
        "and says what to type, its HEX taking the keyboard: " + ColorPickerProbe.Error(picker.Root).text);
      ColorPickerProbe.Hex(picker.Root).text = "#40C0FF";
      text.Escape();
      Assert(text.IsOpen && !picker.Visible, "Esc closes the picker before the editor");
      Assert(!colorButton.Active && dock.Owner == null && Shown(DockKey(dock, "Key done")),
        "Text color lets go once the picker closes, and so does its opacity; the keypad is back");

      // The picker stays while another part of the text is selected, and goes on with it.
      SetField(TextDocumentOf(text), "_anchor", 0); SetField(TextDocumentOf(text), "_caretIndex", 3);
      Call(text, "Render");
      PressBar(text, colorButton);
      Assert(picker.Visible, "the color picker opens again on the selection");
      OverlayColorSettings valueColorBefore = TextDocumentOf(text).Document[9].Color.Clone();
      TextFormulasOf(text).OpenChip(9);
      Assert(picker.Visible && colorButton.Active && dock.Owner == "value:9"
        && TextColorOf(text).Title == L("선택한 글자 색", "Selected text color"),
        "a value clicked in the text keeps the picker open, on that value, its formula in the dock");
      Press(ColorPickerProbe.Segment(picker.Root, 1));
      Settle();
      ColorPickerProbe.Hex(picker.Root).text = "#FF8000";
      picker.Commit();
      document = TextDocumentOf(text).Document;
      Assert(document[9].Color.Hex == "#FF8000" && !document[9].Color.Highlight && document[0].Color.Hex == "#40C0FF"
        && document[8].Color.Hex != "#FF8000", "the color goes to the clicked value alone: " + document[8].Color.Hex);
      TextDocumentOf(text).Undo();
      document = TextDocumentOf(text).Document;
      Assert(document[9].Color.Hex == valueColorBefore.Hex && document[9].Color.Highlight == valueColorBefore.Highlight
        && document[0].Color.Hex == "#40C0FF", "one undo takes back the value's color, not the selection's before it");
      SetField(TextDocumentOf(text), "_anchor", 2); SetField(TextDocumentOf(text), "_caretIndex", 2);
      Call(text, "Render");
      Assert(picker.Visible && colorButton.Active, "a click that selects nothing keeps the picker open");
      AssertInColumn(text, picker.Root, L("글자 색", "Text color"));
      text.Escape();
      Assert(text.IsOpen && !picker.Visible, "Esc still closes it");

      // Where the text grows from.
      PressBar(text, textGrow);
      GrowPopoverPanel grow = Field<GrowPopoverPanel>(Grow(text), "_popover");
      Assert(grow.Visible && InsideScreen(ScreenRectOf(grow.Root)) && textGrow.Active, "the grow point opens its own popover, on screen");
      AssertInColumn(text, grow.Root, L("기준점", "Grow from"));
      CheckGrowPopover(Grow(text));
      Call(Grow(text), "Pick", OverlayGrowth.Right);
      Assert(Field<UiCapsuleGraphic>(Grow(text), "_mark").gameObject.activeSelf, "the text marks the point that stays put");
      SaveFrame(camera, texture, canvases, name + "-text-grow");
      Call(text, "CloseOpen");

      TextDocumentOf(text).DeleteRange(8, 9);
      document = TextDocumentOf(text).Document;
      Assert(document.Count == 9 && document[8].Value == "{combo}*2+{miss}", "deleting next to a value removes one step");
      TextDocumentOf(text).Undo();
      Assert(TextDocumentOf(text).Document.Count == 10, "the text editor has its own undo");
      PressEditorButton(text, "Text editor done");
      Assert(!text.IsOpen && fps.Settings.Text.Runs.Any(run => run.Value == "{combo}*2+{miss}") && !fps.Settings.Text.Runs[0].Color.Highlight
        && fps.Settings.Text.Runs[0].Color.Hex == "#40C0FF" && fps.Settings.Text.Runs[0].Size > 0 && fps.Settings.GrowFrom == OverlayGrowth.Right,
        "Done saves the runs with their colors, sizes, formulas and grow point");
      Assert(Field<TextMeshProUGUI>(fps, "_text").text.Contains("240"), "the overlay calculates the saved formula with samples");
      Call(editor, "Undo");
      Assert(!fps.Settings.Text.Runs.Any(run => run.Value.Contains("combo")) && fps.Settings.GrowFrom == OverlayGrowth.Auto,
        "a text edit is one layout undo step");

      TextOverlayElement progress = host.LayoutTargets.OfType<TextOverlayElement>().Single(element => element.Id == "progress");
      Select(editor, IndexOf(editor, progress));
      Kind<TextKind>(editor).Open(progress, false);
      document = TextDocumentOf(text).Document;
      Assert(!document[0].IsValue && Enumerable.Range(0, document.Count).Any(i => document[i].Value == "progress-current"),
        "a converted label opens as plain words");
      text.Close(false, true);

      Kind<TextKind>(editor).Add();
      Assert(text.IsOpen && TextDocumentOf(text).Document.Count == 0, "Add Element → Text opens an empty editor");
      RectTransform hint = Field<TextMeshProUGUI>(TextMarksOf(text), "_placeholder").rectTransform, emptyCaret = Field<Image>(TextMarksOf(text), "_emptyCaret").rectTransform;
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

    private static Vector2 ScreenCenterOfItem(TextEditorScreen text, int item)
    {
      Field<TextLayoutMap>(text, "_map").ItemRect(item, out Rect local);
      TextMeshProUGUI preview = TextPreviewOf(text).Text;
      return RectTransformUtility.WorldToScreenPoint(null, preview.rectTransform.TransformPoint(local.center));
    }

    private static void CheckHighlightColor(OverlayEditorScreen editor, ElementSettings settings, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      Call(editor, "ToggleColor");
      HighlightColorPanel popover = Field<HighlightColorPanel>(Field<CanvasPopovers>(editor, "_popovers"), "_colorPopover");
      UiColorPicker controls = Field<UiColorPicker>(popover, "_picker");
      Settle();
      Assert(popover.Visible && !ColorPickerProbe.Shown(ColorPickerProbe.Segment(controls.Root, 0))
        && ColorPickerProbe.Title(controls.Root).text == L("하이라이트 색상", "Highlight color") && ColorPickerProbe.Shown(ColorPickerProbe.Close(controls.Root))
        && ColorPickerProbe.Shown(ColorPickerProbe.Default(controls.Root)) && !ColorPickerProbe.Shown(ColorPickerProbe.Alpha(controls.Root)),
        "the highlight color itself has a title, close and Default, no opacity and no Highlight/Custom choice");
      TMP_InputField hex = ColorPickerProbe.Hex(controls.Root);
      Press(ColorPickerProbe.Default(controls.Root));
      UiTheme.TryParseHighlight(UiTheme.DefaultHighlight, out Color defaultHighlight);
      Assert(settings.HighlightColor == "#" + ColorUtility.ToHtmlStringRGB(defaultHighlight), "Default puts Element's highlight back: " + settings.HighlightColor);
      string before = settings.HighlightColor;
      hex.text = "#12";
      Assert(settings.HighlightColor == before && !popover.Commit(), "an incomplete color cannot commit");
      hex.text = "#204060";
      Assert(popover.Commit() && settings.HighlightColor == "#204060", "a valid highlight commits");
      SaveFrame(camera, texture, canvases, name + "-color");
      popover.Hide();
    }

    // Fonts: a TTF and every face of a TTC from the Fonts folder, installed
    // fonts listed in the background, the overlay's default font, a font on
    // some characters only, importing a file, and a font this PC lacks.
    private static void CheckFonts(OverlayEditorScreen editor, OverlayCanvasHost host, ElementSettings settings, Camera camera,
      RenderTexture texture, Canvas[] canvases, string name)
    {
      OverlayFonts fonts = host.Fonts;
      IOverlayFontLibrary library = fonts.Library;
      IReadOnlyList<OverlayFontFace> imported = library.Imported;
      Assert(imported.Any(face => face.Family == "Georgia") && imported.Count(face => face.Path.EndsWith(".ttc")) > 1,
        "the Fonts folder lists a TTF and every face of a TTC: " + string.Join(", ", imported.Select(face => face.Key)));
      string georgia = imported.First(face => face.Family == "Georgia").Key;
      OverlayFontFace typewriterFace = imported.First(face => face.Family.StartsWith("American Typewriter"));
      string typewriter = typewriterFace.Key;

      Call(editor, "ToggleFont");
      UiFontPopover popover = Field<UiFontPopover>(Field<CanvasPopovers>(editor, "_popovers"), "_fontPopover");
      Call(editor, "UpdatePopupPositions");
      Assert(popover.Visible && PopoverInside(editor, popover.Root, Screen.width, Screen.height), "the default font list opens on screen");
      Assert(editor.NativeExclusions.Count > 0 && editor.NativeExclusionBounds.Contains(ScreenCenter(editor, popover.Root)),
        "native viewers leave the font list uncovered");
      // An empty field draws its caret around a zero-width space; it must sit
      // in the middle of the field, beside the hint, not on the field's top edge.
      TMP_InputField search = Field<TMP_InputField>(popover, "_search");
      search.ForceLabelUpdate();
      search.textComponent.ForceMeshUpdate();
      TMP_CharacterInfo emptyCaret = search.textComponent.textInfo.characterInfo[0];
      float caretMiddle = (emptyCaret.ascender + emptyCaret.descender) * .5f;
      Rect searchArea = search.textComponent.rectTransform.rect;
      Assert(Mathf.Abs(caretMiddle - searchArea.center.y) < 3 && emptyCaret.ascender - emptyCaret.descender > 10,
        "the empty font search's caret is centered in the field: " + caretMiddle + " in " + searchArea);
      library.RequestInstalled();
      var waited = System.Diagnostics.Stopwatch.StartNew();
      while (library.Installed == null && waited.ElapsedMilliseconds < 30000)
        System.Threading.Thread.Sleep(50);
      Assert(library.Installed != null && library.Installed.Count > 20 && library.Installed.Count(face => face.Path != null) > 20,
        "installed fonts are listed with their files: " + (library.Installed?.Count ?? -1));
      popover.Refresh();
      Call(popover, "Pick", (int)popover.GetType().GetMethod("IndexOf", Private).Invoke(popover, new object[] { georgia }));
      host.TickLayoutPreview();
      TextOverlayElement fps = host.LayoutTargets.OfType<TextOverlayElement>().Single(element => element.Id == "fps");
      TextMeshProUGUI drawn = Field<TextMeshProUGUI>(fps, "_text");
      drawn.ForceMeshUpdate();
      Assert(settings.OverlayFont == georgia && drawn.font == fonts.DefaultAsset && drawn.font.faceInfo.familyName == "Georgia",
        "picking a default font redraws every text in it: " + settings.OverlayFont);
      Assert(drawn.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline)
        && drawn.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay), "a chosen font keeps the overlay's outline and shadow");
      foreach (TextMeshProUGUI label in popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
        Assert(label.font == Session(editor).Font, "the font list itself stays in the UI font");
      SaveFrame(camera, texture, canvases, name + "-font");

      // A font without Latin letters takes them from the OS's Latin font, as
      // the OS draws it, not from the bundled font that was there before.
      OverlayFontFace hebrew = library.Installed.FirstOrDefault(face => face.Family == "Arial Hebrew Scholar" && face.Style == "Regular");
      if (hebrew != null)
      {
        Call(popover, "Pick", (int)popover.GetType().GetMethod("IndexOf", Private).Invoke(popover, new object[] { hebrew.Key }));
        host.TickLayoutPreview();
        drawn.ForceMeshUpdate();
        TMP_CharacterInfo[] glyphs = drawn.textInfo.characterInfo;
        TMP_CharacterInfo letter = glyphs.First(c => c.character == 'F');
        TMP_CharacterInfo digit = glyphs.First(c => char.IsDigit(c.character));
        Assert(drawn.font == fonts.DefaultAsset && digit.fontAsset == fonts.DefaultAsset
          && letter.fontAsset != null && letter.fontAsset != fonts.Bundled && letter.fontAsset.faceInfo.familyName == "Helvetica",
          "a font without Latin letters draws them in the OS's Latin font: " + letter.fontAsset?.faceInfo.familyName
            + " / digits " + digit.fontAsset?.faceInfo.familyName);
        SaveFrame(camera, texture, canvases, name + "-font-latin-fallback");
        Call(popover, "Pick", (int)popover.GetType().GetMethod("IndexOf", Private).Invoke(popover, new object[] { georgia }));
        host.TickLayoutPreview();
      }

      // A file chosen in the dialog is copied into the folder and applied.
      Call(popover, "Import");
      Assert(File.Exists(Path.Combine(_fonts, "Verdana.ttf")) && library.Imported.Any(face => face.Family == "Verdana")
        && settings.OverlayFont.StartsWith("Verdana|"), "an added file lands in the Fonts folder and is applied: " + settings.OverlayFont);
      Assert(fonts.Status("Element Missing Font|Bold") == OverlayFontStatus.Missing, "a font this PC lacks reads as missing");
      Call(popover, "Pick", (int)popover.GetType().GetMethod("IndexOf", Private).Invoke(popover, new object[] { georgia }));
      popover.Hide();
      Assert(!popover.Visible, "the font list closes");

      // In the text editor, a font goes to the selection, or the whole text.
      Select(editor, IndexOf(editor, fps));
      PressEdit(editor);
      TextEditorScreen text = (TextEditorScreen)Kind<TextKind>(editor).Editor;
      SetField(TextDocumentOf(text), "_anchor", 0);
      SetField(TextDocumentOf(text), "_caretIndex", 3);
      Call(Field<TextFonts>(text, "_fonts"), "ToggleFont");
      UiFontPopover textFonts = Field<UiFontPopover>(Field<TextFonts>(text, "_fonts"), "_fontPopover");
      Assert(textFonts.Visible, "the text editor's Font opens its list");
      Call(textFonts, "Pick", (int)textFonts.GetType().GetMethod("IndexOf", Private).Invoke(textFonts, new object[] { typewriter }));
      TextMeshProUGUI preview = TextPreviewOf(text).Text;
      preview.ForceMeshUpdate();
      TMP_CharacterInfo[] characters = preview.textInfo.characterInfo;
      Assert(characters[0].fontAsset != null && characters[0].fontAsset.faceInfo.familyName == typewriterFace.Family
        && characters[3].fontAsset == fonts.DefaultAsset,
        "a font picked for selected characters draws only them: " + characters[0].fontAsset?.faceInfo.familyName
          + " / " + characters[3].fontAsset?.faceInfo.familyName);
      PropertyButtonViewModel textFont = Field<TextFonts>(text, "_fonts").Button;
      Assert(textFont.Value.StartsWith("American") && textFont.Active, "the row takes the selection's font, lit while the list is open: " + textFont.Value);
      AssertInColumn(text, textFonts.Root, L("선택한 글자 폰트", "Selected text font"));
      SaveFrame(camera, texture, canvases, name + "-text-font");
      // The list stays while another part is selected, marking that part's font; its pick is its own undo step.
      SetField(TextDocumentOf(text), "_anchor", 3);
      SetField(TextDocumentOf(text), "_caretIndex", 5);
      Call(text, "Render");
      Assert(textFonts.Visible && textFont.Active && textFont.Value == L("기본", "Default")
        && Field<string>(textFonts, "_current") == "", "another selection keeps the font list open, marking its font: " + textFont.Value);
      AssertInColumn(text, textFonts.Root, L("선택한 글자 폰트", "Selected text font"));
      Call(textFonts, "Pick", (int)textFonts.GetType().GetMethod("IndexOf", Private).Invoke(textFonts, new object[] { typewriter }));
      Assert(TextDocumentOf(text).Document[3].Font.Length > 0 && TextDocumentOf(text).Document[5].Font == "",
        "the font goes to the new selection");
      TextDocumentOf(text).Undo();
      Assert(TextDocumentOf(text).Document[3].Font == "" && TextDocumentOf(text).Document[0].Font.Length > 0,
        "one undo takes back that pick alone");
      SetField(TextDocumentOf(text), "_anchor", 0);
      SetField(TextDocumentOf(text), "_caretIndex", 0);
      Call(text, "Render");
      Assert(textFonts.Visible, "a click that selects nothing keeps the list open");
      AssertInColumn(text, textFonts.Root, L("글자 폰트", "Text font"));
      SetField(TextDocumentOf(text), "_anchor", 0);
      SetField(TextDocumentOf(text), "_caretIndex", 3);
      Call(text, "Render");
      PressBack(text);
      Assert(!textFonts.Visible && !textFont.Active
        && BarButton(text, textFont).GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == textFont.Value),
        "back closes the list; the row names the selection's font: " + textFont.Value);
      TextDocumentOf(text).Undo();
      OverlayTextDocument document = TextDocumentOf(text).Document;
      Assert(Enumerable.Range(0, document.Count).All(i => document[i].Font == ""), "one undo takes back the fonts tried");
      text.Close(false, true);
      Assert(fps.Settings.Text.Runs.All(run => string.IsNullOrEmpty(run.Font)), "cancel leaves the text's fonts as they were");
    }

    private static Vector2 ScreenCenter(OverlayEditorScreen editor, RectTransform panel) =>
      (PopoverPlacement.ScreenRect(panel)).center;

    private static string L(string korean, string english) => RDString.language == SystemLanguage.Korean ? korean : english;

    // The system dialog can't open in batch mode; this one picks a fixed file.
    private sealed class PreviewFontPicker : IFontFilePicker
    {
      private readonly string _path;
      public PreviewFontPicker(string path) { _path = path; }
      public IReadOnlyList<string> Pick(string title) => new[] { _path };
    }

    private static bool PopoverInside(OverlayEditorScreen editor, RectTransform popover, int width, int height)
    {
      Rect bounds = PopoverPlacement.ScreenRect(popover);
      return bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= width + .5f && bounds.yMax <= height + .5f;
    }

    private static CanvasSession Session(OverlayEditorScreen editor) => Field<CanvasSession>(editor, "_session");
    private static ToolbarPanel Toolbar(OverlayEditorScreen editor) => Field<ToolbarPanel>(editor, "_toolbar");
    private static CanvasSelection Selection(OverlayEditorScreen editor) => Field<CanvasSelection>(editor, "_selection");
    private static CanvasMarqueeInput Marquee(OverlayEditorScreen editor) => Field<CanvasMarqueeInput>(editor, "_marquee");
    private static CanvasDragInput Drag(OverlayEditorScreen editor) => Field<CanvasDragInput>(editor, "_drag");
    private static ElementBarPanel ElementBar(OverlayEditorScreen editor) => Field<ElementBarPanel>(editor, "_elementBar");

    // A click as the EventSystem delivers it, on the option of the bar's segmented control.
    private static void ClickOption(RectTransform bar, string option)
    {
      Pressable button = ByName<Pressable>(bar, "Option " + option);
      ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);
    }

    // The option the segmented control draws as picked: the one its pill sits under.
    private static int SegmentShown(RectTransform bar)
    {
      RectTransform pill = ByName<RectTransform>(bar, "Selection");
      Pressable[] options = bar.GetComponentsInChildren<Pressable>(true).Where(option => option.name.StartsWith("Option ")).ToArray();
      for (int i = 0; i < options.Length; i++)
        if (Mathf.Abs(((RectTransform)options[i].transform).anchoredPosition.x - pill.anchoredPosition.x) < 1)
          return i;
      return -1;
    }

    // Native key viewers draw above Unity: each editor panel must be cut out where it is drawn.
    private static void AssertCutOut(OverlayEditorScreen editor, RectTransform panel, string what)
    {
      Rect drawn = PopoverPlacement.ScreenRect(panel);
      bool covered = editor.NativeExclusions.Any(cutout => cutout.Bounds.X <= drawn.xMin + .5f && cutout.Bounds.Y <= drawn.yMin + .5f
        && cutout.Bounds.Right >= drawn.xMax - .5f && cutout.Bounds.Bottom >= drawn.yMax - .5f);
      Assert(covered, what + " is cut out of native viewers where it is drawn: " + drawn + " / "
        + string.Join(", ", editor.NativeExclusions.Select(cutout => cutout.Bounds.X + "," + cutout.Bounds.Y + " " + cutout.Bounds.Width + "x" + cutout.Bounds.Height)));
    }
    private static T Kind<T>(OverlayEditorScreen editor) where T : class, IElementKind => Field<ElementKinds>(editor, "_kinds").Get<T>();
    private static RectTransform AddMenu(OverlayEditorScreen editor) => Field<ListMenu>(Field<AddMenuPanel>(editor, "_addMenu"), "_menu").Root;

    private static void Select(OverlayEditorScreen editor, int index)
    {
      Selection(editor).Selected = index;
      Call(editor, "UpdateFrames");
      Settle();
    }

    private static int IndexOf(OverlayEditorScreen editor, IOverlayLayoutTarget target) =>
      Session(editor).IndexOf(target);

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    private static void Cleanup(OverlayEditorScreen editor, GameObject root, GameObject eventsRoot, GameObject cameraRoot,
      RenderTexture texture, OverlayCanvasHost host)
    {
      if (editor.IsOpen)
        editor.Close(false);
      editor.Dispose();
      cameraRoot.GetComponent<Camera>().targetTexture = null;
      Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot);
      Call(eventsRoot.GetComponent<EventSystem>(), "OnDisable");
      Object.DestroyImmediate(eventsRoot);
      if (host.Root != null)
        Object.DestroyImmediate(host.Root);
    }

    // Overlay canvases render through the camera only while a frame is captured,
    // so element geometry stays in the same screen pixels the editor measures.
    private static Action _refresh;
    // UI_PREVIEW_SCALE=2 saves the layout frames at twice the fixture's
    // screen, the same layout drawn sharper, for reading small text.
    private static readonly int FrameScale = int.TryParse(System.Environment.GetEnvironmentVariable("UI_PREVIEW_SCALE"), out int scale) ? Mathf.Clamp(scale, 1, 4) : 1;
    private static void SaveFrame(Camera camera, RenderTexture target, Canvas[] canvases, string name)
    {
      _refresh?.Invoke();
      UiTidyAudit.Inspect(name);
      RenderTexture sharp = FrameScale > 1 ? new RenderTexture(target.width * FrameScale, target.height * FrameScale, 24) : null;
      RenderTexture drawn = sharp ?? target;
      float[] factors = canvases.Select(canvas => canvas.scaleFactor).ToArray();
      camera.targetTexture = drawn;
      foreach (Canvas canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
      canvases[0].planeDistance = 2;
      if (sharp != null)
        for (int i = 0; i < canvases.Length; i++) canvases[i].scaleFactor = factors[i] * FrameScale;
      Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = drawn;
      Texture2D image = new Texture2D(drawn.width, drawn.height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, drawn.width, drawn.height), 0, 0); image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      RenderTexture.active = null; Object.DestroyImmediate(image);
      camera.targetTexture = target;
      for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].scaleFactor = factors[i]; }
      if (sharp != null) Object.DestroyImmediate(sharp);
      Canvas.ForceUpdateCanvases();
    }
    private static void SaveFrame(Camera camera, RenderTexture target, int width, int height, string name)
    {
      UiTidyAudit.Inspect(name);
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
      public KeyViewerEditResult EditResult { get; set; } = KeyViewerEditResult.Opened;
      public List<IOverlayLayoutTarget> OpenedInApp { get; } = new List<IOverlayLayoutTarget>();
      public KeyViewerEditResult OpenInApp(IOverlayLayoutTarget target)
      {
        OpenedInApp.Add(target);
        return EditResult;
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
      Assert(fresh.HighlightColor == "#BA712B" && UiTheme.DefaultHighlight == "#BA712B", "new settings and theme use brown default");
      var legacy = new ElementSettings { HighlightColor = " #7cce00 " };
      SettingsMigrator.Migrate(legacy);
      Assert(legacy.HighlightColor == "#BA712B" && legacy.SettingsVersion >= 1, "old default migrates once");
      legacy.HighlightColor = "#7CCE00"; SettingsMigrator.Migrate(legacy);
      Assert(legacy.HighlightColor == "#7CCE00", "explicit green after migration is preserved");
      var custom = new ElementSettings { HighlightColor = "#336699" };
      SettingsMigrator.Migrate(custom); Assert(custom.HighlightColor == "#336699", "migration preserves custom color");
      Assert(fresh.SettingsShortcutKey == KeyCode.F9 && fresh.SettingsShortcutModifier == KeyCode.LeftControl,
        "new settings open with Left Ctrl + F9");
      var oldShortcut = new ElementSettings { SettingsVersion = 6, SettingsShortcutKey = KeyCode.Tab, SettingsShortcutModifier = KeyCode.LeftAlt };
      SettingsMigrator.Migrate(oldShortcut);
      Assert(oldShortcut.SettingsShortcutKey == KeyCode.F9 && oldShortcut.SettingsShortcutModifier == KeyCode.LeftControl
        && oldShortcut.SettingsVersion >= 7, "old Left Alt + Tab default moves to Left Ctrl + F9");
      oldShortcut.SettingsShortcutKey = KeyCode.Tab; oldShortcut.SettingsShortcutModifier = KeyCode.LeftAlt; SettingsMigrator.Migrate(oldShortcut);
      Assert(oldShortcut.SettingsShortcutKey == KeyCode.Tab, "Left Alt + Tab chosen after migration is preserved");
      var customShortcut = new ElementSettings { SettingsVersion = 6, SettingsShortcutKey = KeyCode.BackQuote, SettingsShortcutModifier = KeyCode.None };
      SettingsMigrator.Migrate(customShortcut);
      Assert(customShortcut.SettingsShortcutKey == KeyCode.BackQuote && customShortcut.SettingsShortcutModifier == KeyCode.None,
        "migration preserves a custom shortcut");
      foreach (bool hidden in new[] { false, true })
      foreach (int version in new[] { 0, 1 })
      {
        var old = new ElementSettings { SettingsVersion = version, HidePerfectJudgmentText = hidden };
        SettingsMigrator.Migrate(old);
        Assert(old.Gameplay.Judgement.HidePerfect == hidden && old.Gameplay.Judgement.HideXPerfect == hidden,
          "outer legacy Perfect visibility migrates to Perfect +/- and XP before normalization: " + version + " / " + hidden);
        old.Gameplay.Judgement.HidePerfect = !hidden;
        old.HidePerfectJudgmentText = !hidden;
        SettingsMigrator.Migrate(old);
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
      SettingsMigrator.Migrate(released);
      SettingsMigrator.Migrate(released);
      Assert(released.SettingsVersion == ElementSettings.CurrentSettingsVersion && released.OverlayLayouts[0].Text.Runs.Count == 2 && released.OverlayLayouts[0].Text.Runs[1].Text == "%",
        "1.1.0 texts get their units back once");
      Assert(new[] { OverlayElementDefaults.ProgressBarId, OverlayElementDefaults.ProgressTrackId, OverlayElementDefaults.ProgressFillId }
        .All(id => released.OverlayLayouts.Count(layout => layout.Id == id) == 1),
        "earlier layouts get the progress bar once");
      var presets = new ElementSettings { SettingsVersion = 2 };
      SettingsMigrator.Migrate(presets);
      Assert(presets.OverlayLayouts.Single(layout => layout.Id == "accuracy").Text.Runs.Count(run => run.Text == "%") == 1,
        "presets from before 1.1 convert straight to the new form");
      foreach (bool hidden in new[] { false, true })
      {
        var grouped = new ElementSettings { SettingsVersion = 2 };
        grouped.Gameplay.Judgement.HidePerfect = hidden;
        SettingsMigrator.Migrate(grouped);
        Assert(grouped.Gameplay.Judgement.HidePerfect == hidden && grouped.Gameplay.Judgement.HideXPerfect == hidden,
          "existing gameplay Perfect group migrates its XP visibility without consulting the obsolete outer switch");
      }
    }









    // This probe checks the UI/render-service boundary only. The deliberately
    // rectangular test textures are not ADOFAI artwork or a game-render test.
    // The longest status: a download that outlasted startup.
    private sealed class PreviewDialogs : IFileDialogs
    {
      public int Opened;
      public FileDialogResult Open(FileDialogRequest request) { Opened++; return FileDialogResult.Cancelled; }
      public FileDialogResult Save(FileDialogRequest request) { Opened++; return FileDialogResult.Cancelled; }
    }

    // Three profiles, the newest in use, as a player who made two copies would have.
    // The settings window on ElementUI: open it, walk the pages, and photograph one.
    private static readonly System.Collections.Generic.Dictionary<string, string> PageFields = new System.Collections.Generic.Dictionary<string, string>
    {
      ["_home"] = "Home", ["_tweaks"] = "Tweaks", ["_general"] = "General", ["_effects"] = "Effects",
      ["_editorTweaks"] = "EditorTweaks", ["_judgment"] = "Judgment", ["_planets"] = "Planets",
      ["_presetEditor"] = "PresetEditor", ["_chatter"] = "Chatter", ["_profiles"] = "Profiles",
    };

    private static void Render(int width, int height, SystemLanguage language, string page, string name)
    {
      RDString.language = language;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      Assert(font != null, "overlay font exists");
      ElementSettings settings = new ElementSettings { ReduceUiMotion = true };
      UiTheme.SetHighlight(settings.HighlightColor);
      bool overlayOpened = false;
      using var planetPreview = new PlanetPreviewProbe();
      PreviewDialogs dialogs = new PreviewDialogs();
      ProfileService profiles = PreviewProfiles(settings, dialogs);
      OverlayFontCatalog fontCatalog = page == "_profiles"
        ? new OverlayFontCatalog(_fonts, new PreviewFontPicker(VerdanaFile), Debug.LogWarning, (action, exception) => Debug.LogException(exception))
        : null;
      OverlayFonts overlayFonts = fontCatalog == null ? null : new OverlayFonts(font, font.material, fontCatalog, fontCatalog);
      SettingsWindowScreen panel = new SettingsWindowScreen(settings, font, () => {}, () => true, () => overlayOpened = true, _ => {},
        new SettingsFeatures { RenderPlanetPreview = planetPreview.Render, Updates = new PreviewUpdates(), Profiles = profiles, OverlayFonts = () => overlayFonts });
      Assert(planetPreview.Calls == 0, "constructing settings does not render a hidden planet preview");
      GameObject root = Field<GameObject>(panel, "_root");
      panel.Open();
      SettingsWindowViewModel model = Field<SettingsWindowViewModel>(panel, "_model");
      Canvas canvas = root.GetComponent<Canvas>();
      GameObject cameraRoot = new GameObject("UI preview camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .055f);
      RenderTexture target = new RenderTexture(width, height, 24);
      camera.targetTexture = target;
      try
      {
        Assert(model.Current == model.Home, "settings open on Home");
        model.Home.OpenTweaks.TryExecute();
        Assert(model.Current == model.Tweaks && model.CanGoBack.Value, "Tweaks opens within settings, with Back");
        model.Back.TryExecute();
        Assert(model.Current == model.Home && !model.CanGoBack.Value, "Back returns to Home");
        model.Home.OpenEffects.TryExecute();
        Assert(model.Current == model.Effects, "Effects opens within settings");
        model.Back.TryExecute();
        model.Home.OpenGeneral.TryExecute();
        Assert(model.Current == model.General, "General opens within settings");
        model.Back.TryExecute();
        model.Home.EditOverlay.TryExecute();
        Assert(overlayOpened && !root.activeSelf, "the Overlay card hands off directly to the overlay editor");
        panel.Open();
        Assert(planetPreview.Calls == 0, "Home, Tweaks, Effects and General do not render planet textures");
        SettingsPageViewModel shown = (SettingsPageViewModel)typeof(SettingsWindowViewModel).GetField(PageFields[page]).GetValue(model);
        Assert(shown != null, page + " exists");
        model.Show(shown);
        ElementUI.Runtime.Probe();
        panel.Resize(width, height);
        for (int i = 0; i < 4; i++) { ElementUI.Runtime.Flush(1); Canvas.ForceUpdateCanvases(); }
        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(false)) label.ForceMeshUpdate();
        SettingsCheck check = new SettingsCheck
        {
          Panel = panel, Model = model, Root = root, Settings = settings, Width = width, Height = height, Name = name,
          Features = Field<SettingsWindowViewModel>(panel, "_model").Features,
          Save = frame => SaveFrame(camera, target, new[] { canvas }, frame),
        };
        _checks += SettingsPageChecks.Run(page, check);
        check.Settle();
        SaveFrame(camera, target, new[] { canvas }, name);
        _checks += 8;
      }
      finally
      {
        panel.Dispose();
        Object.DestroyImmediate(root);
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(cameraRoot);
      }
    }

    private static ProfileService PreviewProfiles(ElementSettings settings, PreviewDialogs dialogs)
    {
      bool korean = RDString.language == SystemLanguage.Korean;
      ProfileLibrary library = new ProfileLibrary(settings, korean ? "기본" : "Default");
      library.TryCreate(korean ? "방송용" : "Stream", out _, out _);
      library.TryCreate(korean ? "연습 · 느린 곡과 긴 이름의 프로필" : "Practice · slow songs and a long name", out _, out _);
      return new ProfileService(library, dialogs, new ProfileFileStore(), () => true, () => {}, () => {}, "1.2.0", (_, _) => {});
    }



    // New profile opens a small panel under its button; the profile starts from the initial settings.

    // An imported profile names a font this PC lacks: the card offers a font to
    // use instead, and a pick lands where the missing font was.

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
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiPalette.Background;
      var texture = new RenderTexture(640, 280, 24); camera.targetTexture = texture;
      Canvas canvas = root.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      var ui = new UiFactory(font);
      ui.Text("Input caret", root.transform, 28, 18, 300, 32, 20);
      string[] samples = { "Remove All", "한글 이름", "" };
      for (int i = 0; i < samples.Length; i++)
      {
        TMP_InputField input = ui.Input("Caret sample", samples[i], root.transform, 28, 64 + i * 64, 584, null, characterLimit: 40);
        CheckInputCaret(input, "visual caret sample", samples[i]);
        TMP_Text text = input.textComponent;
        UIVertex[] vertices = GenerateNativeCaret(input, text.textInfo.characterCount - 1);
        RectTransform rect = ui.Rect("Native TMP caret", input.textViewport, 0, 0, input.textViewport.rect.width, input.textViewport.rect.height);
        UiFactory.Stretch(rect, 0, 0, 0, 0);
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
      // A handle's edge stays apart from the color it holds, whatever that color is.
      foreach (Color held in new[] { Color.white, Color.black, Color.red, Color.yellow, Color.cyan, Color.blue, new Color(.5f, .5f, .5f), new Color(1, 1, 1, .3f) })
      {
        Color edge = UiPalette.HandleEdge(held);
        Color shown = Color.Lerp(new Color(.6f, .6f, .6f), new Color(held.r, held.g, held.b), held.a);
        float apart = Mathf.Abs(edge.grayscale - shown.grayscale) + Mathf.Abs(edge.r - shown.r) + Mathf.Abs(edge.b - shown.b);
        Assert(apart > .35f, "a handle's edge stands out from " + held + ": " + edge);
      }
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      var root = new GameObject("Color picker fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
      var events = new GameObject("Color picker events", typeof(EventSystem));
      var cameraRoot = new GameObject("Color picker camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.05f, .05f, .05f);
      var texture = new RenderTexture(512, 480, 24); camera.targetTexture = texture;
      Canvas canvas = root.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      Color current = new Color(.73f, .44f, .17f, .5f), original = current, theme = UiTheme.Accent;
      int changed = 0;
      var picker = new UiColorPicker(root.transform, font, () => true);
      picker.Show("Planet color", current, true, value => { current = value; changed++; });
      UiFactory.Place(picker.Root, 112, 52, UiColorPicker.Width, picker.Height);
      Settle();
      Transform drawn = picker.Root;
      var opacity = ColorPickerProbe.Opacity(drawn);
      var hex = ColorPickerProbe.Hex(drawn);
      var plane = ColorPickerProbe.Plane(drawn);
      var hue = ColorPickerProbe.Hue(drawn);
      var alpha = ColorPickerProbe.Alpha(drawn);
      var error = ColorPickerProbe.Error(drawn);
      Assert(changed == 0 && current == original, "opening an RGBA picker does not mutate its consumer");
      Assert(picker.Height == 358 && ColorPickerProbe.Shown(opacity) && ColorPickerProbe.Shown(alpha) && !ColorPickerProbe.Shown(ColorPickerProbe.Default(drawn)),
        "RGBA mode adds an opacity track and a numeric field beside HEX: " + picker.Height);
      Assert(ColorPickerProbe.Title(drawn).text == "Planet color" && ColorPickerProbe.Shown(ColorPickerProbe.Close(drawn))
        && hex.text == ColorUtility.ToHtmlStringRGB(original) && opacity.text == "50",
        "the card names the color, closes, and shows its HEX and opacity: " + hex.text + " / " + opacity.text);
      Assert(plane.GetComponent<UiCursor>().Kind == UiCursorKind.Crosshair
        && hue.GetComponent<UiCursor>().Kind == UiCursorKind.Horizontal
        && alpha.GetComponent<UiCursor>().Kind == UiCursorKind.Horizontal
        && hex.GetComponent<UiCursor>().Kind == UiCursorKind.Text,
        "color controls expose crosshair, adjustment and text cursor affordances");
      Rect card = ScreenRectOf(picker.Root), planeRect = ScreenRectOf((RectTransform)plane.transform), hueRect = ScreenRectOf((RectTransform)hue.transform),
        alphaRect = ScreenRectOf((RectTransform)alpha.transform), hexRect = ScreenRectOf((RectTransform)hex.transform),
        opacityRect = ScreenRectOf((RectTransform)opacity.transform);
      Assert(card.Contains(planeRect.min) && card.Contains(planeRect.max) && planeRect.center.y > hueRect.center.y && hueRect.center.y > alphaRect.center.y
        && alphaRect.center.y > hexRect.center.y && Mathf.Abs(hexRect.center.y - opacityRect.center.y) < .5f && opacityRect.xMin > hexRect.xMax
        && Mathf.Abs(hueRect.xMin - (planeRect.xMin - 10 * card.width / UiColorPicker.Width)) < 1,
        "top to bottom: the square, hue, opacity, then HEX with the opacity beside it; the bars' handles reach past the square");
      foreach (int percent in new[] { 0, 50, 100 })
      {
        opacity.text = percent.ToString();
        Assert(Mathf.Abs(current.a - percent / 100f) < .001f && picker.Commit(),
          "numeric opacity accepts and commits " + percent + "%");
        Settle();
        Assert(Mathf.Abs(alpha.value - percent / 100f) < .001f, "numeric opacity updates its slider");
      }
      opacity.text = "101";
      Settle();
      Assert(current.a == 1 && error.text == "", "out-of-range partial opacity leaves the last valid color untouched");
      int focusBefore = picker.Model.FocusOpacity.Value;
      Assert(!picker.Commit() && picker.Model.FocusOpacity.Value > focusBefore, "invalid opacity asks its field to take the keyboard on commit");
      Settle();
      Assert(error.text.Length > 0, "invalid opacity produces an inline correction on commit: " + error.text);
      opacity.text = "50";
      Assert(picker.Commit(), "valid opacity commits");
      Settle();
      Assert(error.text == "", "valid opacity clears its validation error");
      hex.text = "204060";
      Assert(Mathf.Abs(current.a - .5f) < .001f && ColorUtility.ToHtmlStringRGB(current) == "204060",
        "HEX changes preserve the independent alpha channel");
      alpha.value = .25f;
      Settle();
      Assert(opacity.text == "25" && Mathf.Abs(current.a - .25f) < .001f,
        "opacity dragging updates the numeric field continuously");
      plane.Changed(.8f, 0); hue.value = .77f; plane.Changed(.8f, .9f);
      Color.RGBToHSV(current, out float h, out float s, out float v);
      Assert(Mathf.Abs(h - .77f) < .01f && Mathf.Abs(current.a - .25f) < .001f,
        "HSV gestures preserve hue through black and retain opacity");
      Assert(UiTheme.Accent == theme, "planet color editing cannot change the shared overlay highlight");
      hex.text = "BA712B"; opacity.text = "50"; picker.Commit();
      Settle();
      CheckInputCaret(hex, "HEX color", "", "BA712B");
      CheckInputCaret(opacity, "opacity", "", "0", "50", "100");
      hex.DeactivateInputField(); opacity.DeactivateInputField();
      Settle();
      foreach (var label in picker.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "RGBA picker label fits: " + label.text);
      }
      SaveFrame(camera, texture, 512, 480, "rgba-color-picker");
      Press(ColorPickerProbe.Close(drawn));
      Assert(!picker.Visible, "the close button hides the picker");
      picker.SetDefault(new Color(.2f, .4f, .6f));
      picker.Show("Highlight color", current, false, value => current = value);
      Settle();
      Assert(picker.Height == 318 && !ColorPickerProbe.Shown(ColorPickerProbe.Alpha(drawn)) && !ColorPickerProbe.Shown(ColorPickerProbe.Opacity(drawn))
        && ColorPickerProbe.Shown(ColorPickerProbe.Default(drawn)),
        "RGB mode stays compact, hides both opacity controls and offers Default: " + picker.Height);
      hex.text = "BA712C";
      Assert(current.a == 1, "RGB mode always emits an opaque highlight color");
      Press(ColorPickerProbe.Default(drawn));
      Assert(ColorUtility.ToHtmlStringRGB(current) == "336699", "Default puts the default color back: " + ColorUtility.ToHtmlStringRGB(current));
      picker.Hide();
      Assert(!picker.Visible && !picker.IsTyping && picker.Commit(), "hidden picker releases its editing state");
      picker.Dispose();
      RenderTexture.active = null; camera.targetTexture = null;
      Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(root); Object.DestroyImmediate(events);
    }

    private static void CheckSpacingGuides()
    {
      var root = new GameObject("Spacing fixture", typeof(RectTransform), typeof(CanvasSpacingGraphic));
      var graphic = root.GetComponent<CanvasSpacingGraphic>();
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



    // Leaves the preset editor on its Shortcut tab for the screenshot: a set
    // shortcut, or on narrow windows a recording that hit another preset's.

    private static void RenderNotice(int width, int height, SystemLanguage language, string name)
    {
      RDString.language = language;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      UiTheme.SetHighlight(new ElementSettings().HighlightColor);
      var notice = new UiToast(font, () => true);
      Assert(notice.Root == null, "the notice builds nothing until it is first shown");
      bool korean = language == SystemLanguage.Korean;
      notice.Show((korean ? "이펙트 프리셋: " : "Effect preset: ") + "Remove All",
        korean ? "레벨을 시작하거나 재시작하면 반영됩니다." : "Takes effect when a level starts or restarts.");
      notice.Resize(width, height);
      GameObject root = notice.Root;
      Canvas canvas = root.GetComponent<Canvas>();
      Assert(root.activeSelf && canvas.sortingOrder < 32760, "the notice shows below the settings window");
      Assert(root.GetComponent<GraphicRaycaster>() == null, "the notice never takes clicks from the game");
      GameObject cameraRoot = new GameObject("Notice preview camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .055f);
      RenderTexture target = new RenderTexture(width, height, 24);
      camera.targetTexture = target;
      canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      Canvas.ForceUpdateCanvases();
      RectTransform card = (RectTransform)root.transform.GetChild(0);
      foreach (TextMeshProUGUI text in card.GetComponentsInChildren<TextMeshProUGUI>())
      {
        text.ForceMeshUpdate();
        Assert(width < 400 || !text.isTextTruncated, "the notice text fits: " + text.text);
      }
      Assert(card.rect.width <= width / canvas.scaleFactor, "the notice fits the screen");
      camera.Render();
      RenderTexture.active = target;
      Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      RenderTexture.active = null;
      Object.DestroyImmediate(image); camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(cameraRoot);
      notice.Dispose();
      Assert(notice.Root == null, "closing the notice removes it");
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
