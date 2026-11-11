using System;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Popovers;

/// <summary>The dimmed layer over the window under an open popover; clicking it closes the popover.</summary>
internal sealed class UiPopoverShade
{
  private readonly RectTransform _window;
  private readonly Button _button;

  public UiPopoverShade(UiFactory ui, RectTransform window, Action clicked)
  {
    _window = window;
    _button = ui.Button("", window, 0, 0, 1000, 600, clicked, false, false);
    _button.gameObject.name = "Dismiss color picker";
    _button.GetComponent<UiCursor>().enabled = false;
    _button.targetGraphic.color = new Color(0, 0, 0, .18f);
    _button.transition = Selectable.Transition.None;
    _button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _button.gameObject.SetActive(false);
  }

  public bool Visible => _button.gameObject.activeSelf;

  /// <summary>Covers the window; the popover goes over it next.</summary>
  public void Show()
  {
    _button.gameObject.SetActive(true);
    _button.transform.SetAsLastSibling();
  }

  public void Hide() => _button.gameObject.SetActive(false);

  public void Layout() =>
    UiFactory.Place((RectTransform)_button.transform, 0, 0, _window.rect.width, _window.rect.height);
}
