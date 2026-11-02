using System;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>
/// The screen the text and shape editors float on: a dim that fades in, a
/// top line with the shortcuts, Cancel and Done, and room for notices. The
/// stage holds content at its real size in screen pixels; controls live in
/// <see cref="Frame"/>, scaled to the window. A dragged value's ghost and the
/// hover tooltip float above both.
/// </summary>
internal sealed class OverlayEditorFrame : IDisposable
{
  public const float Margin = 32,
    TopBar = 72,
    FadeSeconds = .16f,
    NoticeSeconds = 4;

  private readonly Func<bool> _reduceMotion;
  private readonly CanvasGroup _fade;
  private readonly TextMeshProUGUI _hint;
  private readonly string _hintText;
  private readonly float _openedAt;
  private RectTransform _ghost;
  private TextMeshProUGUI _ghostText;
  private SettingsCapsuleGraphic _ghostSurface;
  private RectTransform _tip;
  private TextMeshProUGUI _tipText;
  private float _noticeUntil;

  public OverlayEditorFrame(
    string name,
    Transform parent,
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    string hint,
    Action cancel,
    Action done
  )
  {
    _reduceMotion = reduceMotion;
    _hintText = hint;
    _openedAt = Time.unscaledTime;
    ViewWidth = Screen.width;
    ViewHeight = Screen.height;
    Ui = new SettingsUiFactory(font) { ReduceMotion = reduceMotion };
    Root = Ui.Rect(name, parent, 0, 0, ViewWidth, ViewHeight).gameObject;
    _fade = Root.AddComponent<CanvasGroup>();
    _fade.alpha = reduceMotion() ? 1 : 0;
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
    _hint = Ui.Text(hint, Frame, Margin, 26, Width - Margin * 2 - 230, 28, 13, SettingsUiFactory.Muted);
    _hint.textWrappingMode = TextWrappingModes.NoWrap;
    Button cancelButton = Ui.Button(
      SettingsLocale.L("취소", "Cancel"),
      Frame,
      Width - Margin - 212,
      20,
      100,
      38,
      cancel
    );
    cancelButton.name = name + " cancel";
    Button doneButton = Ui.Button(
      SettingsLocale.L("완료", "Done"),
      Frame,
      Width - Margin - 104,
      20,
      104,
      38,
      done,
      true
    );
    doneButton.name = name + " done";
    Root.transform.SetAsLastSibling();
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public SettingsUiFactory Ui { get; }
  public GameObject Root { get; }

  /// <summary>Screen pixels from the top left, for content shown at its real size.</summary>
  public RectTransform Stage { get; }

  /// <summary>Controls, in frame units of <see cref="Scale"/> screen pixels.</summary>
  public RectTransform Frame { get; }

  public float Scale { get; }
  public float Width { get; }
  public float Height { get; }
  public int ViewWidth { get; }
  public int ViewHeight { get; }
  public TextMeshProUGUI Measure { get; }

  public bool ViewChanged => ViewWidth != Screen.width || ViewHeight != Screen.height;

  /// <summary>Fades in, and puts the shortcuts back once a notice has been read.</summary>
  public void Tick()
  {
    if (_fade.alpha < 1)
      _fade.alpha = _reduceMotion() ? 1 : Mathf.Clamp01((Time.unscaledTime - _openedAt) / FadeSeconds);
    if (_noticeUntil > 0 && Time.unscaledTime > _noticeUntil)
    {
      _noticeUntil = 0;
      _hint.text = _hintText;
      _hint.color = SettingsUiFactory.Muted;
    }
  }

  /// <summary>A short message where the shortcuts are; a warning reads in a warm color.</summary>
  public void Notice(string message, bool warning = true)
  {
    _hint.text = message;
    _hint.color = warning ? OverlayEditorUi.Warning : SettingsUiFactory.Foreground;
    _noticeUntil = Time.unscaledTime + NoticeSeconds;
  }

  public string NoticeText => _noticeUntil > 0 ? _hint.text : "";

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
      SettingsUiFactory.Stretch(_ghostText.rectTransform, 0, 0, 0, 0);
    }
    _ghost.gameObject.SetActive(true);
    _ghostText.text = text;
    float width = Mathf.Ceil(_ghostText.GetPreferredValues(text).x) + 28;
    Vector2 at = ToFrame(screen);
    SettingsUiFactory.Place(_ghost, at.x + 10, at.y + 12, width, 30);
    _ghostSurface.color = state < 0 ? new Color(1, 1, 1, .45f) : SettingsPalette.SoftAccent;
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
      _tip = Ui.Capsule("Value tooltip", Frame, 0, 0, 10, 26, new Color(.2f, .2f, .2f, .98f)).rectTransform;
      _tipText = Ui.Text("", _tip, 0, 0, 10, 26, 12);
      _tipText.alignment = TextAlignmentOptions.Midline;
      _tipText.textWrappingMode = TextWrappingModes.NoWrap;
      SettingsUiFactory.Stretch(_tipText.rectTransform, 0, 0, 0, 0);
    }
    _tip.gameObject.SetActive(true);
    _tipText.text = text;
    float width = Mathf.Ceil(_tipText.GetPreferredValues(text).x) + 24;
    float x = Mathf.Clamp(anchor.x - width * .5f, 8, Mathf.Max(8, Width - width - 8));
    SettingsUiFactory.Place(_tip, x, Mathf.Max(8, anchor.y - 34), width, 26);
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
    Ui.Dispose();
    SettingsUiFactory.Destroy(Root);
  }
}
