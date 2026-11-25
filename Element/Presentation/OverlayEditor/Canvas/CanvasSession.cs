using System;
using System.Collections.Generic;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// What every part of the layout editor shares: the canvas being arranged,
/// its elements' frames, the undo history and the view the editor fills.
/// </summary>
internal sealed class CanvasSession
{
  /// <summary>The largest scale an element may be given, as a factor.</summary>
  public const float MaximumScale = 10f;

  /// <summary>The main toolbar's width before it is scaled to the view.</summary>
  public const float ToolbarWidth = 876;

  private readonly HashSet<string> _committedGroups = new();
  private readonly List<IOverlayLayoutTarget> _groupMembers = new();
  private int _viewportWidth,
    _viewportHeight;

  public CanvasSession(
    OverlayCanvasHost host,
    IKeyViewerElements keyViewers,
    TMP_FontAsset font,
    ElementSettings preferences
  )
  {
    Host = host;
    KeyViewers = keyViewers;
    Font = font;
    Preferences = preferences;
    ReduceMotion = () => Preferences.ReduceUiMotion;
    CustomValues = new CustomValueBook(preferences);
  }

  public OverlayCanvasHost Host { get; }

  public IKeyViewerElements KeyViewers { get; }

  /// <summary>The UI font.</summary>
  public TMP_FontAsset Font { get; }

  public ElementSettings Preferences { get; }

  public Func<bool> ReduceMotion { get; }

  /// <summary>The values the player made, which every element shares.</summary>
  public CustomValueBook CustomValues { get; }

  public LayoutHistory History { get; } = new();

  /// <summary>Every element, in the order they were added.</summary>
  public List<CanvasFrame> Frames { get; } = new();

  /// <summary>The editor's canvas.</summary>
  public GameObject Root { get; set; }

  /// <summary>The factory of the editor's own controls.</summary>
  public UiFactory Ui { get; set; }

  public bool IsOpen { get; set; }

  /// <summary>True while the editor closes or goes away; typed values are no longer applied.</summary>
  public bool Closing { get; set; }

  public int ViewWidth => _viewportWidth > 0 ? _viewportWidth : Screen.width;

  public int ViewHeight => _viewportHeight > 0 ? _viewportHeight : Screen.height;

  public float UiScale => Mathf.Clamp(ViewHeight / 1000f, 1f, 2f);

  public float ToolbarScale => Mathf.Min(UiScale, Mathf.Max(.4f, (ViewWidth - 16) / ToolbarWidth));

  /// <summary>Whether the view size is not the one last set.</summary>
  public bool ViewportChanged(int width, int height) => _viewportWidth != width || _viewportHeight != height;

  public void SetViewport(int width, int height)
  {
    _viewportWidth = width;
    _viewportHeight = height;
  }

  /// <summary>A new factory for a panel that comes and goes.</summary>
  public UiFactory NewUi() => new UiFactory(Font, ReduceMotion);

  /// <summary>
  /// What one of the editor's panels draws its ElementUI screen with: the UI
  /// font, the highlight color as it is now and the panels' compact controls.
  /// <paramref name="hints"/> takes what <c>.Hint(...)</c> says (null when the
  /// pointer leaves); without it hints show as bubbles.
  /// </summary>
  public ElementBackend NewBackend(Action<string> hints = null) =>
    new(new ElementTheme(UiFonts.Choose(Font), Preferences.ReduceUiMotion) { Compact = true }) { Hints = hints };

  public OverlayFonts Fonts => Host.Fonts;

  /// <summary>One undo step for what changed since the last.</summary>
  public void Commit() => History.Commit(Host.LayoutSettings);

  public CanvasFrame AddFrame(IOverlayLayoutTarget target)
  {
    GameObject root = OverlayTextFactory.CreateRect(
      "Layout: " + target.Id,
      Root.transform,
      Vector2.zero,
      new Vector2(0f, 1f),
      Vector2.zero,
      Vector2.one
    );
    CanvasOutlineGraphic outline = root.AddComponent<CanvasOutlineGraphic>();
    outline.raycastTarget = false;
    CanvasFrame frame = new(target, outline);
    Frames.Add(frame);
    return frame;
  }

  /// <summary>Frames for the canvas's elements and the key viewers, made again.</summary>
  public void RebuildFrames()
  {
    foreach (CanvasFrame frame in Frames)
    {
      (frame.Target as IExternalLayoutOutline)?.SetSelected(false);
      UiFactory.Destroy(frame.Outline.gameObject);
    }
    Frames.Clear();
    foreach (IOverlayLayoutTarget target in Host.LayoutTargets)
      AddFrame(target);
    if (KeyViewers != null)
      foreach (IOverlayLayoutTarget target in KeyViewers.Targets)
        AddFrame(target);
  }

