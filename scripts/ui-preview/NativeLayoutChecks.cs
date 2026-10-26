using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Layout;
using ImplResourcePack.Infrastructure.Game.DmNote;
using TMPro;
using UnityEngine;

namespace ImplResourcePack.Editor
{
  // Executes the production managed native target. The fake host supplies
  // AppKit point-space observations and records transport only; no WebKit or
  // actual game integration is claimed by these checks.
  internal static class NativeLayoutChecks
  {
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;

    internal static int Run(TMP_FontAsset font)
    {
      _checks = 0;
      Check(Screen.width > 0 && Screen.height > 0, "fixture has a real Unity viewport");
      CheckKind("hand", font);
      CheckKind("foot", font);
      return _checks;
    }

    private static void CheckKind(string kind, TMP_FontAsset font)
    {
      float viewportWidth = Screen.width;
      float viewportHeight = Screen.height;
      float screenScale = OverlayLayoutPosition.ScreenScale(viewportWidth, viewportHeight);
      Vector2 reference = new Vector2(36, 24);
      const float migratedScale = 7.25f;
      Vector2 size = reference * screenScale * migratedScale;
      Rect original = new Rect(viewportWidth * .82f - size.x * .5f,
        viewportHeight * .81f - size.y * .5f, size.x, size.y);
      var host = new GameWebView
      {
        HostSizePoints = new Vector2(viewportWidth * .5f, viewportHeight * .5f),
        ReferenceSize = reference,
        LegacyBoundsPoints = new Rect(original.x * .5f, original.y * .5f, original.width * .5f, original.height * .5f),
        LegacyEffectiveScale = 2.25f,
      };
      var settings = new OverlayLayoutSettings
      {
        Id = "dmnote-" + kind,
        TemplateId = "native-" + kind,
        Customized = true,
        CenterX = .82f,
        CenterY = .81f,
        Scale = 2.25f,
      };
      var values = new List<OverlayLayoutSettings> { settings };
      var opening = new LayoutSnapshot(values);
      string originalSettings = Serialized(settings);
      using var target = new NativeViewerLayoutTarget(kind, kind, _ => { }, () => { });
      target.ConfigurePreview(font);
      target.Configure(values);
      target.Bind(host);
      Same(original, target.Bounds, "legacy native points normalize into the actual Unity viewport");
      // The legacy center/scale transport only observes where the old placement
      // renders; temporary anchored geometry then holds that same rectangle.
      Check(host.LegacyCalls > 0 && host.LastCustomized
        && host.LastLegacyCenter == new Vector2(.82f, .81f) && host.LastLegacyScale == 2.25f,
        "legacy native layout retains its old transport and scale interpretation");
      target.SetSelected(true);
      target.SetSelected(false);
      target.RefreshLayout();
      target.SetBounds(target.Bounds);
      target.CommitLayout(true);
      Check(Serialized(settings) == originalSettings,
        "loading, selecting and completing a motionless click do not migrate legacy settings");

      Rect moved = original;
      moved.position += new Vector2(-13, 9);
      target.SetBounds(moved);
      Check(Serialized(settings) == originalSettings, "a staged pointer move does not write persisted placement");
      target.SetBounds(original);
      target.CommitLayout(true);
      Same(original, target.Bounds, "returning the pointer to its start restores the legacy visual position");
      Check(Serialized(settings) == originalSettings, "a move returned to its start does not migrate legacy data");
      target.SetBounds(moved);
      target.RefreshLayout();
      Same(original, target.Bounds, "refresh cancels runtime staging before undo or viewport refresh");
      Check(Serialized(settings) == originalSettings, "discarding a staged gesture preserves its opening configuration");

      target.SetBounds(moved);
      Rect staged = target.Bounds;
      target.CommitLayout(true);
      Same(staged, target.Bounds, "the first completed edit migrates without a visual jump");
      Check(settings.LayoutVersion == 1 && settings.Customized && settings.Scale > 5,
        "explicit migration preserves a positive scale beyond the old slider maximum");
      Near(migratedScale, settings.Scale, "new native scale derives from actual pixels and shared reference units");
      Check(host.BoundsCalls > 0, "anchored layout uses normalized rectangle transport");
      Same(target.Bounds, HostPixels(host), "managed and native transports agree on the content rectangle");

      string anchoredSettings = Serialized(settings);
      Rect beforeHostChange = target.Bounds;
      int sends = host.BoundsCalls;
      host.ChangeHost(new Vector2(viewportWidth, viewportHeight));
      Same(beforeHostChange, target.Bounds, "changing native points per Unity pixel preserves anchored pixels");
      Check(host.BoundsCalls > sends, "a native geometry revision refreshes connected layout without IPC reload");
      Same(beforeHostChange, HostPixels(host), "normalized bounds adapt to the changed native host size");
      Check(Serialized(settings) == anchoredSettings, "host backing changes do not rewrite anchor offsets or scale");

      // Unity Screen is real and read-only during this synchronous edit-mode
      // fixture. Simulate stale cached dimensions, then exercise its actual
      // viewport refresh path; pure domain tests cover other viewport sizes.
      SetField(target, "_screenWidth", Screen.width + 400);
      SetField(target, "_screenHeight", Screen.height + 200);
      SetField(target, "_screenScale", -1f);
      Same(beforeHostChange, target.Bounds, "stale viewport caches resolve again against Unity's current viewport");
      Check(Serialized(settings) == anchoredSettings, "viewport refresh preserves saved placement intent");

      Vector2 anchorBefore = AnchorPoint(target.Bounds, settings.Anchor);
      host.ChangeReference(reference * 1.5f);
      Rect changedContent = target.Bounds;
      Check(Vector2.Distance(anchorBefore, AnchorPoint(changedContent, settings.Anchor)) < .1f,
        "late native CSS content dimensions retain the selected anchor point");
      Near(beforeHostChange.width * 1.5f, changedContent.width, "new CSS intrinsic width is resolved before positioning");
      Near(beforeHostChange.height * 1.5f, changedContent.height, "new CSS intrinsic height uses the same uniform scale");
      Check(Serialized(settings) == anchoredSettings, "CSS measurement changes do not migrate or renormalize storage");

      target.Bind(null);
      Same(changedContent, target.Bounds, "disconnect preserves the last known content size and anchored rectangle");
      Check(Serialized(settings) == anchoredSettings, "disconnect preserves layout version, anchor, offsets and scale");
      target.SetPreview(true);
      Canvas.ForceUpdateCanvases();
      RectTransform mock = (RectTransform)target.GetType().GetField("_previewRoot", Private).GetValue(target);
      Check(mock != null && mock.gameObject.activeInHierarchy, "disconnected preview creates the production mock visual");
      Near(changedContent.width, NativeViewerMock.Width(kind) * mock.localScale.x,
        "mock horizontal scaling fills the cached native content rectangle");
      Near(changedContent.height, NativeViewerMock.ViewportHeight * mock.localScale.y,
        "mock vertical scaling preserves arbitrary native content aspect ratios");
      Near(changedContent.x, mock.anchoredPosition.x, "mock uses the same top-left horizontal coordinate");
      Near(-changedContent.y, mock.anchoredPosition.y, "mock converts top-left downward coordinates to Unity UI coordinates");
      target.SetPreview(false);
      target.Bind(host);
      Same(changedContent, target.Bounds, "reconnecting the same native surface preserves the mock placement");

      // A saved anchor may legitimately differ from today's nearest anchor.
      // Resize must preserve that saved choice rather than reselecting it.
      Rect beforeResize = target.Bounds;
      settings.Anchor = Anchor9.TopLeft;
      settings.OffsetX = beforeResize.x / screenScale;
      settings.OffsetY = beforeResize.y / screenScale;
      target.RefreshLayout();
      Check(OverlayLayoutPosition.SelectAnchor(ToBounds(beforeResize), Screen.width, Screen.height) != settings.Anchor,
        "resize fixture distinguishes retained and automatically selected anchors");
      Rect resized = new Rect(beforeResize.x, beforeResize.y, beforeResize.width * .8f, beforeResize.height * .8f);
      target.SetBounds(resized);
      Rect resizeStaged = target.Bounds;
      target.CommitLayout(false);
      Check(settings.Anchor == Anchor9.TopLeft, "resize commit retains the existing anchor");
      Same(resizeStaged, target.Bounds, "resize commit preserves its staged rectangle");
      Near(beforeResize.x, target.Bounds.x, "right-edge resizing keeps the opposite left edge fixed");
      Near(beforeResize.y, target.Bounds.y, "bottom-edge resizing keeps the opposite top edge fixed");

      settings.Scale = 100000;
      settings.Anchor = Anchor9.Center;
      settings.OffsetX = settings.OffsetY = 0;
      target.RefreshLayout();
      Rect capped = target.Bounds;
      Check(target.EffectiveScale < settings.Scale, "oversized saved scale is capped only in the rendered view");
      Rect cappedMove = capped;
      cappedMove.position += new Vector2(7, -5);
      target.SetBounds(cappedMove);
      target.CommitLayout(true);
      Check(settings.Scale == 100000,
        "moving a temporarily capped anchored viewer preserves the original larger scale intent");
      Rect afterCappedMove = target.Bounds;
      target.RefreshLayout();
      Same(afterCappedMove, target.Bounds, "refreshing a moved capped viewer does not rewrite its displayed position");
      Check(settings.Scale == 100000, "passive refresh continues to preserve the saved oversized scale");
      Rect explicitResize = new Rect(afterCappedMove.x, afterCappedMove.y,
        afterCappedMove.width * .9f, afterCappedMove.height * .9f);
      target.SetBounds(explicitResize);
      Rect explicitResizeStaged = target.Bounds;
      target.CommitLayout(false);
      Check(settings.Scale > 0 && settings.Scale < 100000,
        "an explicit resize replaces the old larger scale intent with the chosen visible size");
      Same(explicitResizeStaged, target.Bounds, "resizing a capped viewer commits without a visual jump");

      target.ResetLayout();
      Check(!settings.Customized && settings.LayoutVersion == 0 && settings.Anchor == Anchor9.Center
        && settings.OffsetX == 0 && settings.OffsetY == 0 && settings.Scale == 1
        && settings.CenterX == .5f && settings.CenterY == .5f,
        "explicit reset clears the full anchored format and restores defaults");
      host.ChangeHost(new Vector2(viewportWidth * .5f, viewportHeight * .5f));
      opening.Restore(values);
      target.RefreshLayout();
      Same(original, target.Bounds, "cancel restores the old native rectangle after anchored edits and reset");
      Check(Serialized(settings) == originalSettings, "cancel restores the exact legacy format and all original fields");

      var addedSettings = new OverlayLayoutSettings { Id = "added-" + kind, TemplateId = "native-" + kind };
      using var added = new NativeViewerLayoutTarget(kind, kind, _ => { }, () => { }, addedSettings.Id);
      added.Configure(new List<OverlayLayoutSettings> { addedSettings });
      added.Bind(host);
      Rect addedBounds = added.Bounds;
      added.SetBounds(addedBounds);
      added.CommitLayout(true, initialize: true);
      Check(addedSettings.LayoutVersion == 1 && addedSettings.Customized,
        "explicit new-element initialization captures placement even if its initial move was a no-op");
      Same(addedBounds, added.Bounds, "new-element initialization does not move or resize the visible viewer");
    }

