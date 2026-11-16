using System;
using Element.Configuration;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The highlight color, picked above the layout editor's toolbar: the shared
/// picker as a card, opaque, with Default putting Element's color back.
/// </summary>
internal sealed class OverlayColorPopover : IPopover, IDisposable
{
  private readonly ElementSettings _settings;
  private readonly UiColorPicker _picker;
  public RectTransform Root => _picker.Root;
  public bool Visible => _picker.Visible;
  public bool IsTyping => _picker.IsTyping;
  public RectTransform Anchor => null;
  public const float Width = UiColorPicker.Width;

  /// <summary>Its height as laid out: the card with its square, hue and HEX with Default.</summary>
  public float Height => _picker.Height;

  public OverlayColorPopover(Transform parent, TMP_FontAsset font, ElementSettings settings)
  {
    _settings = settings;
    _picker = new UiColorPicker(parent, font, () => settings.ReduceUiMotion);
    UiTheme.TryParseHighlight(UiTheme.DefaultHighlight, out Color defaultColor);
    _picker.SetDefault(defaultColor);
  }

  public void Show()
  {
    if (!UiTheme.TryParseHighlight(_settings.HighlightColor, out Color color))
      UiTheme.TryParseHighlight(UiTheme.DefaultHighlight, out color);
    _picker.Show(L("하이라이트 색상", "Highlight color"), color, false, Apply);
  }

  private void Apply(Color color)
  {
    _settings.HighlightColor = "#" + ColorUtility.ToHtmlStringRGB(color);
    UiTheme.SetHighlight(_settings.HighlightColor);
  }

  public bool Commit() => _picker.Commit();

  public void Hide() => _picker.Hide();

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(Root, screen);

  public void Dispose() => _picker.Dispose();
}
