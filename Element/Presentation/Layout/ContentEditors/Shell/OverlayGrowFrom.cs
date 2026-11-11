using System;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Where an edited element grows from: the bar's Grow from button, its
/// popover with the nine points, and a dot on the preview at the point that
/// stays put while the popover is open. Done saves the choice with the element.
/// </summary>
internal sealed class OverlayGrowFrom
{
  private const float MarkSize = 12;

  private readonly Action _closeOthers;
  private readonly Action _changed;
  private OverlayElementTarget _element;
  private OverlayEditorFrame _screen;
  private OverlayGrowPopover _popover;
  private SettingsCapsuleGraphic _mark;

  /// <param name="closeOthers">Closes what else is open before the popover opens.</param>
  /// <param name="changed">Redraws the editor after a click.</param>
  public OverlayGrowFrom(Action closeOthers, Action changed)
  {
    _closeOthers = closeOthers;
    _changed = changed;
  }

  public OverlayGrowth Growth { get; private set; }

  public OverlayBarButton Button { get; private set; }

  public bool Visible => _popover.Visible;

  /// <summary>Starts from what <paramref name="element"/> has saved.</summary>
  public void Open(OverlayElementTarget element)
  {
    _element = element;
    Growth = element.Settings?.GrowFrom ?? OverlayGrowth.Auto;
  }

  /// <summary>The Grow from button, added where the bar has it.</summary>
  public OverlayBarButton BuildButton(SettingsUiFactory ui, Transform bar, bool reduceMotion) =>
    Button = new OverlayBarButton(
      ui,
      bar,
      "Grow from",
      L("기준점", "Grow from"),
      Toggle,
      reduceMotion,
      icon: LayoutAction.GrowFrom
    );

  /// <summary>The popover and the dot, over the editor's controls.</summary>
  public void Build(OverlayEditorFrame screen, bool reduceMotion)
  {
    _screen = screen;
    _popover = new OverlayGrowPopover(screen.Ui, screen.Frame, reduceMotion, Pick);
    _mark = screen.Ui.Capsule("Grow point", screen.Frame, 0, 0, MarkSize, MarkSize, UiPalette.SoftAccent);
    _mark.gameObject.SetActive(false);
  }

  public void Hide() => _popover?.Hide();

  public bool Contains(Vector2 screen) => _popover.Contains(screen);

  /// <summary>The button opens the popover under itself, and closes it again.</summary>
  private void Toggle()
  {
    bool open = _popover.Visible;
    _closeOthers();
    if (!open)
    {
      _popover.Show(_screen.FrameRect(Button.Root), _screen.Width, _screen.Height);
      _popover.Refresh(Growth, PointNow());
    }
    _changed();
  }

  private void Pick(OverlayGrowth growth)
  {
    Growth = growth;
    _popover.Refresh(Growth, PointNow());
    _changed();
  }

  // With Auto, the point picked from where the element sits on screen.
  private Anchor9 PointNow() => OverlayLayoutPosition.ValidGrowth(Growth) ? (Anchor9)(int)Growth : _element.GrowPoint;

  /// <summary>Shows the bar's choice: the short name of the point, lit while the popover is open.</summary>
  public void ShowButton() => Button.Set(OverlayGrowPicker.ShortName(Growth), false, _popover.Visible);

  /// <summary>
  /// While the popover is open, puts the dot on the point of the preview's
  /// box (frame units) that stays put; hides it otherwise.
  /// </summary>
  public void PlaceMark(float x, float y, float width, float height)
  {
    Anchor9 point = PointNow();
    Vector2 at = new(
      x + width * OverlayLayoutPosition.AnchorX(point),
      y + height * OverlayLayoutPosition.AnchorY(point)
    );
    _mark.gameObject.SetActive(true);
    SettingsUiFactory.Place(_mark.rectTransform, at.x - MarkSize * .5f, at.y - MarkSize * .5f, MarkSize, MarkSize);
    _mark.transform.SetAsLastSibling();
  }

  public void HideMark() => _mark.gameObject.SetActive(false);
}
