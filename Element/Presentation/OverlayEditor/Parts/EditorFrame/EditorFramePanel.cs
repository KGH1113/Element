using System;
using System.Collections.Generic;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Motion = ElementUI.Motion;

namespace Element.Presentation.OverlayEditor.Parts.EditorFrame;

/// <summary>
/// The screen the text and shape editors float on: a deep dim that fades
/// in, leaving the game faintly behind, and a header with what is edited,
/// Cancel and Done, as wide as
/// the editor's content (<see cref="ContentLeft"/>, <see cref="ContentWidth"/>). Notices go to the input dock's
/// line when the editor has one, else to the header. The
/// stage holds content at its real size in screen pixels; controls live in
/// <see cref="Frame"/>, scaled to the window. A dragged value's ghost and the
/// hover tooltip float above both. The header, the columns and the popovers
/// are ElementUI screens on panels the frame makes (<see cref="Panel"/>).
/// An editor with an input dock says so when it makes the frame (<c>noticesInDock</c>).
/// </summary>
internal sealed class EditorFramePanel : IDisposable
{
  public const float Margin = 32,
    TopBar = 52,
    FadeSeconds = .16f,
    NoticeSeconds = 4;

  // Opaque, so the overlay behind never shows through: a carried value that
  // can't land here reads greyed, and a tooltip as a raised label.
  private static readonly Color RefusedGhost = UiPalette.Wash(UiPalette.EditorDim, .45f),
    Tip = new(.2f, .2f, .2f);

  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly AnimatedFloat _fade;
  private readonly EditorFrameViewModel _top;
  private readonly List<ScreenPanel> _panels = new();
  private RectTransform _ghost;
  private TextMeshProUGUI _ghostText;
  private UiCapsuleGraphic _ghostSurface;
  private RectTransform _tip;
  private TextMeshProUGUI _tipText;

