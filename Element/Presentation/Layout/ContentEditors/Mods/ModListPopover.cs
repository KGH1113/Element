using System;
using System.Collections.Generic;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A dropdown's options, opened under it: its name, then a row per option
/// with a dot by the picked one, scrolling when there are many. Picking closes it.
/// </summary>
internal sealed class ModListPopover
{
  private const float Width = 260,
    Padding = 10,
    Title = 24,
    Row = 34,
    VisibleRows = 8;

  private readonly SettingsUiFactory _ui;
  private readonly Transform _parent;
  private readonly bool _reduceMotion;
  private RectTransform _root;
  private Action<int> _picked;

  public ModListPopover(SettingsUiFactory ui, Transform parent, bool reduceMotion)
  {
    _ui = ui;
    _parent = parent;
    _reduceMotion = reduceMotion;
  }

  public RectTransform Root => _root;

  public bool Visible => _root != null && _root.gameObject.activeSelf;

  /// <summary>The dropdown it opened for, so the same one closes it again.</summary>
  public RectTransform Anchor { get; private set; }

  /// <summary>Opens below <paramref name="anchor"/> (frame units), or above it when the screen ends.</summary>
  public void Show(ModListRequest request, Rect anchor, float viewWidth, float viewHeight)
  {
    Hide();
    Anchor = request.Anchor;
    _picked = request.Picked;
    IReadOnlyList<string> options = request.Options;
    float width = Mathf.Max(Width, anchor.width);
    float listHeight = Row * Mathf.Min(options.Count, VisibleRows);
    float height = Padding * 2 + Title + listHeight;
    Image panel = _ui.Panel("List popover", _parent, 0, 0, width, height, new Color(.09f, .09f, .09f, .98f));
    panel.raycastTarget = true;
    _root = panel.rectTransform;
    TextMeshProUGUI heading = _ui.Text(
      request.Title,
      _root,
      Padding + 4,
      Padding - 2,
      width - Padding * 2,
      Title,
      13,
      SettingsUiFactory.Muted
    );
    heading.textWrappingMode = TextWrappingModes.NoWrap;

    // The rows scroll inside the popover when there are more than fit.
    RectTransform viewport = _ui.Rect(
      "List viewport",
      _root,
      Padding - 4,
      Padding + Title,
      width - Padding * 2 + 8,
      listHeight
    );
    viewport.gameObject.AddComponent<RectMask2D>();
    Image catcher = viewport.gameObject.AddComponent<Image>();
    catcher.color = Color.clear;
    RectTransform content = _ui.Rect("List content", viewport, 0, 0, width - Padding * 2 + 8, Row * options.Count);
    ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
    scroll.viewport = viewport;
    scroll.content = content;
    scroll.horizontal = false;
    scroll.vertical = options.Count > VisibleRows;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.scrollSensitivity = Row;
    for (int i = 0; i < options.Count; i++)
    {
      int index = i;
      Button row = OverlayEditorUi.CapsuleButton(
        _ui,
        content,
        "Option " + (i + 1),
        "",
        0,
        Row * i,
        width - Padding * 2 + 8,
        Row - 2,
        14,
        () => Pick(index),
        true,
        _reduceMotion
      );
      if (i == request.Selected)
        _ui.Capsule("Picked", row.transform, 12, (Row - 2) * .5f - 4, 8, 8, SettingsPalette.SoftAccent).raycastTarget =
          false;
      TextMeshProUGUI label = _ui.Text(
        options[i],
        row.transform,
        30,
        0,
        width - Padding * 2 - 30,
        Row - 2,
        14,
        i == request.Selected ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted
      );
      label.alignment = TextAlignmentOptions.MidlineLeft;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.overflowMode = TextOverflowModes.Ellipsis;
      label.raycastTarget = false;
    }
    // The picked option in view.
    if (scroll.vertical && request.Selected >= VisibleRows)
    {
      float hidden = Row * options.Count - listHeight;
      scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((request.Selected - VisibleRows + 1) * Row / hidden);
    }
    float top = anchor.yMax + 6;
    if (top + height > viewHeight - 8)
      top = anchor.y - 6 - height;
    SettingsUiFactory.Place(
      _root,
      Mathf.Clamp(anchor.x, 8, Mathf.Max(8, viewWidth - width - 8)),
      Mathf.Clamp(top, 8, Mathf.Max(8, viewHeight - height - 8)),
      width,
      height
    );
    _root.SetAsLastSibling();
  }

  public void Hide()
  {
    Anchor = null;
    _picked = null;
    if (_root == null)
      return;
    SettingsUiFactory.Destroy(_root.gameObject);
    _root = null;
  }

  public bool Contains(Vector2 screen) => Visible && OverlayEditorUi.Contains(_root, screen);

  private void Pick(int index)
  {
    Action<int> picked = _picked;
    Hide();
    picked?.Invoke(index);
  }
}
