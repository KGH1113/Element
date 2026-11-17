using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Canvas.Toolbar;

/// <summary>
/// The main toolbar's frame at the bottom of the view: where it sits (centered
/// at the bottom until dragged, then where it was left), its scale to the view,
/// dragging from anywhere but its controls, and the keyboard focus of its
/// scale field. What it shows is <see cref="ToolbarView"/>.
/// </summary>
internal sealed class ToolbarPanel
{
  private const float Width = CanvasSession.ToolbarWidth;

  /// <summary>How many frames the highlight color must hold still before the toolbar is drawn again in it.</summary>
  private const int ThemeSettleFrames = 8;

  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly CanvasStatusLine _status;
  private readonly ElementKinds _kinds;
  private ScreenPanel _panel;
  private TMP_InputField _scale;
  private Selectable _add;
  private float _addX;
  private int _controlsVersion = -1;
  private bool _dragging,
    _placed;
  private Vector2 _position,
    _grab;
  private Rect _lastBounds;
  private int _themeVersion = -1,
    _seenTheme = -1,
    _themeHeld;

  public ToolbarPanel(CanvasSession session, CanvasSelection selection, CanvasStatusLine status, ElementKinds kinds)
  {
    _session = session;
    _selection = selection;
    _status = status;
    _kinds = kinds;
  }

  /// <summary>What the toolbar shows and does; made by <see cref="Build"/>.</summary>
  public ToolbarViewModel Model { get; private set; }

  public RectTransform Root => _panel?.Root;

  /// <summary>The Add Element button, focused again when its menu closes.</summary>
  public Selectable AddButton
  {
    get
    {
      FindControls();
      return _add;
    }
  }

  /// <summary>Whether the toolbar is being dragged.</summary>
  public bool Dragging
  {
    get => _dragging;
    set => _dragging = value;
  }

  /// <summary>Whether the scale field has the keyboard.</summary>
  public bool Typing => _scale != null && _scale.isFocused;

  /// <summary>In screen pixels from the top left; centered at the bottom until dragged.</summary>
  public Rect Bounds
  {
    get
    {
      float scale = _session.ToolbarScale;
      float width = Width * scale;
      float height = (_panel != null ? _panel.Size.y : 120) * scale;
      int viewWidth = _session.ViewWidth,
        viewHeight = _session.ViewHeight;
      Vector2 p = _placed ? _position : new Vector2((viewWidth - width) * .5f, viewHeight - height - 12);
      p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, viewWidth - width));
      p.y = Mathf.Clamp(p.y, 0, Mathf.Max(0, viewHeight - height));
      return new Rect(p, new Vector2(width, height));
    }
  }

  public void Build(CanvasCommands commands)
  {
    Model = new ToolbarViewModel(_session, _selection, _status, _kinds, commands);
    _panel = new ScreenPanel("Layout actions", _session.Root.transform, UiPalette.Toolbar, keyboard: false);
    Mount();
  }

  // Drawn in the highlight color as it is now.
  private void Mount()
  {
    _panel.Mount(_session.NewBackend(_status.Hint), new ToolbarView(Model), null, Width);
    _themeVersion = _seenTheme = UiTheme.Version;
    _controlsVersion = -1;
    _lastBounds = default;
  }

  // The scale field comes and goes with the selection; the Add button stays.
  private void FindControls()
  {
    if (_panel == null || _panel.ControlsVersion == _controlsVersion)
      return;
    _controlsVersion = _panel.ControlsVersion;
    _scale = _panel.Control<TMP_InputField>("Scale percent");
    _add = _panel.Control<Selectable>("Add element");
    if (_add == null)
      return;
    RectTransform add = (RectTransform)_add.transform;
    _addX = _panel.Root.InverseTransformPoint(add.TransformPoint(add.rect.min)).x;
  }

  /// <summary>
  /// Where a popover of <paramref name="width"/> × <paramref name="height"/>
  /// opens: above the toolbar (below it when there is no room), from
  /// <paramref name="toolbarX"/> along it or flush with its right end, scaled to fit the view.
  /// </summary>
  public Rect PopoverAbove(float width, float height, float toolbarX, bool alignRight)
  {
    Rect toolbar = Bounds;
    float viewWidth = _session.ViewWidth,
      viewHeight = _session.ViewHeight;
    float scale = Mathf.Min(_session.UiScale, Mathf.Min((viewWidth - 16) / width, (viewHeight - 16) / height));
    float w = width * scale,
      h = height * scale;
    float x = alignRight ? toolbar.xMax - w : toolbar.x + toolbarX * _session.ToolbarScale;
    return PopoverPlacement.Above(toolbar, x, w, h, 10, viewWidth, viewHeight);
  }

  /// <summary>Above the toolbar from its Add Element button.</summary>
  public Rect PopoverAboveAdd(float width, float height)
  {
    FindControls();
    return PopoverAbove(width, height, _addX, false);
  }

  public void BeginDrag(Vector2 pointer)
  {
    _position = Bounds.position;
    _dragging = _placed = true;
    _grab = pointer - _position;
  }

  public void Move(Vector2 pointer)
  {
    _position = pointer - _grab;
    _position = Bounds.position;
  }

  /// <summary>Whether a control, which takes the press itself, is under <paramref name="pointer"/> (screen pixels from the top left).</summary>
  public bool ControlAt(Vector2 pointer) =>
    _panel != null && _panel.ControlAt(new Vector2(pointer.x, _session.ViewHeight - pointer.y));

  public void Show(bool shown)
  {
    if (_panel != null && _panel.Root.gameObject.activeSelf != shown)
      _panel.Root.gameObject.SetActive(shown);
  }

  /// <summary>A scale being typed is dropped; the field shows the element's again once it lets go.</summary>
  public void DropTypedScale() => Model.DropTypedScale();

  /// <summary>The scale shown is read again; a typed scale is dropped.</summary>
  public void ForgetScale() => Model.ForgetScale();

  public void ScaleBy(float ratio) => Model.ScaleBy(ratio);

  /// <summary>
  /// Applies a scale still being typed when the editor saves; false (with
  /// the field focused again) when it can't be used.
  /// </summary>
  public bool CommitPendingScale()
  {
    if (!Model.CommitTyped())
    {
      _scale?.Select();
      return false;
    }
    if (Typing)
      _scale.DeactivateInputField();
    return true;
  }

  /// <summary>Once a frame: its place, and what it shows.</summary>
  public void Refresh()
  {
    if (_panel == null)
      return;
    FollowHighlight();
    Rect bounds = Bounds;
    if (bounds != _lastBounds)
    {
      _lastBounds = bounds;
      PopoverPlacement.PlaceScaled(_panel.Root, bounds, Width, _panel.Size.y);
      _panel.Root.SetAsLastSibling();
    }
    _panel.Sync();
    FindControls();
    Model.Sync(Typing);
  }

  // The highlight color is drawn into the toolbar's buttons: once it holds
  // still for a few frames (a drag on the color plane stopped, typing
  // paused), the toolbar is drawn again in it, not on every step of a drag.
  // Its dot follows at once.
  private void FollowHighlight()
  {
    int version = UiTheme.Version;
    if (version == _themeVersion)
      return;
    if (version != _seenTheme)
    {
      _seenTheme = version;
      _themeHeld = 0;
      return;
    }
    if (++_themeHeld >= ThemeSettleFrames)
      Mount();
  }

  public void Dispose()
  {
    _panel?.Dispose();
    _panel = null;
    Model?.Dispose();
  }
}
