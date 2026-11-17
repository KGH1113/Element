using System;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using static ElementUI.UI;
using Motion = ElementUI.Motion;
using Object = UnityEngine.Object;

namespace Element.Presentation.Ui.Widgets;

/// <summary>A non-interactive notice above the game, with declarative content and presentation.</summary>
internal sealed class UiToast : IDisposable
{
  private const float Height = 72,
    Enter = .18f,
    Hold = 1.8f,
    Exit = .22f;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly State<string> _title = new(""),
    _detail = new("");
  private readonly State<UiColor> _accent = new(Colors.Accent);
  private readonly State<bool> _visible = new(false),
    _entered = new(false);
  private GameObject _root;
  private Canvas _canvas;
  private RectTransform _card;
  private UiHost _host;
  private IDisposable _timeout,
    _dismiss;

  public UiToast(TMP_FontAsset font, Func<bool> reduceMotion)
  {
    _font = font;
    _reduceMotion = reduceMotion;
  }

  internal GameObject Root => _root;

  public void Show(string title, string detail)
  {
    Build();
    _timeout?.Dispose();
    _dismiss?.Dispose();
    _title.Value = title;
    _detail.Value = detail;
    _accent.Value = UiPalette.SoftAccent;
    WithAnimation(
      null,
      () =>
      {
        _visible.Value = false;
        _entered.Value = false;
      }
    );
    ElementUI.Runtime.Flush();
    Resize(Screen.width, Screen.height);
    _root.SetActive(true);
    bool reduced = _reduceMotion();
    WithAnimation(
      reduced ? null : Motion.EaseOut(Enter),
      () =>
      {
        _visible.Value = true;
        _entered.Value = true;
      }
    );
    _timeout = ElementUI.Runtime.Delay(
      Enter + Hold,
      () =>
      {
        if (!reduced)
          WithAnimation(Motion.Linear(Exit), () => _visible.Value = false);
        _dismiss = ElementUI.Runtime.Delay(Exit, () => _root.SetActive(false));
      }
    );
  }

  internal void Resize(int pixelWidth, int pixelHeight)
  {
    if (_root == null)
      return;
    float scale = Mathf.Clamp(pixelHeight / 1000f, 1f, 2f);
    _canvas.scaleFactor = scale;
    float maxWidth = Mathf.Max(240, Mathf.Min(520, pixelWidth / scale - 32));
    ElementUI.Runtime.Flush();
    float natural = _host.Root.ContentSize(Proposal.Ideal).Width + 12;
    float width = Mathf.Clamp(natural, Mathf.Min(280, maxWidth), maxWidth);
    _card.sizeDelta = new Vector2(width, Height);
    _host.Root.Resize(new Size(width, Height));
  }

  private void Build()
  {
    if (_root != null)
      return;
    _root = new GameObject("Element Notice", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    _canvas = _root.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _canvas.sortingOrder = 32759;
    _card = new GameObject("Notice", typeof(RectTransform)).GetComponent<RectTransform>();
    _card.SetParent(_root.transform, false);
    _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(.5f, 1);
    _card.anchoredPosition = new Vector2(0, -24);
    _card.sizeDelta = new Vector2(320, Height);
    View content = HStack(
        spacing: 17,
        Spacer().Frame(width: 3, height: 44).Background(() => _accent.Value),
        VStack(
            spacing: 2,
            Text(() => _title.Value)
              .Font(ElementUI.Font.Body.Sized(17))
              .LineLimit(1)
              .Frame(height: 24, alignment: Alignment.Leading),
            Text(() => _detail.Value)
              .Font(ElementUI.Font.Caption.Sized(13))
              .Foreground(Colors.Muted)
              .LineLimit(1)
              .Frame(height: 20, alignment: Alignment.Leading)
          )
          .Frame(maxWidth: float.PositiveInfinity)
      )
      .Padding(new Insets(14, 0, 14, 20))
      .Frame(height: Height)
      .Background(new Color(.115f, .115f, .115f, .97f), 8)
      .Opacity(() => _visible.Value ? 1f : 0f)
      .Offset(y: () => _entered.Value ? 0f : -8f)
      .AllowsHitTesting(false);
    _host = UiHost.Mount(_card, new ElementBackend(new ElementTheme(_font, false)), content);
    _root.SetActive(false);
  }

  public void Dispose()
  {
    _timeout?.Dispose();
    _dismiss?.Dispose();
    if (_root == null)
      return;
    _root.SetActive(false);
    UiFactory.Destroy(_root);
    _root = null;
    _host = null;
  }
}