  public EditorFramePanel(
    string name,
    Transform parent,
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    string title,
    Action cancel,
    Action done,
    bool noticesInDock
  )
  {
    _font = font;
    _reduceMotion = reduceMotion;
    ViewWidth = Screen.width;
    ViewHeight = Screen.height;
    Ui = new UiFactory(font) { ReduceMotion = reduceMotion };
    Root = Ui.Rect(name, parent, 0, 0, ViewWidth, ViewHeight).gameObject;
    CanvasGroup fadeGroup = Root.AddComponent<CanvasGroup>();
    _fade = new AnimatedFloat(alpha => fadeGroup.alpha = alpha);
    _fade.Set(reduceMotion() ? 1 : 0);
    _fade.Set(1, reduceMotion() ? null : Motion.Linear(FadeSeconds));
    // Deep enough that the game's own texts stay far behind the edited one.
    Image dim = Ui.Panel("Dim", Root.transform, 0, 0, ViewWidth, ViewHeight, new Color(0, 0, 0, .92f), false);
    dim.raycastTarget = true;
    Stage = Ui.Rect("Stage", Root.transform, 0, 0, ViewWidth, ViewHeight);
    Scale = Mathf.Clamp(Mathf.Min(ViewWidth / 1120f, ViewHeight / 760f), .55f, 2f);
    Width = ViewWidth / Scale;
    Height = ViewHeight / Scale;
    Frame = Ui.Rect("Controls", Root.transform, 0, 0, Width, Height);
    Frame.localScale = Vector3.one * Scale;
    Measure = Ui.Text("", Frame, 0, 0, 10, 10, 13);
    Measure.fontSize = 13;
    Measure.textWrappingMode = TextWrappingModes.NoWrap;
    Measure.color = Color.clear;
    _top = new EditorFrameViewModel(name, title, cancel, done, noticesInDock);
    // No fill: the dim is deep enough behind it. Its ends line up with the content below.
    // The screen's width but its margins, up to a limit only very wide screens reach.
    ContentWidth = Mathf.Min(1600, Width - Margin * 2);
    ContentLeft = (Width - ContentWidth) * .5f;
    ScreenPanel top = Panel("Header", Color.clear);
    top.Mount(NewBackend(), new EditorFrameView(_top), _top, ContentWidth);
    top.Root.anchoredPosition = new Vector2(Mathf.Round(ContentLeft), 0);
    Root.transform.SetAsLastSibling();
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public UiFactory Ui { get; }
  public GameObject Root { get; }

  /// <summary>Screen pixels from the top left, for content shown at its real size.</summary>
  public RectTransform Stage { get; }

  /// <summary>Controls, in frame units of <see cref="Scale"/> screen pixels.</summary>
  public RectTransform Frame { get; }

  public float Scale { get; }

  /// <summary>How wide the header and the input dock are, in frame units; centered.</summary>
  public float ContentWidth { get; }

  /// <summary>Where the header and the input dock start, in frame units.</summary>
  public float ContentLeft { get; }
  public float Width { get; }
  public float Height { get; }
  public int ViewWidth { get; }
  public int ViewHeight { get; }
  public TextMeshProUGUI Measure { get; }

  public bool ViewChanged => ViewWidth != Screen.width || ViewHeight != Screen.height;

  /// <summary>The header: what is edited, a notice when no dock shows it, Cancel and Done.</summary>
  public EditorFrameViewModel ViewModel => _top;

  /// <summary>
  /// A panel over the controls, on top of those made before, holding an
  /// ElementUI screen (<see cref="ScreenPanel.Mount"/>); it goes with the
  /// frame. A clear <paramref name="color"/> leaves it unfilled. Its
  /// controls never keep the keyboard: Enter and Esc stay the editor's.
  /// </summary>
  public ScreenPanel Panel(string name, Color color, float radius = 8)
  {
    ScreenPanel panel = new(name, Frame, color, keyboard: false, radius);
    _panels.Add(panel);
    return panel;
  }

  /// <summary>Draws a screen in Element's look, at the editors' compact sizes.</summary>
  public ElementBackend NewBackend() =>
    new(new ElementTheme(UiFonts.Choose(_font), _reduceMotion()) { Compact = true });

  /// <summary>Fades in, and lets a notice go once it has been read.</summary>
  public void Tick()
  {
    if (_reduceMotion())
      _fade.Finish();
    _top.Tick();
  }

  /// <summary>A screen point (origin bottom left) in frame units from the top left.</summary>
  public Vector2 ToFrame(Vector2 screen) => new(screen.x / Scale, (ViewHeight - screen.y) / Scale);

  /// <summary>Where <paramref name="rect"/> is, in frame units from the top left.</summary>
  public Rect FrameRect(RectTransform rect)
  {
    Vector3[] corners = new Vector3[4];
    rect.GetWorldCorners(corners);
    Vector2 a = ToFrame(RectTransformUtility.WorldToScreenPoint(null, corners[1])),
      b = ToFrame(RectTransformUtility.WorldToScreenPoint(null, corners[3]));
    return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
  }

  /// <summary>The carried value's name next to the pointer; <paramref name="state"/> -1 dims it.</summary>
  public void ShowGhost(string text, Vector2 screen, int state)
  {
    if (_ghost == null)
    {
      _ghostSurface = Ui.Capsule("Dragged value", Frame, 0, 0, 10, 30, Color.white);
      _ghost = _ghostSurface.rectTransform;
      _ghostText = Ui.Text("", _ghost, 0, 0, 10, 30, 13, new Color(.08f, .08f, .08f));
      _ghostText.alignment = TextAlignmentOptions.Midline;
      _ghostText.textWrappingMode = TextWrappingModes.NoWrap;
      UiFactory.Stretch(_ghostText.rectTransform, 0, 0, 0, 0);
    }
    _ghost.gameObject.SetActive(true);
    _ghostText.text = text;
    float width = Mathf.Ceil(_ghostText.GetPreferredValues(text).x) + 28;
    Vector2 at = ToFrame(screen);
    UiFactory.Place(_ghost, at.x + 10, at.y + 12, width, 30);
    _ghostSurface.color = state < 0 ? RefusedGhost : UiPalette.SoftAccent;
    _ghost.SetAsLastSibling();
  }

  public void HideGhost()
  {
    if (_ghost != null)
      _ghost.gameObject.SetActive(false);
  }

  /// <summary>A small label centered above <paramref name="anchor"/> (frame units).</summary>
  public void ShowTip(string text, Vector2 anchor)
  {
    if (_tip == null)
    {
      _tip = Ui.Capsule("Value tooltip", Frame, 0, 0, 10, 26, Tip).rectTransform;
      _tipText = Ui.Text("", _tip, 0, 0, 10, 26, 12);
      _tipText.alignment = TextAlignmentOptions.Midline;
      _tipText.textWrappingMode = TextWrappingModes.NoWrap;
      UiFactory.Stretch(_tipText.rectTransform, 0, 0, 0, 0);
    }
    _tip.gameObject.SetActive(true);
    _tipText.text = text;
    float width = Mathf.Ceil(_tipText.GetPreferredValues(text).x) + 24;
    float x = Mathf.Clamp(anchor.x - width * .5f, 8, Mathf.Max(8, Width - width - 8));
    UiFactory.Place(_tip, x, Mathf.Max(8, anchor.y - 34), width, 26);
    _tip.SetAsLastSibling();
  }

  public void HideTip()
  {
    if (_tip != null && _tip.gameObject.activeSelf)
      _tip.gameObject.SetActive(false);
  }

  public string TipText => _tip != null && _tip.gameObject.activeSelf ? _tipText.text : "";

  public void Dispose()
  {
    _fade.Dispose();
    foreach (ScreenPanel panel in _panels)
      panel.Dispose();
    _panels.Clear();
    Ui.Dispose();
    UiFactory.Destroy(Root);
  }
}
