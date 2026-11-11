using System.Collections.Generic;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The row of labeled buttons that floats over an editor's preview: font,
/// size, color… for a text, fill, border… for a shape. Buttons sit side by
/// side in the order they were added.
/// </summary>
internal sealed class OverlayEditorBar
{
  public const float Height = 40;

  private const float Gap = 4;

  private readonly List<OverlayBarButton> _buttons = new();

  public OverlayEditorBar(SettingsUiFactory ui, Transform parent, string name)
  {
    SettingsCapsuleGraphic surface = ui.Capsule(name, parent, 0, 0, 10, Height, UiPalette.Bar);
    surface.raycastTarget = true;
    Root = surface.rectTransform;
  }

  public RectTransform Root { get; }

  public void Add(OverlayBarButton button) => _buttons.Add(button);

  /// <summary>Places the buttons side by side; returns the bar's width.</summary>
  public float LayOut()
  {
    float y = (Height - OverlayBarButton.Height) * .5f;
    float x = y;
    foreach (OverlayBarButton button in _buttons)
    {
      button.Place(x, y);
      x += button.Width + Gap;
    }
    return x - Gap + y;
  }

  /// <summary>Puts the bar at <paramref name="x"/>, <paramref name="y"/> (frame units).</summary>
  public void Place(float x, float y, float width) => SettingsUiFactory.Place(Root, x, y, width, Height);

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);
}
