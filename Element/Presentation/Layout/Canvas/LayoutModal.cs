using System;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Canvas;

/// <summary>A full-screen dialog over the editor, made again when the view size settles.</summary>
internal sealed class LayoutModal
{
  private readonly LayoutSession _session;
  private GameObject _root;
  private UiFactory _ui;
  private int _width,
    _height;
  private Action _rebuild;

  public LayoutModal(LayoutSession session) => _session = session;

  public bool IsOpen => _root != null;

  public RectTransform Open(string name, float width, float height, Action rebuild)
  {
    Close();
    int viewWidth = _session.ViewWidth,
      viewHeight = _session.ViewHeight;
    _rebuild = rebuild;
    _width = viewWidth;
    _height = viewHeight;
    _ui = _session.NewUi();
    _root = _ui.Rect(name, _session.Root.transform, 0, 0, viewWidth, viewHeight).gameObject;
    Image backdrop = _ui.Panel(
      "Backdrop",
      _root.transform,
      0,
      0,
      viewWidth,
      viewHeight,
      new Color(0, 0, 0, .7f),
      false
    );
    backdrop.raycastTarget = true;
    float scale = Mathf.Min(_session.UiScale, Mathf.Min((viewWidth - 32) / width, (viewHeight - 32) / height));
    RectTransform panel = _ui.Panel(
      "Dialog",
      _root.transform,
      (viewWidth - width * scale) * .5f,
      (viewHeight - height * scale) * .5f,
      width,
      height,
      UiPalette.Background
    ).rectTransform;
    panel.localScale = Vector3.one * scale;
    return panel;
  }

  /// <summary>Asks before a change that drops work. Cancel is focused, so Enter is safe.</summary>
  public void Confirm(string name, string title, string body, string confirm, Action confirmed, Action rebuild)
  {
    RectTransform panel = Open(name, 500, 260, rebuild);
    _ui.Text(title, panel, 28, 24, 444, 40, 23);
    _ui.Text(body, panel, 28, 80, 444, 100, 15, UiPalette.Muted);
    Button cancel = _ui.Button(L("취소", "Cancel"), panel, 232, 196, 112, 40, Close);
    _ui.Button(
      confirm,
      panel,
      356,
      196,
      116,
      40,
      () =>
      {
        Close();
        confirmed();
      },
      true
    );
    EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
  }

  public void Close()
  {
    if (_root == null)
      return;
    _root.SetActive(false);
    UiFactory.Destroy(_root);
    _root = null;
    _rebuild = null;
    _ui?.Dispose();
    _ui = null;
  }

  /// <summary>Made again for a view of another size.</summary>
  public void Follow()
  {
    if (_root != null && (_width != _session.ViewWidth || _height != _session.ViewHeight))
      _rebuild?.Invoke();
  }

  public void Raise()
  {
    if (_root != null)
      _root.transform.SetAsLastSibling();
  }
}
