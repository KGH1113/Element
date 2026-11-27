using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Input;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.FloatView;

internal sealed class FloatScene : IDisposable, ITrackpadViewport
{
  private readonly CanvasSession _session;
  private readonly Func<Rect> _toolbar;
  private readonly Func<Vector2, bool> _beside;
  private readonly FloatOrbit _orbit = new();
  private readonly FloatOrder _order;
  private readonly FloatHover _hover;
  private readonly CanvasCursors _cursors;
  private Vector2? _pointer;
  private readonly float _bottomMargin;
  private readonly RectTransform _root,
    _pitch,
    _plane,
    _shadows;
  private readonly CanvasGroup _fade;
  private readonly FloatGamePicture _picture;
  private readonly List<FloatLayer> _layers = new();
  private FloatLayer _drag;
  private bool _orbiting;
  private Vector2 _press,
    _last;
  private float _lastClick = -1,
    _progress,
    _velocity,
    _lift,
    _step;
  private Vector2 _lastClickPosition;
  private Vector4 _drawnPose;
  private float _drawnLift;
  private bool _dirty = true;
  private int _nativeFrame = -1;
  public bool Open = true;

  /// <param name="beside">
  /// Whether a point (0–1 from the bottom left) is over the order panel
  /// beside the scene, which takes its own gestures.
  /// </param>
  public FloatScene(
    CanvasSession session,
    Func<Rect> toolbar,
    Func<IOverlayLayoutTarget, string> label,
    CanvasCursors cursors,
    Func<Vector2, bool> beside
  )
  {
    _beside = beside;
    _session = session;
    _toolbar = toolbar;
    _order = new FloatOrder(session);
    _cursors = cursors;
    float w = session.ViewWidth,
      h = session.ViewHeight;
    Rect chrome = toolbar();
    _bottomMargin = chrome.y > h * .6f ? h - chrome.y + 8 : 12;
    _root = session
      .Ui.Panel("Float view scene", session.Root.transform, 0, 0, w, h, UiPalette.EditorDim, false)
      .rectTransform;
    _root.SetAsFirstSibling();
    _fade = _root.gameObject.AddComponent<CanvasGroup>();
    _fade.blocksRaycasts = false;
    _pitch = Centered("Camera pitch", _root);
    _plane = Centered("Game plane", _pitch);
    session.Ui.Panel("Screen edge", _plane, -1, -1, w + 2, h + 2, new Color(.22f, .24f, .28f), false);
    var floor = session.Ui.Rect("Game picture", _plane, 0, 0, w, h).gameObject.AddComponent<RawImage>();
    floor.raycastTarget = false;
    _picture = new FloatGamePicture(session.ViewWidth, session.ViewHeight);
    floor.texture = _picture.Texture;
    floor.color = floor.texture == null ? new Color(.10f, .12f, .15f) : Color.white;
    _shadows = session.Ui.Rect("Ground shadows", _plane, 0, 0, w, h);
    _hover = new FloatHover(session, _root, label, toolbar);
    Refresh();
  }

  public void Refresh()
  {
    _dirty = true;
    CancelGesture();
    foreach (var layer in _layers)
      UiFactory.Destroy(layer.Face.gameObject);
    _layers.Clear();
    for (int i = _shadows.childCount - 1; i >= 0; i--)
      UiFactory.Destroy(_shadows.GetChild(i).gameObject);
    UnityEngine.Canvas.ForceUpdateCanvases();
    foreach (var target in _order.Read())
      _layers.Add(new FloatLayer(_session, target, _plane, _shadows, _layers.Count));
    Draw(0);
  }

  private RectTransform Centered(string name, RectTransform parent)
  {
    var rect = _session.Ui.Rect(name, parent, 0, 0, _session.ViewWidth, _session.ViewHeight);
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
    return rect;
  }

