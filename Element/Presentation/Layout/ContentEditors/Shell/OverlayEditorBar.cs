using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The row of labeled buttons that floats over an editor's preview: font,
/// size, color… for a text, fill, border… for a shape. A capsule on the
/// editor's controls as wide as its buttons (<see cref="OverlayEditorBarView"/>);
/// the editor places it, and asks it where a button is.
/// </summary>
internal sealed class OverlayEditorBar
{
  public const float Height = 40;

  private readonly ScreenPanel _panel;
  private readonly OverlayEditorBarModel _model;
  private int _fitted;

  public OverlayEditorBar(OverlayEditorFrame screen, string name, params OverlayBarButtonModel[] buttons)
  {
    _model = new OverlayEditorBarModel(buttons);
    _panel = screen.Panel(name, UiPalette.Bar, Height * .5f);
    _panel.Mount(screen.NewBackend(), new OverlayEditorBarView(_model), _model);
    _fitted = _model.Revision;
  }

  public RectTransform Root => _panel.Root;

  /// <summary>Fits the bar to what its buttons show now; returns its width. A compare when nothing changed.</summary>
  public float LayOut()
  {
    int revision = _model.Revision;
    if (revision != _fitted)
    {
      _fitted = revision;
      _panel.Refit();
    }
    return _panel.Size.x;
  }

  /// <summary>Puts the bar's top left at <paramref name="x"/>, <paramref name="y"/> (frame units).</summary>
  public void Place(float x, float y) => Root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

  /// <summary>Whether a screen point (origin bottom left) is over <paramref name="button"/>.</summary>
  public bool Contains(OverlayBarButtonModel button, Vector2 screen) =>
    PopoverPlacement.Contains(RectOf(button), screen);

  /// <summary>Where <paramref name="button"/> is drawn, as a popover's anchor; null when it isn't on this bar.</summary>
  public RectTransform RectOf(OverlayBarButtonModel button)
  {
    Selectable control = _panel.Control<Selectable>(button.Name);
    return control != null ? (RectTransform)control.transform : null;
  }
}
