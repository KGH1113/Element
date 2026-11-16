using System;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// A dialog over the editor: a dark backdrop over the whole view and a panel
/// in its middle, scaled to fit; made again when the view size settles. What
/// the panel says is an ElementUI screen (<see cref="LayoutConfirmView"/>).
/// </summary>
internal sealed class LayoutModal
{
  private const float Width = 500;
  private readonly LayoutSession _session;
  private GameObject _root;
  private ScreenPanel _dialog;
  private int _width,
    _height;
  private Action _rebuild;

  public LayoutModal(LayoutSession session) => _session = session;

  public bool IsOpen => _root != null;

  /// <summary>The open dialog's panel; null while closed.</summary>
  public RectTransform Dialog => _dialog?.Root;

  /// <summary>Asks before a change that drops work. Cancel is focused, so Enter is safe.</summary>
  public void Confirm(string name, string title, string body, string confirm, Action confirmed, Action rebuild)
  {
    Close();
    int viewWidth = _session.ViewWidth,
      viewHeight = _session.ViewHeight;
    _rebuild = rebuild;
    _width = viewWidth;
    _height = viewHeight;
    _root = new GameObject(name, typeof(RectTransform));
    RectTransform root = (RectTransform)_root.transform;
    root.SetParent(_session.Root.transform, false);
    RectPeer.Stretch(root, 0, 0, 0, 0);
    Image backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
    backdrop.transform.SetParent(root, false);
    RectPeer.Stretch(backdrop.rectTransform, 0, 0, 0, 0);
    backdrop.color = new Color(0, 0, 0, .7f);
    backdrop.raycastTarget = true;
    LayoutConfirmModel model = new(
      title,
      body,
      confirm,
      Close,
      () =>
      {
        Close();
        confirmed();
      }
    );
    _dialog = new ScreenPanel("Dialog", root, UiPalette.Background, keyboard: true);
    Vector2 size = _dialog.Mount(_session.NewBackend(), new LayoutConfirmView(model), model, Width);
    float scale = Mathf.Min(_session.UiScale, Mathf.Min((viewWidth - 32) / size.x, (viewHeight - 32) / size.y));
    PopoverPlacement.PlaceScaled(
      _dialog.Root,
      new Rect((viewWidth - size.x * scale) * .5f, (viewHeight - size.y * scale) * .5f, size.x * scale, size.y * scale),
      size.x,
      size.y
    );
    Selectable cancel = _dialog.Control<Selectable>("Cancel");
    if (cancel != null)
      EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
  }

  public void Close()
  {
    if (_root == null)
      return;
    _dialog.Dispose();
    _dialog = null;
    _root.SetActive(false);
    RectPeer.DestroyObject(_root);
    _root = null;
    _rebuild = null;
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
