using System;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Motion = ElementUI.Motion;

namespace Element.Presentation.Ui.Widgets;

// Borderless navigation controls keep their generous target, but feedback
// belongs to the glyph itself instead of introducing another button surface.
internal sealed class UiIconInteraction
  : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
  private Button _button;
  private Graphic _icon;
  private Func<bool> _reduceMotion;
  private Color _normal,
    _hover;
  private bool _inside,
    _pressed;
  private AnimatedColor _color;
  private AnimatedFloat _scale;

  public void Initialize(Button button, Graphic icon, Func<bool> reduceMotion)
  {
    _button = button;
    _icon = icon;
    _reduceMotion = reduceMotion;
    _color = new AnimatedColor(color =>
    {
      if (_icon != null)
        _icon.color = color;
    });
    _scale = new AnimatedFloat(scale =>
    {
      if (_icon != null)
        _icon.rectTransform.localScale = Vector3.one * scale;
    });
  }

  public void SetPalette(Color normal, Color hover)
  {
    _normal = normal;
    _hover = hover;
    Refresh(true);
  }

  private void OnEnable()
  {
    _inside = _pressed = false;
    Refresh(true);
  }

  private void OnDisable()
  {
    _inside = _pressed = false;
    if (_icon != null)
    {
      _color?.Set(_normal);
      _color?.Finish();
      _scale?.Snap(1);
    }
  }

  public void OnPointerEnter(PointerEventData data)
  {
    _inside = true;
    Refresh(false);
  }

  public void OnPointerExit(PointerEventData data)
  {
    _inside = false;
    Refresh(false);
  }

  public void OnPointerDown(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
    {
      _pressed = true;
      Refresh(true);
    }
  }

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
    {
      _pressed = false;
      Refresh(false);
    }
  }

  private void Refresh(bool immediate)
  {
    if (_icon == null)
      return;
    bool available = _button != null && _button.IsInteractable();
    Color targetColor =
      !available ? UiPalette.Muted
      : _inside ? _hover
      : _normal;
    if (_pressed && _inside && available)
      targetColor = Color.Lerp(targetColor, Color.black, .16f);
    float targetScale = _pressed && _inside && available && !(_reduceMotion?.Invoke() ?? false) ? .92f : 1;
    Motion? animation = immediate || (_reduceMotion?.Invoke() ?? false) ? null : Motion.Exponential(28, .001f);
    _color?.Set(targetColor, animation);
    _scale?.Set(targetScale, animation);
  }

  private void OnDestroy()
  {
    _color?.Dispose();
    _scale?.Dispose();
  }
}
