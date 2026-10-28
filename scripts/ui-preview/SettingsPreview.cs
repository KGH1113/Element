using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ImplResourcePack.Configuration;
using ImplResourcePack.Configuration.Effects;
using ImplResourcePack.Presentation.Settings;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Layout;
using ImplResourcePack.Domain.Input;
using ImplResourcePack.Domain.Layout;
using ImplResourcePack.Application.Updates;
using ImplResourcePack.Infrastructure.Game.DmNote;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Editor
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
      RenderLayout(1280, 720, SystemLanguage.Korean, "layout-ko-720");
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
      var host = new OverlayCanvasHost();
      var settings = new ImplResourcePackSettings { ReduceUiMotion = true };
      OverlayTheme.SetHighlight(settings.HighlightColor);
      var target = new PreviewTarget(new Rect(96, 80, 280, 44), language == SystemLanguage.Korean ? "진행률" : "Progress", font);
      host.LayoutSettings.Add(target.Settings); host.LayoutTargets.Add(target);
      GameObject gallerySources = CreateGalleryTemplates(host, font);
      var external = new ExternalPreviewTarget(new Rect(width - 264, 180, 208, 130));
      var footTemplate = new ExternalPreviewTarget(new Rect(0, 0, 210, 270), "foot");
      host.LayoutTargets.Add(external);
      host.LayoutSettings.Add(external.Settings);
      int instanceNumber = 0;
      host.AddFactory = templateId =>
      {
        var copy = new PreviewTarget(new Rect(width * .5f - 140, height * .5f - 22, 280, 44), target.Label, font, "copy-" + ++instanceNumber);
        host.LayoutTargets.Add(copy); host.LayoutSettings.Add(copy.Settings);
        return copy;
      };
      var editor = new OverlayLayoutEditor(host, null, font, font.material, _ => {}, settings, null, new[] { external, footTemplate });
      var eventsRoot = new GameObject("Fixture events", typeof(EventSystem));
      editor.Open();
      editor.GetType().GetField("_selected", Private).SetValue(editor, 0);
      GameObject root = Field<GameObject>(editor, "_root"); root.SetActive(true);
      var cameraRoot = new GameObject("Layout preview camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .05f, .07f);
      var texture = new RenderTexture(width, height, 24); camera.targetTexture = texture;
      Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
      editor.Resize(width, height); Canvas.ForceUpdateCanvases();
      Rect toolbar = editor.ToolbarBounds;
      Assert(toolbar.xMin >= 0 && toolbar.xMax <= width && toolbar.yMax <= height, "layout toolbar fits viewport");
      Assert(toolbar.width <= 640.1f && toolbar.height <= 120.1f, "overlay toolbar stays compact with element actions");
      Assert(root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 1, "only the selected target has a Unity outline");
      editor.GetType().GetField("_selected", Private).SetValue(editor, 1);
      Call(editor, "UpdateFrames");
      Assert(external.Selected && root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 0,
        "native target owns its outline without a duplicate Unity outline");
      editor.GetType().GetField("_selected", Private).SetValue(editor, -1);
      Call(editor, "UpdateFrames");
      Assert(!external.Selected && root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Length == 0,
        "deselection removes all outlines");
      RectTransform toolbarRect = Field<RectTransform>(editor, "_toolbar");
      RectTransform grip = (RectTransform)toolbarRect.Find("Drag panel");
      Assert(Mathf.Abs(CenterIn(grip, toolbarRect).y - CenterIn(Field<TextMeshProUGUI>(editor, "_instruction").rectTransform, toolbarRect).y) < .1f,
        "move grip and title share the same vertical center");
      external.PointerOffset = new Vector2(0, 28);
      editor.GetType().GetField("_selected", Private).SetValue(editor, 1);
      foreach (LayoutHandle edge in new[] { LayoutHandle.Top, LayoutHandle.Bottom })
      {
        Vector2 edgePoint = new Vector2(external.Bounds.center.x, edge == LayoutHandle.Top ? external.Bounds.yMin : external.Bounds.yMax);
        object[] hitArgs = { edgePoint - external.PointerOffset, LayoutHandle.None };
        int hit = (int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, hitArgs);
        Assert(hit == 1 && (LayoutHandle)hitArgs[1] == edge, "native top/bottom hit uses the same coordinates as its outline");
      }
      editor.GetType().GetField("_selected", Private).SetValue(editor, -1);
      object[] firstHit = { new Vector2(external.Bounds.center.x, external.Bounds.yMin + 3) - external.PointerOffset, LayoutHandle.None };
      Assert((int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, firstHit) == -1,
        "unselected native element excludes the empty note viewport");
      object[] contentHit = { external.SelectionBounds.center - external.PointerOffset, LayoutHandle.None };
      Assert((int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, contentHit) == 1
        && (LayoutHandle)contentHit[1] == LayoutHandle.Move, "unselected native element selects only current content");
      external.PointerOffset = Vector2.zero;
      editor.GetType().GetField("_selected", Private).SetValue(editor, 0);
      Call(editor, "UpdateFrames");
      Button plus = Field<Button>(editor, "_plus"), minus = Field<Button>(editor, "_minus");
      TMP_InputField sizeInput = Field<TMP_InputField>(editor, "_scaleInput");
      Assert(Mathf.Abs(CenterIn((RectTransform)plus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f
        && Mathf.Abs(CenterIn((RectTransform)minus.transform, toolbarRect).y - CenterIn((RectTransform)sizeInput.transform, toolbarRect).y) < .1f,
        "plus, minus and percentage field share one vertical center");
      foreach (string value in new[] { "20", "100", "500", "99.9" })
      {
        sizeInput.SetTextWithoutNotify(value); Call(editor, "AlignScaleValue"); Canvas.ForceUpdateCanvases();
        TextMeshProUGUI suffix = Field<TextMeshProUGUI>(editor, "_scaleSuffix");
        sizeInput.textComponent.ForceMeshUpdate(); suffix.ForceMeshUpdate();
        RectTransform field = (RectTransform)sizeInput.transform;
        Vector3 left = field.InverseTransformPoint(sizeInput.textComponent.transform.TransformPoint(sizeInput.textComponent.textBounds.min));
        Vector3 right = field.InverseTransformPoint(suffix.transform.TransformPoint(suffix.textBounds.max));
        Assert(Mathf.Abs((left.x + right.x) * .5f - field.rect.center.x) < 2, "percentage text group stays horizontally centered: " + value);
      }
      CheckInputCaret(sizeInput, "overlay scale", "", "100", "99.9");
      Assert(((RectTransform)plus.transform).anchoredPosition.x < ((RectTransform)sizeInput.transform).anchoredPosition.x
        && ((RectTransform)sizeInput.transform).anchoredPosition.x < ((RectTransform)minus.transform).anchoredPosition.x,
        "size controls are ordered plus, percent, minus");
      Assert(root.GetComponentsInChildren<TextMeshProUGUI>(false).All(label => label.text != "Size %" && label.text != "크기 %"),
        "size caption is absent");
      foreach (LayoutActionIcon icon in root.GetComponentsInChildren<LayoutActionIcon>(false))
      {
        using (var mesh = new VertexHelper())
        {
          typeof(LayoutActionIcon).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(icon, new object[] { mesh });
          Assert(mesh.currentVertCount > 0, "vector icon renders: " + icon.Kind);
        }
      }
      Assert(Field<Button>(editor, "_save").colors.normalColor.maxColorComponent < .5f,
        "save uses a dark tinted fill");
      Assert(Field<Button>(editor, "_delete").interactable && Field<Button>(editor, "_add").interactable,
        "selected element exposes delete and add actions");
      Call(editor, "CommitScale", "150");
      Assert(Mathf.Approximately(target.Settings.Scale, 1.5f), "precise layout scale commits");
      Call(editor, "Undo"); Assert(Mathf.Approximately(target.Settings.Scale, 1), "layout undo restores settings");
      Call(editor, "Redo"); Assert(Mathf.Approximately(target.Settings.Scale, 1.5f), "layout redo restores edit");
      Call(editor, "CommitScale", "999"); Call(editor, "UpdateToolbar");
      Assert(Field<TextMeshProUGUI>(editor, "_keyboardHelp").text.Contains("500"), "layout validation stays visible");
      Call(editor, "ResetSelected"); Call(editor, "Undo");
      Assert(Mathf.Approximately(target.Settings.Scale, 1.5f), "element reset is undoable");
      var beforeResize = new LayoutSnapshot(host.LayoutSettings);
      Rect beforeDrag = target.Bounds;
      target.SetBounds(new Rect(beforeDrag.x + 80, beforeDrag.y + 40, beforeDrag.width, beforeDrag.height));
      editor.GetType().GetField("_dragging", Private).SetValue(editor, 0);
      editor.Resize(width + 100, height + 40);
      Assert(Field<int>(editor, "_dragging") == -1 && beforeResize.Matches(new LayoutSnapshot(host.LayoutSettings)),
        "viewport change cancels the staged drag without selecting an anchor or changing persistent placement");
      Assert(target.Bounds == beforeDrag, "viewport change discards staged pixel movement");
      editor.GetType().GetField("_scaleErrorUntil", Private).SetValue(editor, 0f);
      editor.Resize(width, height); Canvas.ForceUpdateCanvases();
      foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(label.font == font, "layout uses overlay font");
        if (label.transform.IsChildOf(Field<RectTransform>(editor, "_toolbar")))
          Assert(!label.isTextTruncated, "layout label fits: " + label.text);
      }
      // Drag the toolbar over the selected outline while preserving the exact
      // pointer offset. Validate the generated mesh, not just sibling order.
      Vector2 grab = toolbar.position + new Vector2(20, 18);
      Call(editor, "BeginToolbarDrag", grab);
      Call(editor, "MoveToolbar", grab);
      Assert(editor.ToolbarBounds.position == toolbar.position, "panel does not jump on pointer down");
      Vector2 moved = new Vector2(110, 90);
      Call(editor, "MoveToolbar", moved + new Vector2(20, 18));
      Call(editor, "UpdateFrames"); Canvas.ForceUpdateCanvases();
      Assert(editor.ToolbarBounds.position == moved, "panel follows drag one to one");
      LayoutOutlineGraphic outline = root.GetComponentsInChildren<LayoutOutlineGraphic>(false).Single();
      using (var mesh = new VertexHelper())
      {
        typeof(LayoutOutlineGraphic).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(outline, new object[] { mesh });
        UIVertex vertex = default;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
          mesh.PopulateUIVertex(ref vertex, i);
          Vector2 point = new Vector2(target.Bounds.x + vertex.position.x, target.Bounds.y - vertex.position.y);
          Assert(!editor.ToolbarBounds.Contains(point), "outline mesh excludes the dragged panel");
        }
      }
      Assert(!outline.GetComponentsInChildren<TextMeshProUGUI>(true).Any(), "selected outline has no floating name label");
      Call(editor, "MoveToolbar", new Vector2(-1000, -1000));
      Assert(editor.ToolbarBounds.position == Vector2.zero, "panel remains reachable at screen edges");
      Call(editor, "MoveToolbar", moved + new Vector2(20, 18));
      Call(editor, "UpdateFrames"); Canvas.ForceUpdateCanvases();
      camera.Render(); RenderTexture.active = texture;
      var image = new Texture2D(width, height, TextureFormat.RGB24, false);
      image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
      File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
      CheckElementEditing(editor, host, target, settings, camera, texture, width, height, name);
      RenderTexture.active = null; camera.targetTexture = null;
      Object.DestroyImmediate(image); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot);
      OverlayColorPopover popover = Field<OverlayColorPopover>(editor, "_colorPopover");
      if (popover != null)
      {
        SettingsUiFactory pickerUi = Field<SettingsUiFactory>(Field<SettingsColorPicker>(popover, "_picker"), "_ui");
        Object.DestroyImmediate(Field<Sprite>(pickerUi, "_rounded")); Object.DestroyImmediate(Field<Texture2D>(pickerUi, "_texture"));
      }
      root.SetActive(false); Object.DestroyImmediate(root);
      Object.DestroyImmediate(eventsRoot);
      Object.DestroyImmediate(gallerySources);
      SettingsUiFactory ui = Field<SettingsUiFactory>(editor, "_ui");
      Object.DestroyImmediate(Field<Sprite>(ui, "_rounded")); Object.DestroyImmediate(Field<Texture2D>(ui, "_texture"));
    }

    private static void CheckElementEditing(OverlayLayoutEditor editor, OverlayCanvasHost host, PreviewTarget original,
      ImplResourcePackSettings settings, Camera camera, RenderTexture texture, int width, int height, string name)
    {
      Call(editor, "ToggleColor");
      OverlayColorPopover picker = Field<OverlayColorPopover>(editor, "_colorPopover");
      SettingsColorPicker controls = Field<SettingsColorPicker>(picker, "_picker");
      Assert(picker.Visible, "Edit Color opens its anchored popover");
      TMP_InputField hex = Field<TMP_InputField>(controls, "_hex");
      string before = settings.HighlightColor;
      hex.text = "#12";
      Assert(settings.HighlightColor == before && Field<TextMeshProUGUI>(controls, "_error").text == "", "incomplete color preserves theme without a premature error");
      Assert(!picker.Commit() && Field<TextMeshProUGUI>(controls, "_error").text.Length > 0, "invalid color cannot commit and shows an inline error");
      hex.text = "#204060";
      Assert(picker.Commit() && settings.HighlightColor == "#204060", "valid HEX color commits as RGB");
      SettingsColorPlane plane = Field<SettingsColorPlane>(controls, "_plane");
      Slider hue = Field<Slider>(controls, "_hue");
      plane.Changed(.8f, 0);
      hue.value = .77f;
      plane.Changed(.8f, .9f);
      Color.RGBToHSV(OverlayTheme.Accent, out float h, out float s, out float v);
      Assert(Mathf.Abs(h - .77f) < .01f && Mathf.Abs(s - .8f) < .01f && Mathf.Abs(v - .9f) < .01f,
        "HSV preserves selected hue through black without RGB round-trip jumps");
      hex.text = "#BA712B"; picker.Commit();
      Canvas.ForceUpdateCanvases();
      foreach (Button button in picker.Root.GetComponentsInChildren<Button>(false))
        foreach (TextMeshProUGUI label in button.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          label.ForceMeshUpdate();
          Assert(label.textInfo.lineCount <= 1 && !label.isTextTruncated, "color popover button stays on one line: " + label.text);
        }
      SaveFrame(camera, texture, width, height, name + "-color");
      picker.Hide();

      Call(editor, "OpenGallery");
      GameObject gallery = Field<GameObject>(editor, "_galleryRoot");
      Assert(gallery != null && gallery.activeSelf, "Add Element opens modal gallery");
      Assert(editor.NativeExclusionBounds == new Rect(0, 0, width, height), "gallery excludes native surfaces across the viewport");
      var samples = gallery.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.text.Contains("0%-48%")).ToArray();
      Assert(samples.Length == 1 && samples[0].color == OverlayTheme.ProgressColor(.48f), "gallery renders real progress composition with mock data and theme");
      Assert(host.GalleryTargets.Count == 11 && gallery.GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("Add ")) == 13,
        "gallery offers all eleven overlay templates and both native viewer kinds");
      Assert(gallery.GetComponentsInChildren<NativeViewerMock>(true).Length == 2, "gallery renders production hand and foot mock components");
      Assert(gallery.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text == "Tab")
        && gallery.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text == "Left"), "gallery includes real key labels for hand and foot");
      Canvas.ForceUpdateCanvases();
      Assert(gallery.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition > .99f, "gallery starts at the first row");
      SaveFrame(camera, texture, width, height, name + "-gallery");
      ScrollRect galleryScroll = gallery.GetComponentInChildren<ScrollRect>();
      galleryScroll.verticalNormalizedPosition = 0;
      Canvas.ForceUpdateCanvases();
      SaveFrame(camera, texture, width, height, name + "-gallery-viewers");
      Button tile = gallery.GetComponentsInChildren<Button>().Single(b => b.name == "Add progress");
      tile.onClick.Invoke();
      Assert(Field<GameObject>(editor, "_galleryRoot") == null, "choosing a gallery element closes the modal");
      IOverlayLayoutTarget first = host.LayoutTargets.Last();
      Assert(first.Id != original.Id && first.TemplateId == original.TemplateId && first.Enabled, "adding existing template creates an independent enabled instance");
      Call(editor, "CommitScale", "200");
      Assert(Mathf.Approximately(first.EffectiveScale, 2) && Mathf.Approximately(original.EffectiveScale, 1.5f), "duplicate scale does not change original");
      Call(editor, "DeleteSelected");
      Assert(!first.Enabled && original.Enabled, "delete removes only selected instance");
      Call(editor, "Undo"); Assert(first.Enabled, "undo restores deleted instance");
      Call(editor, "Redo"); Assert(!first.Enabled, "redo deletes the same instance");
      Call(editor, "Undo");
      Call(editor, "AddElement", original);
      IOverlayLayoutTarget second = host.LayoutTargets.Last();
      Assert(second.Id != first.Id && first.Enabled && second.Enabled, "same template can be added repeatedly");
      first.SetBounds(second.Bounds);
      int firstIndex = host.LayoutTargets.IndexOf(first), secondIndex = host.LayoutTargets.IndexOf(second);
      editor.GetType().GetField("_selected", Private).SetValue(editor, firstIndex);
      object[] selectedHit = { second.SelectionBounds.center, LayoutHandle.None };
      Assert((int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, selectedHit) == firstIndex,
        "selected maximum editing area retains priority over overlapping duplicate content");
      editor.GetType().GetField("_selected", Private).SetValue(editor, -1);
      object[] duplicateHit = { second.SelectionBounds.center, LayoutHandle.None };
      Assert((int)editor.GetType().GetMethod("FindTarget", Private).Invoke(editor, duplicateHit) == secondIndex,
        "equal overlapping unselected elements select the most recently added instance");
      editor.GetType().GetField("_selected", Private).SetValue(editor, 0);
      Call(editor, "DeleteSelected");
      Assert(!original.Enabled && first.Enabled && second.Enabled, "deleting original leaves copies enabled");
      var beforeReset = new LayoutSnapshot(host.LayoutSettings);
      Call(editor, "OpenResetConfirmation");
      Assert(Field<bool>(editor, "_resetConfirmation") && editor.NativeExclusionBounds == new Rect(0, 0, width, height),
        "factory reset opens a modal blocking overlay interaction");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "opening reset confirmation never changes settings");
      Call(editor, "CloseGallery");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "canceling reset confirmation preserves all settings");
      Call(editor, "OpenResetConfirmation");
      Call(editor, "RestoreDefaultOverlays");
      Assert(!first.Enabled && !second.Enabled && Field<GameObject>(editor, "_galleryRoot") == null,
        "confirming reset removes added instances and closes the modal");
      Call(editor, "Undo");
      Assert(beforeReset.Matches(new LayoutSnapshot(host.LayoutSettings)), "factory reset is one undoable editor action");
      Call(editor, "ToggleColor");
      hex.text = "#336699"; picker.Commit(); picker.Hide();
      editor.Close(false);
      Assert(original.Enabled && Mathf.Approximately(original.EffectiveScale, 1) && !first.Enabled && !second.Enabled,
        "cancel restores opening elements and discards all added instances");
      Assert(settings.HighlightColor == "#BA712B" && !editor.IsOpen, "cancel restores opening highlight and exits editor");
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
    private static GameObject CreateGalleryTemplates(OverlayCanvasHost host, TMP_FontAsset font)
    {
      // Mirror the authored text geometry from the production views; the actual
      // target generates every gallery sample, including its theme calculation.
      var sources = new GameObject("Gallery source templates", typeof(RectTransform));
      AddGalleryTemplate(host, sources.transform, font, "progress", "진행도", "진행도 | 0%-48%", new Vector2(456, 30), 25, TextAlignmentOptions.TopLeft);
      AddGalleryTemplate(host, sources.transform, font, "accuracy", "정확도", "정확도 | 100%", new Vector2(456, 30), 25, TextAlignmentOptions.TopLeft);
      AddGalleryTemplate(host, sources.transform, font, "time", "음악·맵 시간", "음악 시간 | 1:27~3:48", new Vector2(456, 30), 25, TextAlignmentOptions.TopLeft);
      AddGalleryTemplate(host, sources.transform, font, "fps", "FPS", "FPS | 120", new Vector2(456, 30), 25, TextAlignmentOptions.TopLeft);
      AddGalleryTemplate(host, sources.transform, font, "tile-bpm", "타일 BPM", "TBPM | 180", new Vector2(456, 30), 25, TextAlignmentOptions.TopRight);
      AddGalleryTemplate(host, sources.transform, font, "current-bpm", "현재 BPM", "CBPM | 180", new Vector2(456, 30), 25, TextAlignmentOptions.TopRight);
      AddGalleryTemplate(host, sources.transform, font, "keys-per-second", "초당 입력 수", "KPS | 6", new Vector2(456, 30), 25, TextAlignmentOptions.TopRight);
      AddGalleryTemplate(host, sources.transform, font, "song-title", "곡 제목", "A Dance of Fire and Ice", new Vector2(600, 32), 24, TextAlignmentOptions.Center);
      GameObject combo = OverlayTextFactory.CreateRect("Combo", sources.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 200));
      GameObject title = OverlayTextFactory.CreateRect("ComboTitle", combo.transform, new Vector2(.5f, .45f), new Vector2(.5f, 0), Vector2.zero, Vector2.zero);
      TextMeshProUGUI titleText = OverlayTextFactory.AddText(title, font, font.material, 40, TextAlignmentOptions.Center);
      titleText.text = "Perfect"; titleText.color = Color.white;
      var titleFit = title.AddComponent<ContentSizeFitter>();
      titleFit.horizontalFit = titleFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      GameObject value = OverlayTextFactory.CreateRect("ComboValue", combo.transform, new Vector2(.5f, .45f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
      TextMeshProUGUI valueText = OverlayTextFactory.AddText(value, font, font.material, 108, TextAlignmentOptions.Top);
      valueText.text = "120";
      // ComboAnimationDriver.Initialize applies this production resting pose
      // before RegisterLayout captures the template's sample geometry.
      valueText.fontSize = 78;
      ((RectTransform)title.transform).anchoredPosition = new Vector2(0, 43.505f);
      var valueFit = value.AddComponent<ContentSizeFitter>();
      valueFit.horizontalFit = valueFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      host.GalleryTargets.Add(new RectTransformLayoutTarget("combo", "콤보", (RectTransform)combo.transform, sources.transform, valueText, "120"));
      AddGalleryTemplate(host, sources.transform, font, "judgement", "판정", "0  0  0  2  4  120  4  2  0  0  0", new Vector2(1000, 30), 25, TextAlignmentOptions.Bottom);
      AddGalleryTemplate(host, sources.transform, font, "timing-scale", "타이밍 배율", "Timing Scale - 100%", new Vector2(300, 30), 20, TextAlignmentOptions.Bottom);
      sources.SetActive(false);
      return sources;
    }

    private static void AddGalleryTemplate(OverlayCanvasHost host, Transform parent, TMP_FontAsset font,
      string id, string label, string sample, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
      GameObject source = OverlayTextFactory.CreateRect(id, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
      TextMeshProUGUI text = OverlayTextFactory.AddText(source, font, font.material, fontSize, alignment);
      text.text = sample;
      if (id == "song-title")
      {
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 24;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
      }
      host.GalleryTargets.Add(new RectTransformLayoutTarget(id, label, (RectTransform)source.transform, parent, text, sample));
    }
    private sealed class ExternalPreviewTarget : IOverlayLayoutTarget, IExternalLayoutOutline, IOverlayLayoutPointer, IOverlayGalleryPreview
    {
      private readonly string _kind;
      public ExternalPreviewTarget(Rect bounds, string kind = "hand")
      { Bounds = bounds; _kind = kind; Settings = new OverlayLayoutSettings { Id = "native-preview-" + kind, TemplateId = "native-" + kind }; }
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
      public Vector2 PointerOffset;
      public Vector2 PointerInLayoutSpace(Vector2 pointer) => pointer + PointerOffset;
      public void SetSelected(bool selected) { Selected = selected; }
      public void SetEnabled(bool enabled) { Settings.Enabled = enabled; }
      public void SetBounds(Rect bounds) { Bounds = bounds; }
      public void CommitLayout(bool reselectAnchor, bool initialize = false) { }
      public void SetPreview(bool preview) { }
      public void RefreshLayout() { }
      public void ResetLayout() { }
      public GameObject CreateGalleryPreview(Transform parent)
      {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
        return NativeViewerMock.Create(_kind, parent, font, false).gameObject;
      }
    }
    private sealed class PreviewTarget : IOverlayLayoutTarget, IOverlayGalleryPreview
    {
      private readonly Rect _original;
      private readonly TMP_FontAsset _font;
      public readonly OverlayLayoutSettings Settings;
      public PreviewTarget(Rect bounds, string label, TMP_FontAsset font, string id = "preview")
      { _original = bounds; Bounds = bounds; Label = label; _font = font; Settings = new OverlayLayoutSettings { Id = id, TemplateId = "progress", Scale = 1 }; }
      public string Id => Settings.Id;
      public string TemplateId => Settings.TemplateId;
      public string Label { get; }
      public Rect Bounds { get; private set; }
      public Rect SelectionBounds => new Rect(Bounds.x + 16, Bounds.y + 8, Bounds.width * .6f, Mathf.Max(1, Bounds.height - 16));
      public bool Available => true;
      public bool Enabled => Settings.Enabled;
      private bool _pending;
      public float EffectiveScale => _pending ? Bounds.width / _original.width : Settings.Scale;
      public void SetBounds(Rect bounds) { if (Bounds == bounds) return; Bounds = bounds; _pending = true; }
      public void CommitLayout(bool reselectAnchor, bool initialize = false)
      {
        if (!_pending && !initialize) return;
        var position = OverlayLayoutPosition.Capture(new LayoutBounds(Bounds.x, Bounds.y, Bounds.width, Bounds.height), Screen.width, Screen.height,
          !reselectAnchor && OverlayLayoutPosition.IsAnchored(Settings) ? (Anchor9?)Settings.Anchor : null);
        Settings.Scale = EffectiveScale;
        Settings.Customized = true;
        Settings.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
        Settings.Anchor = position.Anchor; Settings.OffsetX = position.OffsetX; Settings.OffsetY = position.OffsetY;
        _pending = false;
      }
      public void SetEnabled(bool enabled) { Settings.Enabled = enabled; }
      public void SetPreview(bool value) { }
      public void RefreshLayout() { _pending = false; Bounds = new Rect(_original.position, _original.size * Settings.Scale); }
      public void ResetLayout() { Settings.Scale = 1; Settings.Customized = false; Settings.LayoutVersion = 0; Settings.Anchor = Anchor9.Center; Settings.OffsetX = Settings.OffsetY = 0; RefreshLayout(); }
      public GameObject CreateGalleryPreview(Transform parent)
      {
        GameObject source = OverlayTextFactory.CreateRect("Gallery source fixture", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, _original.size);
        TextMeshProUGUI text = OverlayTextFactory.AddText(source, _font, _font.material, 24, TextAlignmentOptions.MidlineLeft);
        text.text = "0%-48%";
        var real = new RectTransformLayoutTarget(TemplateId, Label, (RectTransform)source.transform, parent, text, "0%-48%");
        GameObject sample = real.CreateGalleryPreview(parent);
        Object.DestroyImmediate(source);
        return sample;
      }
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
      var fresh = new ImplResourcePackSettings();
      Assert(fresh.HighlightColor == "#BA712B" && OverlayTheme.DefaultHighlight == "#BA712B", "new settings and theme use brown default");
      var legacy = new ImplResourcePackSettings { HighlightColor = " #7cce00 " };
      legacy.Migrate();
      Assert(legacy.HighlightColor == "#BA712B" && legacy.SettingsVersion >= 1, "old default migrates once");
      legacy.HighlightColor = "#7CCE00"; legacy.Migrate();
      Assert(legacy.HighlightColor == "#7CCE00", "explicit green after migration is preserved");
      var custom = new ImplResourcePackSettings { HighlightColor = "#336699" };
      custom.Migrate(); Assert(custom.HighlightColor == "#336699", "migration preserves custom color");
      foreach (bool hidden in new[] { false, true })
      foreach (int version in new[] { 0, 1 })
      {
        var old = new ImplResourcePackSettings { SettingsVersion = version, HidePerfectJudgmentText = hidden };
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
        var grouped = new ImplResourcePackSettings { SettingsVersion = 2 };
        grouped.Gameplay.Judgement.HidePerfect = hidden;
        grouped.Migrate();
        Assert(grouped.Gameplay.Judgement.HidePerfect == hidden && grouped.Gameplay.Judgement.HideXPerfect == hidden,
          "existing gameplay Perfect group migrates its XP visibility without consulting the obsolete outer switch");
      }
    }

    private static void CheckChatterBlocking(SettingsPanel panel, ImplResourcePackSettings settings, int width)
    {
      ChatterBlockerOptions chatter = settings.Gameplay.Chatter;
      Slider window = Field<Slider>(panel, "_chatterWindow");
      Button toggle = Field<Button>(panel, "_chatterSwitch");
      Assert(!chatter.Enabled && !window.interactable, "the chatter window waits until blocking is on");
      Assert(window.minValue == 5 && window.maxValue == 150 && window.wholeNumbers, "the window is whole milliseconds from 5 to 150");
      Assert(window.value == chatter.WindowMs, "the slider starts at the saved window");
      toggle.onClick.Invoke();
      Assert(chatter.Enabled && window.interactable, "turning blocking on enables the window");
      window.value = 33;
      Assert(chatter.WindowMs == 33 && Field<TextMeshProUGUI>(panel, "_chatterWindowValue").text == "33 ms", "the slider saves and shows the window");
      toggle.onClick.Invoke();
      window.SetValueWithoutNotify(chatter.WindowMs = ChatterFilter.DefaultWindowMs);
      Assert(!chatter.Enabled, "turning it off again restores the default state");
      RectTransform row = Field<RectTransform>(panel, "_chatterRow");
      Assert(row.rect.width <= width, "the window row fits the page");
    }

    private static void Render(int width, int height, SystemLanguage language, int page, string name)
    {
      RDString.language = language;
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      Assert(font != null, "overlay font exists");
      ImplResourcePackSettings settings = new ImplResourcePackSettings { ReduceUiMotion = true };
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
          "General contains shortcut, motion, ImplDmNote auto open and automatic update preferences");
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

    private static void CheckPresetEditing(SettingsPanel panel, ImplResourcePackSettings settings, int width, int height)
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
      save.onClick.Invoke();
      EffectPreset created = settings.Effects.Presets.Single(preset => preset.Name == "Fixture calm");
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

    private static void CheckEditorSections(SettingsPanel panel, ImplResourcePackSettings settings, int width, int height)
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

    private static void CheckJudgmentChoices(SettingsPanel panel, ImplResourcePackSettings settings)
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

    private static void CheckPlanetEditing(SettingsPanel panel, ImplResourcePackSettings settings, int width, int height, PlanetPreviewProbe preview)
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

    private static void CheckNavigationControls(SettingsPanel panel, ImplResourcePackSettings settings)
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

    private static void CheckShortcut(SettingsPanel panel, ImplResourcePackSettings settings)
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