  /// <summary>
  /// Saves where <paramref name="moved"/> are now. An element in a group is
  /// saved with its whole group against one screen point, so the group stays
  /// together when the screen size changes; the rest pick their nearest point.
  /// </summary>
  public void CommitMoved(IReadOnlyList<IOverlayLayoutTarget> moved)
  {
    _committedGroups.Clear();
    foreach (IOverlayLayoutTarget target in moved)
    {
      string group = GroupOf(target);
      if (group == null)
      {
        target.CommitLayout(true);
        continue;
      }
      if (!_committedGroups.Add(group))
        continue;
      GroupFrames(group, _groupMembers);
      Anchor9 anchor = GroupAnchor(_groupMembers);
      foreach (IOverlayLayoutTarget member in _groupMembers)
        member.CommitLayout(anchor);
    }
  }

  // The screen point nearest the group as a whole; only corners and the
  // middle when a key viewer is in it, since those are all a viewer can hold.
  private Anchor9 GroupAnchor(List<IOverlayLayoutTarget> members)
  {
    bool viewer = false;
    float xMin = float.MaxValue,
      yMin = float.MaxValue,
      xMax = float.MinValue,
      yMax = float.MinValue;
    foreach (IOverlayLayoutTarget member in members)
    {
      viewer |= member is IOverlayLayoutPointer;
      Rect bounds = member.Bounds;
      bounds.position -= OffsetOf(member);
      xMin = Mathf.Min(xMin, bounds.xMin);
      yMin = Mathf.Min(yMin, bounds.yMin);
      xMax = Mathf.Max(xMax, bounds.xMax);
      yMax = Mathf.Max(yMax, bounds.yMax);
    }
    LayoutBounds union = new(xMin, yMin, xMax - xMin, yMax - yMin);
    return viewer
      ? NativeViewerGrowth.SelectAnchor(union, Screen.width, Screen.height)
      : OverlayLayoutPosition.SelectAnchor(union, Screen.width, Screen.height);
  }

  /// <summary>The group <paramref name="target"/> is in, or null.</summary>
  public string GroupOf(IOverlayLayoutTarget target) =>
    target == null ? null : LayoutGroups.Of(Host.LayoutSettings, target.Id);

  /// <summary>Where <paramref name="target"/> draws: higher is further in front; -1 when it isn't saved.</summary>
  public int DrawIndex(IOverlayLayoutTarget target) =>
    target == null ? -1 : LayoutLayers.IndexOf(Host.LayoutSettings, target.Id);

  /// <summary>The frames of <paramref name="group"/>'s shown elements, into <paramref name="members"/>.</summary>
  public void GroupFrames(string group, List<IOverlayLayoutTarget> members)
  {
    members.Clear();
    if (string.IsNullOrEmpty(group))
      return;
    foreach (CanvasFrame frame in Frames)
      if (CanvasSelection.Selectable(frame.Target) && GroupOf(frame.Target) == group)
        members.Add(frame.Target);
  }

  public int IndexOf(IOverlayLayoutTarget target)
  {
    for (int i = 0; i < Frames.Count; i++)
      if (Frames[i].Target == target)
        return i;
    return -1;
  }

  /// <summary>Places <paramref name="transform"/> at <paramref name="bounds"/> (screen pixels, top left).</summary>
  public void SetRect(RectTransform transform, Rect bounds)
  {
    Vector2 size = bounds.size;
    Vector2 position = new(bounds.x, ViewHeight - bounds.y);
    if (transform.sizeDelta != size)
      transform.sizeDelta = size;
    if (transform.anchoredPosition != position)
      transform.anchoredPosition = position;
  }

  /// <summary>
  /// How far <paramref name="target"/>'s own space is from the editor's: zero
  /// except for native viewers, which measure from their window's origin.
  /// </summary>
  public static Vector2 OffsetOf(IOverlayLayoutTarget target)
  {
    if (target is not IOverlayLayoutPointer)
      return Vector2.zero;
    Vector2 mouse = Mouse;
    return PointerFor(target, mouse) - mouse;
  }

  /// <summary>The pointer in <paramref name="target"/>'s space: native viewers measure from their own origin.</summary>
  public static Vector2 PointerFor(IOverlayLayoutTarget target, Vector2 fallback) =>
    target is IOverlayLayoutPointer native ? native.PointerInLayoutSpace(fallback) : fallback;

  public static LayoutBounds ToBounds(Rect rect) => new(rect.x, rect.y, rect.width, rect.height);

  public static Rect ToRect(LayoutBounds bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

  public static float ValidScale(float value) => OverlayLayoutGeometry.Finite(value) && value > 0 ? value : 1;

  /// <summary>The pointer in screen pixels from the top left.</summary>
  public static Vector2 Mouse => new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

  /// <summary>
  /// Whether a level is being played: ordinary keys then belong to the game,
  /// and the editor's keys need Cmd/Ctrl.
  /// </summary>
  public static bool GameplayActive =>
    ADOBase.controller != null
    && !ADOBase.controller.paused
    && ADOBase.conductor != null
    && ADOBase.conductor.isGameWorld;
}
