using System;
using System.Collections.Generic;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>
/// One option out of a few, opened under the bar button that holds it: the
/// property's name, then a row per option with a dot by the picked one.
/// Picking closes it, like a menu.
/// </summary>
internal sealed class OverlayChoicePopover
{
  private const float Width = 240,
    Padding = 12,
    Title = 22,
    Row = 36;

  private readonly SettingsUiFactory _ui;
  private readonly Transform _parent;
  private readonly bool _reduceMotion;
  private readonly Action<int> _picked;
  private RectTransform _root;

  public OverlayChoicePopover(SettingsUiFactory ui, Transform parent, bool reduceMotion, Action<int> picked)
  {
    _ui = ui;
    _parent = parent;
    _reduceMotion = reduceMotion;
    _picked = picked;
  }

  public RectTransform Root => _root;

  public bool Visible => _root != null && _root.gameObject.activeSelf;

  /// <summary>Which choice it shows, so the same button closes it again.</summary>
  public object Owner { get; private set; }

  /// <summary>Opens below <paramref name="anchor"/> (frame units), or above it when the screen ends.</summary>
  public void Show(
    object owner,
    string title,
    IReadOnlyList<string> options,
    int selected,
    Rect anchor,
    float viewWidth,
    float viewHeight
  )
  {
    Hide();
    Owner = owner;
    float height = Padding * 2 + Title + 4 + Row * options.Count;
    Image panel = _ui.Panel("Choice popover", _parent, 0, 0, Width, height, new Color(.09f, .09f, .09f, .98f));
    panel.raycastTarget = true;
    _root = panel.rectTransform;
    TextMeshProUGUI heading = _ui.Text(
      title,
      _root,
      Padding,
      Padding - 2,
      Width - Padding * 2,
      Title,
      13,
      SettingsUiFactory.Muted
    );
    heading.textWrappingMode = TextWrappingModes.NoWrap;
    for (int i = 0; i < options.Count; i++)
    {
      int index = i;
      float y = Padding + Title + 4 + Row * i;
      Button row = OverlayEditorUi.CapsuleButton(
        _ui,
        _root,
        "Option " + (i + 1),
        "",
        Padding - 4,
        y,
        Width - Padding * 2 + 8,
        Row - 4,
        14,
        () => _picked?.Invoke(index),
        true,
        _reduceMotion
      );
      if (i == selected)
        _ui.Capsule("Picked", row.transform, 12, (Row - 4) * .5f - 4, 8, 8, SettingsPalette.SoftAccent).raycastTarget =
          false;
      TextMeshProUGUI label = _ui.Text(
        options[i],
        row.transform,
        30,
        0,
        Width - Padding * 2 - 30,
        Row - 4,
        14,
        i == selected ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted
      );
      label.alignment = TextAlignmentOptions.MidlineLeft;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.raycastTarget = false;
    }
    float top = anchor.yMax + 10;
    if (top + height > viewHeight - 8)
      top = anchor.y - 10 - height;
    SettingsUiFactory.Place(
      _root,
      Mathf.Clamp(anchor.center.x - Width * .5f, 8, Mathf.Max(8, viewWidth - Width - 8)),
      Mathf.Clamp(top, 8, Mathf.Max(8, viewHeight - height - 8)),
      Width,
      height
    );
    _root.SetAsLastSibling();
  }

  public void Hide()
  {
    Owner = null;
    if (_root == null)
      return;
    SettingsUiFactory.Destroy(_root.gameObject);
    _root = null;
  }

  public bool Contains(Vector2 screen) => Visible && OverlayEditorUi.Contains(_root, screen);
}
