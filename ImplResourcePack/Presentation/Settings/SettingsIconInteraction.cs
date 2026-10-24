using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

// Borderless navigation controls keep their generous target, but feedback
// belongs to the glyph itself instead of introducing another button surface.
internal sealed class SettingsIconInteraction
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
  private Color _targetColor;
  private float _targetScale = 1;

  public void Initialize(Button button, Graphic icon, Func<bool> reduceMotion)
  {
    _button = button;
    _icon = icon;
    _reduceMotion = reduceMotion;
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
      _icon.color = _normal;
      _icon.rectTransform.localScale = Vector3.one;
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
    _targetColor =
      !available ? SettingsUiFactory.Muted
      : _inside ? _hover
      : _normal;
    if (_pressed && _inside && available)
      _targetColor = Color.Lerp(_targetColor, Color.black, .16f);
    _targetScale = _pressed && _inside && available && !(_reduceMotion?.Invoke() ?? false) ? .92f : 1;
    if (immediate || (_reduceMotion?.Invoke() ?? false))
    {
      _icon.color = _targetColor;
      _icon.rectTransform.localScale = Vector3.one * _targetScale;
    }
  }

  private void Update()
  {
    if (_icon == null)
      return;
    if (_icon.color == _targetColor && Mathf.Approximately(_icon.rectTransform.localScale.x, _targetScale))
      return;
    float t = 1 - Mathf.Exp(-28 * Time.unscaledDeltaTime);
    if (_icon.color != _targetColor)
      _icon.color = Color.Lerp(_icon.color, _targetColor, t);
    float scale = Mathf.Lerp(_icon.rectTransform.localScale.x, _targetScale, t);
    if (Mathf.Abs(scale - _targetScale) < .001f)
      scale = _targetScale;
    if (_icon.rectTransform.localScale.x != scale)
      _icon.rectTransform.localScale = Vector3.one * scale;
  }
}
