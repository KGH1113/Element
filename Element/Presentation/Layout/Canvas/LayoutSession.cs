using System;
using System.Collections.Generic;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Layout.Graphics;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// What every part of the layout editor shares: the canvas being arranged,
/// its elements' frames, the undo history and the view the editor fills.
/// </summary>
internal sealed class LayoutSession
{
  /// <summary>The largest scale an element may be given, as a factor.</summary>
  public const float MaximumScale = 10f;

  /// <summary>The main toolbar's width before it is scaled to the view.</summary>
  public const float ToolbarWidth = 760;

  private int _viewportWidth,
    _viewportHeight;

  public LayoutSession(
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
  }

  public OverlayCanvasHost Host { get; }

  public IKeyViewerElements KeyViewers { get; }

  /// <summary>The UI font.</summary>
  public TMP_FontAsset Font { get; }

  public ElementSettings Preferences { get; }

  public Func<bool> ReduceMotion { get; }

  public LayoutHistory History { get; } = new();

  /// <summary>Every element, in the order they were added.</summary>
  public List<LayoutFrame> Frames { get; } = new();

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

  public OverlayFonts Fonts => Host.Fonts;

  /// <summary>One undo step for what changed since the last.</summary>
  public void Commit() => History.Commit(Host.LayoutSettings);

  public LayoutFrame AddFrame(IOverlayLayoutTarget target)
  {
    GameObject root = OverlayTextFactory.CreateRect(
      "Layout: " + target.Id,
      Root.transform,
      Vector2.zero,
      new Vector2(0f, 1f),
      Vector2.zero,
      Vector2.one
    );
    LayoutOutlineGraphic outline = root.AddComponent<LayoutOutlineGraphic>();
    outline.raycastTarget = false;
    LayoutFrame frame = new(target, outline);
    Frames.Add(frame);
    return frame;
  }

  /// <summary>Frames for the canvas's elements and the key viewers, made again.</summary>
  public void RebuildFrames()
  {
    foreach (LayoutFrame frame in Frames)
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