  public bool Advance(float dt)
  {
    bool reduced = _session.Preferences.ReduceUiMotion;
    _progress = reduced
      ? Mathf.MoveTowards(_progress, Open ? 1 : 0, dt / .14f)
      : Mathf.SmoothDamp(_progress, Open ? 1 : 0, ref _velocity, .105f, Mathf.Infinity, dt);
    if (Mathf.Abs(_progress - (Open ? 1 : 0)) < .002f)
      _progress = Open ? 1 : 0;
    _orbit.Tick(dt, _orbiting, reduced);
    Draw(dt);
    return Open || _progress > 0;
  }

  private void Draw(float dt)
  {
    Vector4 pose = new(_progress, _orbit.Yaw, _orbit.Pitch, _orbit.Zoom);
    if (!_dirty && pose == _drawnPose && Mathf.Approximately(_lift, _drawnLift))
      return;
    _drawnPose = pose;
    _drawnLift = _lift;
    _dirty = false;
    float t = _session.Preferences.ReduceUiMotion ? 1 : _progress;
    _fade.alpha = _session.Preferences.ReduceUiMotion ? _progress : Mathf.Clamp01(_progress * 14);
    float yaw = Mathf.DeltaAngle(0, _orbit.Yaw) * t;
    float c = Mathf.Cos(yaw * Mathf.Deg2Rad),
      s = Mathf.Sin(yaw * Mathf.Deg2Rad);
    float pitch = _orbit.Pitch * t * Mathf.Deg2Rad;
    float w = _session.ViewWidth,
      h = _session.ViewHeight;
    float liftBudget = Mathf.Min(h * .2f, _layers.Count * h * .028f) * Mathf.Sin(pitch);
    float room = Mathf.Max(h * .4f, h - _bottomMargin - liftBudget - 12);
    float fitWidth = .94f * w / (Mathf.Abs(c) * w + Mathf.Abs(s) * h);
    float fitHeight = room / ((Mathf.Abs(s) * w + Mathf.Abs(c) * h) * Mathf.Cos(pitch));
    float fit = Mathf.Min(.8f, Mathf.Min(fitWidth, fitHeight));
    float scale = Mathf.Lerp(1, fit * _orbit.Zoom, t);
    _pitch.localScale = new Vector3(scale, scale * Mathf.Cos(pitch), 1);
    _plane.localRotation = Quaternion.Euler(0, 0, yaw);
    _pitch.anchoredPosition = new Vector2(0, (_bottomMargin - liftBudget - 12) * .5f * t);
    Vector2 up = new(s, c);
    _step = Mathf.Min(h * .028f, h * .2f / Mathf.Max(1, _layers.Count)) * Mathf.Sin(pitch);
    Matrix4x4 toScreen = _root.worldToLocalMatrix;
    for (int i = 0; i < _layers.Count; i++)
    {
      var layer = _layers[i];
      layer.Rank =
        _session.Preferences.ReduceUiMotion || Mathf.Abs(layer.Rank - i) < .001f
          ? i
          : Mathf.SmoothDamp(layer.Rank, i, ref layer.Velocity, .09f, Mathf.Infinity, dt);
      _dirty |= layer.Rank != i;
      float rise = (layer.Rank + 1) * _step;
      if (layer == _drag)
        rise += _lift;
      layer.Face.anchoredPosition = layer.Home + up * (rise / _pitch.localScale.y);
      layer.Footprint.Project(layer.Face, toScreen);
    }
    if (_pointer.HasValue || _drag != null)
      UpdateHover();
    _hover.Place();
  }

  public void Pointer(Vector2 mouse)
  {
    if (!Open || !UnityEngine.Application.isFocused)
    {
      CancelGesture();
      return;
    }
    bool onCanvas = !_toolbar().Contains(mouse);
    if (onCanvas && _nativeFrame != Time.frameCount && Input.mouseScrollDelta.y != 0)
      _orbit.Magnify(Input.mouseScrollDelta.y * .085f);
    if (onCanvas && Input.GetMouseButtonDown(0))
      Begin(mouse);
    else if (_orbiting || _drag != null)
    {
      Move(mouse);
      if (!Input.GetMouseButton(0))
        End(mouse);
    }
    else
      Hover(mouse);
  }

