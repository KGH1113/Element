using System;
using Element.Configuration;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Overlay highlights use the shared picker in RGB mode. Planet customization
// uses the same controls with its independent alpha channel enabled.
internal sealed class OverlayColorPopover : IDisposable
{
  private readonly ElementSettings _settings;
  private readonly SettingsColorPicker _picker;
  public RectTransform Root => _picker.Root;
  public bool Visible => _picker.Visible;
  public bool IsTyping => _picker.IsTyping;
  public const float Width = SettingsColorPicker.Width,
    Height = 318;

  public OverlayColorPopover(Transform parent, TMP_FontAsset font, ElementSettings settings)
  {
    _settings = settings;
    _picker = new SettingsColorPicker(parent, font, () => settings.ReduceUiMotion);
    OverlayTheme.TryParseHighlight(OverlayTheme.DefaultHighlight, out Color defaultColor);
    _picker.SetDefault(defaultColor);
  }

  public void Show()
  {
    if (!OverlayTheme.TryParseHighlight(_settings.HighlightColor, out Color color))
      OverlayTheme.TryParseHighlight(OverlayTheme.DefaultHighlight, out color);
    _picker.Show(L("하이라이트 색상", "Highlight color"), color, false, Apply);
  }

  private void Apply(Color color)
  {
    _settings.HighlightColor = "#" + ColorUtility.ToHtmlStringRGB(color);
    OverlayTheme.SetHighlight(_settings.HighlightColor);
  }

  public bool Commit() => _picker.Commit();

  public void Hide() => _picker.Hide();

  public void Dispose() => _picker.Dispose();
}
