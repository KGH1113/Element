using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The values a color can follow, in a small menu under its value row
/// (<see cref="OverlayValueSourceMenuView"/>). Esc and a press elsewhere close it.
/// </summary>
internal sealed class OverlayValueSourceMenu : IPopover
{
  public const float Width = 232;

  private readonly OverlayEditorFrame _screen;
  private readonly ScreenPanel _panel;

  public OverlayValueSourceMenu(OverlayEditorFrame screen)
  {
    _screen = screen;
    _panel = screen.Panel("Value color sources", UiPalette.Popover, 10);
    Root.gameObject.SetActive(false);
  }

  public RectTransform Root => _panel.Root;

  public bool Visible => Root.gameObject.activeSelf;

  public bool IsTyping => false;

  public RectTransform Anchor { get; set; }

  /// <summary>
  /// Opens with <paramref name="values"/>, <paramref name="followed"/> (a key)
  /// ticked, its top right at <paramref name="right"/>, <paramref name="top"/>
  /// (frame units) and kept on the screen.
  /// </summary>
  public void Show(
    IReadOnlyList<OverlayValueDefinition> values,
    string followed,
    Action<OverlayValueDefinition> picked,
    Action other,
    float right,
    float top
  )
  {
    Root.gameObject.SetActive(true);
    OverlayValueSourceMenuModel model = new(values, followed, picked, other);
    _panel.Mount(_screen.NewBackend(), new OverlayValueSourceMenuView(model), model, Width);
    Vector2 size = _panel.Size;
    float x = Mathf.Clamp(right - Width, 8, _screen.Width - Width - 8);
    float y = Mathf.Clamp(
      top,
      OverlayEditorFrame.TopBar + 4,
      Mathf.Max(OverlayEditorFrame.TopBar + 4, _screen.Height - size.y - 8)
    );
    Root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
    Root.SetAsLastSibling();
  }

  public void Hide()
  {
    if (Visible)
      Root.gameObject.SetActive(false);
  }

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(Root, screen);
}