    private static string Serialized(OverlayLayoutSettings settings)
    {
      using var writer = new StringWriter();
      new XmlSerializer(typeof(OverlayLayoutSettings)).Serialize(writer, settings);
      return writer.ToString();
    }

    private static Rect HostPixels(GameWebView host) => new Rect(
      host.ContentBoundsPoints.x / host.HostSizePoints.x * Screen.width,
      host.ContentBoundsPoints.y / host.HostSizePoints.y * Screen.height,
      host.ContentBoundsPoints.width / host.HostSizePoints.x * Screen.width,
      host.ContentBoundsPoints.height / host.HostSizePoints.y * Screen.height);

    private static Vector2 AnchorPoint(Rect rect, Anchor9 anchor) => new Vector2(
      rect.x + rect.width * OverlayLayoutPosition.AnchorX(anchor),
      rect.y + rect.height * OverlayLayoutPosition.AnchorY(anchor));

    private static LayoutBounds ToBounds(Rect value) => new LayoutBounds(value.x, value.y, value.width, value.height);
    private static void SetField(object value, string name, object field) => value.GetType().GetField(name, Private).SetValue(value, field);

    private static void Same(Rect expected, Rect actual, string message)
    {
      Check(Mathf.Abs(expected.x - actual.x) < .1f && Mathf.Abs(expected.y - actual.y) < .1f
        && Mathf.Abs(expected.width - actual.width) < .1f && Mathf.Abs(expected.height - actual.height) < .1f,
        message + ": expected " + expected + ", got " + actual);
    }

    private static void Near(float expected, float actual, string message) =>
      Check(Mathf.Abs(expected - actual) < .001f, message + ": expected " + expected + ", got " + actual);

    private static void Check(bool condition, string message)
    {
      if (!condition) throw new InvalidOperationException("Native layout: " + message);
      _checks++;
    }
  }
}
