using System;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Ui.Widgets;

/// <summary>
/// A brief notice at the top of the screen that confirms an action taken
/// without the settings window, such as a preset shortcut. Built on first use;
/// it costs nothing while hidden.
/// </summary>
internal sealed class UiToast : IDisposable
{
  private const float Height = 72;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private UiFactory _ui;
  private GameObject _root;
  private Canvas _canvas;
  private RectTransform _card;
  private TextMeshProUGUI _title,
    _detail;
  private Image _marker;
  private UiToastMotion _motion;

  public UiToast(TMP_FontAsset font, Func<bool> reduceMotion)
  {
    _font = font;
    _reduceMotion = reduceMotion;
  }

  internal GameObject Root => _root;

  public void Show(string title, string detail)
  {
    Build();
    _title.text = title;
    _detail.text = detail;
    _marker.color = UiPalette.SoftAccent;
    Resize(Screen.width, Screen.height);
    _root.SetActive(true);
    _motion.Play(_reduceMotion());
  }

  /// <summary>Sizes the notice to its text, within the screen, at the settings window's scale.</summary>
  internal void Resize(int pixelWidth, int pixelHeight)
  {
    if (_root == null)
      return;
    float scale = Mathf.Clamp(pixelHeight / 1000f, 1f, 2f);
    _canvas.scaleFactor = scale;
    float maxWidth = Mathf.Max(240, Mathf.Min(520, pixelWidth / scale - 32));
    float textWidth = Mathf.Max(_title.GetPreferredValues(_title.text).x, _detail.GetPreferredValues(_detail.text).x);
    float width = Mathf.Clamp(textWidth + 52, Mathf.Min(280, maxWidth), maxWidth);
    _card.sizeDelta = new Vector2(width, Height);
  }

  private void Build()
  {
    if (_root != null)
      return;
    _ui = new UiFactory(_font) { ReduceMotion = _reduceMotion };
    _root = new GameObject("Element Notice", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    _canvas = _root.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    // Above the game, below the settings window.
    _canvas.sortingOrder = 32759;
    _card = _ui.Panel("Notice", _root.transform, 0, 0, 320, Height, new Color(.115f, .115f, .115f, .97f)).rectTransform;
    _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(.5f, 1);
    _card.anchoredPosition = new Vector2(0, -24);
    _marker = _ui.Panel("Accent", _card, 0, 0, 3, Height, UiPalette.SoftAccent, false);
    _marker.rectTransform.anchorMin = new Vector2(0, 0);
    _marker.rectTransform.anchorMax = new Vector2(0, 1);
    _marker.rectTransform.sizeDelta = new Vector2(3, -28);
    _marker.rectTransform.anchoredPosition = new Vector2(0, -14);
    _title = _ui.Text("", _card, 20, 14, 280, 24, 17);
    UiFactory.StretchWidth(_title.rectTransform, 20, 20);
    _title.textWrappingMode = TextWrappingModes.NoWrap;
    _detail = _ui.Text("", _card, 20, 40, 280, 20, 13, UiPalette.Muted);
    UiFactory.StretchWidth(_detail.rectTransform, 20, 20);
    _detail.textWrappingMode = TextWrappingModes.NoWrap;
    CanvasGroup group = _card.gameObject.AddComponent<CanvasGroup>();
    group.blocksRaycasts = false;
    group.interactable = false;
    _motion = _card.gameObject.AddComponent<UiToastMotion>();
    _motion.Initialize(group, () => _root.SetActive(false));
    _root.SetActive(false);
  }

  public void Dispose()
  {
    if (_root != null)
    {
      _root.SetActive(false);
      UiFactory.Destroy(_root);
      _root = null;
    }
    _ui?.Dispose();
    _ui = null;
  }
}
