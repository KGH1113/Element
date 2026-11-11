using System;
using Element.Configuration.Overlay;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// Which point of the element stays put when it changes size: the nine-point
/// picker, Auto, and a line saying what the choice does.
/// </summary>
internal sealed class OverlayGrowPopover : IPopover
{
  private const float Width = 312,
    Padding = 14,
    Title = 22;

  private readonly OverlayGrowPicker _picker;

  public OverlayGrowPopover(SettingsUiFactory ui, Transform parent, bool reduceMotion, Action<OverlayGrowth> picked)
  {
    Image panel = ui.Panel("Grow popover", parent, 0, 0, Width, Height, new Color(.09f, .09f, .09f, .98f));
    panel.raycastTarget = true;
    Root = panel.rectTransform;
    TextMeshProUGUI title = ui.Text(
      L("크기가 바뀔 때 고정할 점", "Point that stays when the size changes"),
      Root,
      Padding,
      Padding - 2,
      Width - Padding * 2,
      Title,
      13
    );
    title.textWrappingMode = TextWrappingModes.NoWrap;
    _picker = new OverlayGrowPicker(ui, Root, reduceMotion, picked);
    SettingsUiFactory.Place(_picker.Root, Padding, Padding + Title + 6, OverlayGrowPicker.Size, OverlayGrowPicker.Size);
    float right = Padding + OverlayGrowPicker.Size + 14;
    SettingsUiFactory.Place((RectTransform)_picker.Auto.transform, right, Padding + Title + 6, _picker.AutoWidth, 26);
    SettingsUiFactory.Place(
      _picker.Caption.rectTransform,
      right,
      Padding + Title + 38,
      Width - right - Padding,
      OverlayGrowPicker.Size - 32
    );
    Root.gameObject.SetActive(false);
  }

  public static float Height => Padding * 2 + Title + 6 + OverlayGrowPicker.Size;

  public RectTransform Root { get; }

  public bool Visible => Root.gameObject.activeSelf;

  public string Caption => _picker.Caption.text;

  /// <summary>Opens below <paramref name="anchor"/> (frame units).</summary>
  public void Show(Rect anchor, float viewWidth, float viewHeight)
  {
    Root.gameObject.SetActive(true);
    PopoverPlacement.Place(
      Root,
      PopoverPlacement.Under(anchor, anchor.center.x - Width * .5f, Width, Height, 10, viewWidth, viewHeight)
    );
    Root.SetAsLastSibling();
  }

  /// <summary>Shows <paramref name="growth"/>; with Auto, <paramref name="point"/> is today's point.</summary>
  public void Refresh(OverlayGrowth growth, Anchor9 point) => _picker.Show(growth, point);

  public void Hide()
  {
    if (Visible)
      Root.gameObject.SetActive(false);
  }

  public bool IsTyping => false;

  /// <summary>The button that opens it, when its owner says so.</summary>
  public RectTransform Anchor { get; set; }

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);
}
