using System;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Layout;
using ImplResourcePack.Presentation.Layout;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Editor
{
  internal static class OverlayTargetChecks
  {
    public static int Run(TMP_FontAsset font, Material material)
    {
      int checks = 0;
      Action<bool, string> check = (condition, message) =>
      {
        if (!condition)
          throw new InvalidOperationException("Overlay target: " + message);
        checks++;
      };
      GameObject canvasRoot = new GameObject("Overlay target checks", typeof(RectTransform), typeof(Canvas));
      canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      TMP_FontAsset alternateFont = null;
      Material alternateMaterial = null;
      try
      {
        GameObject sourceRoot = OverlayTextFactory.CreateRect(
          "FPS source", canvasRoot.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
          Vector2.zero, new Vector2(500, 50));
        TextMeshProUGUI source = OverlayTextFactory.AddText(sourceRoot, font, material, 25, TextAlignmentOptions.MidlineLeft);
        source.text = "FPS | 120";
        source.color = OverlayTheme.Accent;
        RectTransformLayoutTarget original = new RectTransformLayoutTarget(
          "fps", "FPS", source.rectTransform, canvasRoot.transform, source, "FPS | 120");
        OverlayLayoutSettings originalSettings = new OverlayLayoutSettings { Id = "fps" };
        original.Configure(originalSettings);
        Canvas.ForceUpdateCanvases();
        source.ForceMeshUpdate();
        Rect originalBounds = original.Bounds;
        Rect glyphBounds = original.SelectionBounds;
        check(glyphBounds.width > 0 && glyphBounds.width < originalBounds.width * .8f,
          "initial hit bounds follow the visible text instead of the maximum edit rectangle");
        check(glyphBounds.height > 0 && glyphBounds.height < originalBounds.height,
          "initial hit bounds follow glyph height");
        source.text = "FPS | 123456789";
        check(original.SelectionBounds.width > glyphBounds.width,
          "changing the rendered text updates hit bounds immediately");

        original.SetBounds(new Rect(originalBounds.x, originalBounds.y, originalBounds.width * 2, originalBounds.height * 2));
        original.CommitLayout(false);
        Rect enlargedOriginal = original.Bounds;
        RectTransformLayoutTarget copy = original.CreateInstance("fps:copy", canvasRoot.transform);
        OverlayLayoutSettings copySettings = new OverlayLayoutSettings { Id = "fps:copy", TemplateId = "fps" };
        copy.Configure(copySettings);
        RectTransform copyRoot = (RectTransform)canvasRoot.transform.Find("Overlay instance: fps:copy");
        TextMeshProUGUI copyText = copyRoot.GetComponent<TextMeshProUGUI>();
        Rect newCopyBounds = copy.Bounds;
        copy.CommitLayout(true, true);
        copyText.ForceMeshUpdate();
        check(Vector2.Distance(copy.Bounds.position, newCopyBounds.position) < .1f,
          "a new copy seeds template content so initialization before its first render cannot jump");
        check(copy.TemplateId == "fps" && copy.Id != original.Id, "copies keep a stable template and independent identity");
        check(Mathf.Abs(copy.EffectiveScale - 1) < .001f && Mathf.Abs(copy.Bounds.width - originalBounds.width) < .1f,
          "new copies use the intrinsic default size even when the original is enlarged");
        copy.SetBounds(new Rect(copy.Bounds.x, copy.Bounds.y, copy.Bounds.width * .8f, copy.Bounds.height * .8f));
        copy.CommitLayout(false);
        copy.SetBounds(new Rect(copy.Bounds.x + 40, copy.Bounds.y + 20, copy.Bounds.width, copy.Bounds.height));
        copy.CommitLayout(true);
        check(Mathf.Abs(original.Bounds.width - enlargedOriginal.width) < .1f
          && Mathf.Abs(original.Bounds.x - enlargedOriginal.x) < .1f,
          "moving and resizing a copy leaves the original placement unchanged");
        Rect independentBounds = copy.Bounds;
        Vector2 independentAnchor = AnchorPoint(copy.AnchorBounds, copySettings.Anchor);
        source.text = "FPS | 144";
        source.color = new Color(.8f, .3f, .2f);
        source.fontSize = 29;
        copy.UpdateMirroredVisual();
        copyText.ForceMeshUpdate();
        check(copyText.text == source.text && copyText.color == source.color && copyText.fontSize == source.fontSize,
          "copies mirror live values, colors, and font-size changes");
        check(Vector2.Distance(AnchorPoint(copy.AnchorBounds, copySettings.Anchor), independentAnchor) < .1f
          && Mathf.Abs(copy.Bounds.width - independentBounds.width) < .1f,
          "mirroring live values preserves the copy's independent content anchor and scale");

        original.SetEnabled(false);
        copy.UpdateMirroredVisual();
        check(sourceRoot.GetComponent<CanvasGroup>().alpha == 0 && copyRoot.GetComponent<CanvasGroup>().alpha == 1,
          "deleting the original does not hide enabled copies");
        check(copyRoot.gameObject.activeInHierarchy && copy.SelectionBounds.width > 0,
          "enabled copies remain visible and selectable while their original is deleted");
        sourceRoot.SetActive(false);
        sourceRoot.SetActive(true);
        check(sourceRoot.GetComponent<CanvasGroup>().alpha == 0, "gameplay Show calls cannot reveal a deleted original");

        int enabledTransitions = 0;
        copy.EnabledChanged += _ => enabledTransitions++;
        copy.SetEnabled(false);
        source.text = "FPS | 240";
        copy.UpdateMirroredVisual();
        check(copyText.text == "FPS | 144", "disabled copies skip visual mirroring work");
        check(copy.SelectionBounds.width == 0 && copyRoot.GetComponent<CanvasGroup>().alpha == 0,
          "disabled copies have no selectable content");
        copySettings.Enabled = true;
        copy.RefreshLayout();
        check(copyText.text == "FPS | 240" && copyRoot.GetComponent<CanvasGroup>().alpha == 1,
          "undo or restore immediately refreshes and reveals an enabled copy");
        check(enabledTransitions == 2, "enabled-state notifications support an active-only mirror registry");

        sourceRoot.SetActive(false);
        copy.UpdateMirroredVisual();
        check(!copyRoot.gameObject.activeSelf, "copies follow gameplay source visibility");
        copy.SetPreview(true);
        Canvas.ForceUpdateCanvases();
        check(copy.SelectionBounds.width > 0, "a hidden gameplay source has a selectable editing sample");
        copy.SetEnabled(false);
        check(copy.SelectionBounds.width == 0, "deleting a copy also hides its editing sample");
        copy.SetEnabled(true);
        sourceRoot.SetActive(true);
        copy.UpdateMirroredVisual();
        copy.UpdatePreview();
        check(copyRoot.gameObject.activeSelf, "copies recover their live visual root when gameplay returns");

        alternateFont = Object.Instantiate(font);
        alternateMaterial = new Material(material);
        source.font = alternateFont;
        source.fontSharedMaterial = alternateMaterial;
        copy.UpdateMirroredVisual();
        check(copyText.font == alternateFont && copyText.fontSharedMaterial == alternateMaterial,
          "copies track runtime font and material replacements");
        source.enabled = false;
        copy.UpdateMirroredVisual();
        check(!copyText.enabled, "copies honor the live text renderer's enabled state");
        source.enabled = true;
        source.font = font;
        source.fontSharedMaterial = material;
        copy.UpdateMirroredVisual();

        GameObject gallery = original.CreateGalleryPreview(canvasRoot.transform);
        TextMeshProUGUI galleryText = gallery.GetComponentInChildren<TextMeshProUGUI>();
        check(((RectTransform)gallery.transform).rect.width < originalBounds.width,
          "gallery previews expose compact intrinsic content bounds");
        check(galleryText.font == font && galleryText.fontSharedMaterial == material
          && galleryText.text.Contains("120"), "gallery previews use real text styling with mock values");
        check(gallery.GetComponentsInChildren<ContentSizeFitter>().Length == 0,
          "static gallery samples have no continuously updating layout fitters");
        CheckComposite(canvasRoot.transform, font, material, check);
        CheckAnchoredContent(canvasRoot.transform, font, material, check);
        CheckAnchoredComposite(canvasRoot.transform, font, material, check);
      }
      finally
      {
        Object.DestroyImmediate(canvasRoot);
        if (alternateFont != null)
        {
          // This test font borrows the real font's atlas and material; only the
          // clone is owned here, so TMP must not destroy those shared assets.
          alternateFont.atlasTextures = Array.Empty<Texture2D>();
          alternateFont.material = null;
          Object.DestroyImmediate(alternateFont);
        }
        if (alternateMaterial != null) Object.DestroyImmediate(alternateMaterial);
      }
      return checks;
    }

    private static Vector2 AnchorPoint(Rect rect, Anchor9 anchor) =>
      new Vector2(rect.x + rect.width * OverlayLayoutPosition.AnchorX(anchor), rect.y + rect.height * OverlayLayoutPosition.AnchorY(anchor));

    private static void CheckAnchoredContent(Transform parent, TMP_FontAsset font, Material material, Action<bool, string> check)
    {
      GameObject root = OverlayTextFactory.CreateRect("Anchored title", parent, Vector2.one * .5f,
        Vector2.one * .5f, Vector2.zero, new Vector2(500, 80));
      TextMeshProUGUI text = OverlayTextFactory.AddText(root, font, material, 25, TextAlignmentOptions.TopLeft);
      text.textWrappingMode = TextWrappingModes.NoWrap;
      text.text = "Short title";
      var target = new RectTransformLayoutTarget("song-title", "Song", (RectTransform)root.transform, parent, text, "A Dance of Fire and Ice");
      var settings = new OverlayLayoutSettings { Id = "song-title", Customized = true,
        LayoutVersion = 1, Anchor = Anchor9.BottomRight, OffsetX = -100, OffsetY = -80, Scale = 1 };
      target.Configure(settings);
      Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
      float scale = OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      Vector2 expected = new Vector2(Screen.width - 100 * scale, Screen.height - 80 * scale);
      check(Vector2.Distance(AnchorPoint(target.AnchorBounds, settings.Anchor), expected) < .1f,
        "anchored text pins real glyph edges instead of its maximum edit rectangle");
      Rect shortContent = target.AnchorBounds;
      text.text = "A substantially longer title";
      text.ForceMeshUpdate();
      check(target.AnchorBounds.width > shortContent.width
        && Vector2.Distance(AnchorPoint(target.AnchorBounds, settings.Anchor), expected) < .1f,
        "text growth reflows only that target while retaining its bottom-right anchor");
      Vector3 unchangedPosition = root.transform.position;
      text.color = Color.magenta; text.ForceMeshUpdate();
      check(root.transform.position == unchangedPosition, "color-only mesh refreshes do not move content anchors");
      string persisted = PositionSnapshot(settings);
      Rect prior = target.Bounds;
      target.SetBounds(prior); target.CommitLayout(true);
      check(PositionSnapshot(settings) == persisted, "clicking without movement cannot reselect an anchor or rewrite saved data");
      target.SetBounds(new Rect(prior.x - 30, prior.y - 20, prior.width, prior.height));
      check(PositionSnapshot(settings) == persisted && Vector2.Distance(target.Bounds.position, prior.position) > 1,
        "dragging stages visible geometry without writing settings");
      Rect dragBounds = target.Bounds;
      text.text = "A changed title while dragging"; text.ForceMeshUpdate();
      check(Vector2.Distance(target.Bounds.position, dragBounds.position) < .1f,
        "content growth during a drag cannot pull the edit envelope away from the pointer");
      target.RefreshLayout();
      check(Vector2.Distance(AnchorPoint(target.AnchorBounds, settings.Anchor), expected) < .1f && PositionSnapshot(settings) == persisted,
        "refresh discards staged movement and restores the committed anchor");
      prior = target.Bounds;
      target.SetBounds(new Rect(prior.x - 10, prior.y, prior.width, prior.height));
      target.SetBounds(prior); target.CommitLayout(true);
      check(PositionSnapshot(settings) == persisted, "returning a drag to its starting geometry does not rewrite its anchor");
      target.SetBounds(new Rect(prior.x - 20, prior.y, prior.width * 1.1f, prior.height * 1.1f));
      target.CommitLayout(false);
      check(settings.Anchor == Anchor9.BottomRight && settings.Scale > 1 && settings.LayoutVersion == 1,
        "a committed resize retains its existing anchor");
      target.SetBounds(new Rect(Screen.width * .45f, Screen.height * .4f, target.Bounds.width, target.Bounds.height));
      Rect movedContent = target.AnchorBounds;
      Anchor9 nearest = OverlayLayoutPosition.SelectAnchor(new LayoutBounds(movedContent.x, movedContent.y, movedContent.width, movedContent.height), Screen.width, Screen.height);
      target.CommitLayout(true);
      check(settings.Anchor == nearest, "a committed move chooses the nearest content anchor");
      settings.OffsetX = 10000; settings.OffsetY = -10000;
      persisted = PositionSnapshot(settings);
      target.RefreshLayout();
      Rect corrected = target.Bounds;
      check(corrected.xMin <= Screen.width - corrected.width * .1f + .1f
        && corrected.yMax >= corrected.height * .1f - .1f && PositionSnapshot(settings) == persisted,
        "visibility correction never overwrites saved logical offsets");
      Canvas canvas = parent.GetComponent<Canvas>();
      float previousScale = canvas.scaleFactor;
      canvas.scaleFactor = previousScale * .75f;
      target.RefreshLayout();
      check(PositionSnapshot(settings) == persisted, "viewport-scale refresh cannot migrate or save corrected geometry");
      canvas.scaleFactor = previousScale;
      settings.LayoutVersion = 0; settings.CenterX = .4f; settings.CenterY = .45f; settings.Scale = 1;
      persisted = PositionSnapshot(settings); target.Configure(settings);
      check(PositionSnapshot(settings) == persisted && Mathf.Abs(target.Bounds.center.x - Screen.width * .4f) < .1f,
        "legacy normalized centers load without a jump or automatic migration");
      target.CommitLayout(true);
      check(PositionSnapshot(settings) == persisted, "committing without staged edits leaves legacy XML fields unchanged");
      Rect legacy = target.Bounds;
      target.SetBounds(new Rect(legacy.x + 15, legacy.y + 10, legacy.width, legacy.height));
      check(settings.LayoutVersion == 0, "legacy drag remains unstored until committed");
      Rect staged = target.Bounds; target.CommitLayout(true); target.RefreshLayout();
      check(settings.LayoutVersion == 1 && Vector2.Distance(target.Bounds.position, staged.position) < .1f,
        "the first committed legacy edit migrates without moving the visible element");
      settings.Scale = .1f; target.RefreshLayout();
      check(Mathf.Abs(target.EffectiveScale - .1f) < .001f, "anchored scales below the old editor minimum remain valid");
      settings.Scale = 100; settings.Anchor = Anchor9.Center; settings.OffsetX = settings.OffsetY = 0;
      target.RefreshLayout();
      check(target.EffectiveScale < settings.Scale, "an oversized anchor applies a temporary runtime fit");
      Rect fitted = target.Bounds;
      target.SetBounds(new Rect(fitted.x + 20, fitted.y, fitted.width, fitted.height)); target.CommitLayout(true);
      check(settings.Scale == 100, "moving a temporarily fitted element preserves its saved individual scale");
      fitted = target.Bounds;
      target.SetBounds(new Rect(fitted.x, fitted.y, fitted.width * .9f, fitted.height * .9f)); target.CommitLayout(false);
      check(Mathf.Abs(settings.Scale - target.EffectiveScale) < .001f && settings.Scale < 100,
        "explicit resizing stores the newly chosen scale rather than the prior oversized value");
      target.ResetLayout();
      check(!settings.Customized && settings.LayoutVersion == 0 && target.EffectiveScale == 1,
        "reset restores authored placement and clears the anchored layout version");
      Rect centered = target.Bounds;
      target.CommitLayout(true, true); target.RefreshLayout();
      check(settings.LayoutVersion == 1 && settings.Customized && Vector2.Distance(target.Bounds.position, centered.position) < .1f,
        "explicit new-element initialization captures an already centered element without a no-op migration or jump");
      target.Dispose();
    }

    private static string PositionSnapshot(OverlayLayoutSettings value) =>
      string.Join("|", value.Customized, value.LayoutVersion, value.Anchor, value.OffsetX, value.OffsetY, value.CenterX, value.CenterY, value.Scale);

    private static void CheckAnchoredComposite(Transform parent, TMP_FontAsset font, Material material, Action<bool, string> check)
    {
      GameObject root = OverlayTextFactory.CreateRect("Anchored combo", parent, Vector2.one * .5f,
        Vector2.one * .5f, Vector2.zero, new Vector2(300, 200));
      GameObject titleRoot = OverlayTextFactory.CreateRect("Title", root.transform, Vector2.one * .5f,
        new Vector2(.5f, 0), new Vector2(0, 45), Vector2.zero);
      GameObject valueRoot = OverlayTextFactory.CreateRect("Value", root.transform, Vector2.one * .5f,
        Vector2.one * .5f, Vector2.zero, Vector2.zero);
      TextMeshProUGUI title = OverlayTextFactory.AddText(titleRoot, font, material, 40, TextAlignmentOptions.Center);
      TextMeshProUGUI value = OverlayTextFactory.AddText(valueRoot, font, material, 78, TextAlignmentOptions.Top);
      title.text = "Perfect"; value.text = "120";
      foreach (GameObject child in new[] { titleRoot, valueRoot })
      {
        ContentSizeFitter fitter = child.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      }
      var target = new RectTransformLayoutTarget("combo", "Combo", (RectTransform)root.transform, parent, value, "120");
      var settings = new OverlayLayoutSettings { Id = "combo", Customized = true, LayoutVersion = 1,
        Anchor = Anchor9.Right, OffsetX = -100, OffsetY = 0, Scale = 1 };
      target.Configure(settings); Canvas.ForceUpdateCanvases(); title.ForceMeshUpdate(); value.ForceMeshUpdate();
      Rect resting = target.AnchorBounds;
      Vector2 pinned = AnchorPoint(resting, settings.Anchor);
      Vector3 rootPosition = root.transform.position;
      value.fontSize = 108; title.rectTransform.anchoredPosition = new Vector2(0, 65);
      LayoutRebuilder.ForceRebuildLayoutImmediate(value.rectTransform);
      value.ForceMeshUpdate(); title.ForceMeshUpdate();
      check(Vector2.Distance(target.AnchorBounds.size, resting.size) < .1f && root.transform.position == rootPosition,
        "combo bump font and title animation cannot move or resize stable anchoring bounds");
      value.text = "12345678";
      LayoutRebuilder.ForceRebuildLayoutImmediate(value.rectTransform); value.ForceMeshUpdate();
      check(target.AnchorBounds.width > resting.width
        && Vector2.Distance(AnchorPoint(target.AnchorBounds, settings.Anchor), pinned) < .1f,
        "count growth during a combo bump uses resting-size glyphs and keeps its pinned edge");
      Rect duringBump = target.AnchorBounds;
      value.fontSize = 78; title.rectTransform.anchoredPosition = new Vector2(0, 45);
      LayoutRebuilder.ForceRebuildLayoutImmediate(value.rectTransform); value.ForceMeshUpdate(); title.ForceMeshUpdate();
      check(Vector2.Distance(target.AnchorBounds.position, duringBump.position) < .1f
        && Vector2.Distance(target.AnchorBounds.size, duringBump.size) < .1f,
        "ending the combo animation cannot rewrite the established anchor geometry");
      check(Vector2.Distance(target.SelectionBounds.size, target.AnchorBounds.size) < .5f,
        "resting combo content matches the normalized stable geometry after an animated text change");
      target.Dispose();
    }

    private static void CheckComposite(Transform parent, TMP_FontAsset font, Material material, Action<bool, string> check)
    {
      GameObject root = OverlayTextFactory.CreateRect("Combo source", parent, new Vector2(.5f, .5f),
        new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 200));
      GameObject titleRoot = OverlayTextFactory.CreateRect("ComboTitle", root.transform, new Vector2(.5f, .5f),
        new Vector2(.5f, .5f), new Vector2(0, 45), new Vector2(200, 40));
      TextMeshProUGUI title = OverlayTextFactory.AddText(titleRoot, font, material, 30, TextAlignmentOptions.Center);
      title.text = "Perfect";
      GameObject valueRoot = OverlayTextFactory.CreateRect("ComboValue", root.transform, new Vector2(.5f, .5f),
        new Vector2(.5f, .5f), new Vector2(0, -20), new Vector2(200, 100));
      TextMeshProUGUI value = OverlayTextFactory.AddText(valueRoot, font, material, 78, TextAlignmentOptions.Center);
      value.text = "120";
      RectTransformLayoutTarget original = new RectTransformLayoutTarget("combo", "Combo", (RectTransform)root.transform,
        parent, value, "120");
      original.Configure(new OverlayLayoutSettings { Id = "combo" });
      RectTransformLayoutTarget copy = original.CreateInstance("combo:copy", parent);
      copy.Configure(new OverlayLayoutSettings { Id = "combo:copy", TemplateId = "combo" });
      Transform copyRoot = parent.Find("Overlay instance: combo:copy");
      TextMeshProUGUI copyTitle = copyRoot.Find("ComboTitle").GetComponent<TextMeshProUGUI>();
      TextMeshProUGUI copyValue = copyRoot.Find("ComboValue").GetComponent<TextMeshProUGUI>();
      value.fontSize = 100;
      title.rectTransform.anchoredPosition = new Vector2(0, 65);
      value.rectTransform.localScale = new Vector3(1.1f, 1.1f, 1);
      copy.UpdateMirroredVisual();
      check(copyValue.fontSize == 100 && copyTitle.rectTransform.anchoredPosition.y == 65
        && copyValue.rectTransform.localScale == value.rectTransform.localScale,
        "composite copies follow child animation geometry without copying animation drivers");
      titleRoot.SetActive(false);
      copy.UpdateMirroredVisual();
      check(!copyTitle.gameObject.activeSelf, "composite copies preserve per-child visibility");
      titleRoot.SetActive(true);
      copy.UpdateMirroredVisual();
      check(copyTitle.gameObject.activeSelf, "a composite child becomes visible again when its source returns");
      GameObject gallery = original.CreateGalleryPreview(parent);
      TextMeshProUGUI[] galleryTexts = gallery.GetComponentsInChildren<TextMeshProUGUI>();
      check(galleryTexts.Length == 2, "composite gallery includes title and value");
      TextMeshProUGUI galleryValue = gallery.transform.Find("Content/ComboValue").GetComponent<TextMeshProUGUI>();
      check(galleryValue.fontSize == 78, "gallery samples retain the resting pose during a live animation");
    }
  }
}