  internal void Hover(Vector2 mouse)
  {
    if (
      !Open
      || _toolbar().Contains(mouse)
      || mouse.x < 0
      || mouse.x >= _session.ViewWidth
      || mouse.y < 0
      || mouse.y >= _session.ViewHeight
    )
    {
      ClearHover();
      return;
    }
    if (_pointer == mouse)
      return;
    _pointer = mouse;
    UpdateHover();
  }

  private void UpdateHover()
  {
    FloatLayer candidate = _drag;
    if (candidate == null && !_orbiting && _pointer.HasValue)
    {
      float unit = _session.UiScale;
      candidate = Nearest(_pointer.Value, 14 * unit) ?? Nearest(_pointer.Value, 28 * unit);
    }
    _hover.Show(candidate);
    if (candidate != null)
      _cursors.Grab();
    else
      _cursors.Set(LayoutHandle.None);
  }

  private FloatLayer Nearest(Vector2 mouse, float radius)
  {
    FloatLayer closest = null;
    float best = radius * radius;
    // Front to back, so equally distant faces keep the frontmost hit.
    for (int i = _layers.Count - 1; i >= 0; i--)
    {
      float distance = _layers[i].Footprint.DistanceSquared(mouse, radius);
      if (distance == 0)
        return _layers[i];
      if (distance < best || (closest == null && distance == best))
      {
        best = distance;
        closest = _layers[i];
      }
    }
    return closest;
  }

  public void ClearHover()
  {
    _pointer = null;
    _hover.Show(null);
    _cursors.Set(LayoutHandle.None);
  }

  internal void Begin(Vector2 mouse)
  {
    _press = _last = mouse;
    Hover(mouse);
    _drag = _hover.Target;
    if (_drag != null)
    {
      _lastClick = -1;
      return;
    }
    _orbit.Grab();
    _orbiting = true;
  }

  internal void Move(Vector2 mouse)
  {
    if (_orbiting && mouse != _last)
    {
      Vector2 delta = mouse - _last;
      _orbit.Orbit(new Vector2(delta.x / _session.ViewWidth, -delta.y / _session.ViewHeight), true);
    }
    if (_drag != null)
      _lift = Mathf.Clamp(_press.y - mouse.y, -_step * 1.5f, _step * 1.5f);
    _last = mouse;
  }

  internal void End(Vector2 mouse)
  {
    float dy = _press.y - mouse.y;
    if (_drag != null && _step > 0)
      _drag.Rank += _lift / _step;
    if (_drag != null && Mathf.Abs(dy) > 12 * _session.UiScale && _order.Move(_drag.Target, dy > 0 ? 1 : -1))
    {
      var faces = _layers.ToDictionary(layer => layer.Target);
      _layers.Clear();
      foreach (var target in _order.Read())
      {
        var layer = faces[target];
        _layers.Add(layer);
        layer.Face.SetAsLastSibling();
      }
    }
    _dirty = true;
    if (_orbiting && Vector2.Distance(mouse, _press) < 4)
    {
      if (Time.unscaledTime - _lastClick < .32f && Vector2.Distance(mouse, _lastClickPosition) < 8)
        _orbit.Reset();
      _lastClickPosition = mouse;
      _lastClick = Time.unscaledTime;
    }
    CancelGesture();
    Hover(mouse);
  }

  public void CancelGesture()
  {
    ClearHover();
    _orbiting = false;
    _drag = null;
    _lift = 0;
  }

  public bool Accepts(Vector2 p) =>
    Open
    && !_orbiting
    && _drag == null
    && p.x >= 0
    && p.y >= 0
    && p.x <= 1
    && p.y <= 1
    && !_toolbar().Contains(new Vector2(p.x * _session.ViewWidth, (1 - p.y) * _session.ViewHeight))
    && !_beside(p);

  public void SuppressWheel() => _nativeFrame = Time.frameCount;

  public void Orbit(Vector2 delta) => _orbit.Orbit(delta, false);

  public void Rotate(float degrees) => _orbit.Rotate(degrees);

  public void Magnify(float delta) => _orbit.Magnify(delta);

  public void Dispose()
  {
    ClearHover();
    _root.gameObject.SetActive(false);
    UiFactory.Destroy(_root.gameObject);
    _picture.Dispose();
  }
}
