using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// A second toolbar that follows a selected key viewer with hand or foot. Texts
// and shapes have none: the main toolbar's Edit button, a double-click or
// Enter opens their editor. It drags by its grip like the main toolbar and
// keeps its place; it never blocks the canvas.
internal sealed partial class OverlayLayoutEditor
{
  private const float ElementBarHeight = 56;
  private RectTransform _settingsPanel;
  private SettingsUiFactory _settingsUi;
  private IOverlayLayoutTarget _settingsTarget;
  private SettingsSegmentedControl _kindControl;
  private readonly List<RectTransform> _elementBarControls = new();
  private float _settingsWidth,
    _settingsHeight;
  private bool _settingsStale;
  private bool _elementBarPlaced,
    _elementBarDragging;
  private Vector2 _elementBarPosition,
    _elementBarGrab;

  // Shows the toolbar for the selected element, rebuilt when the selection or
  // its settings change; hidden while the element is being dragged.
  private void SyncElementSettings()
  {
    IOverlayLayoutTarget target = HasSelection ? _frames[_selected].Target : null;
    if (target == null || _modalRoot != null || ContentEditorOpen)
    {
      CloseElementSettings();
      return;
    }
    if (_settingsTarget != target || _settingsStale)
    {
      _settingsStale = false;
      if (OverlayElementKinds.IsKeyViewer(target.TemplateId))
        BuildKeyViewerSettings(target);
      else
        CloseElementSettings();
    }
    if (_settingsPanel == null)
      return;
    bool hidden = (_dragging >= 0 && _pressMoved) || _addMenu != null || (_colorPopover?.Visible ?? false);
    if (_settingsPanel.gameObject.activeSelf == hidden)
      _settingsPanel.gameObject.SetActive(!hidden);
    UpdateSettingsPosition();
  }

  private bool SettingsPanelContains(Vector2 point) =>
    _settingsPanel != null && _settingsPanel.gameObject.activeSelf && ElementBarBounds.Contains(point);

  // Above the main toolbar until it is dragged somewhere else.
  private Rect ElementBarBounds
  {
    get
    {
      float width = _settingsWidth * ToolbarScale,
        height = _settingsHeight * ToolbarScale;
      Rect toolbar = ToolbarBounds;
      Vector2 position = _elementBarPlaced
        ? _elementBarPosition
        : new Vector2(toolbar.center.x - width * .5f, toolbar.y - height - 10);
      if (!_elementBarPlaced && position.y < 0)
        position.y = toolbar.yMax + 10;
      position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, ViewWidth - width));
      position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, ViewHeight - height));
      return new Rect(position, new Vector2(width, height));
    }
  }

  private bool ElementBarControlAt(Vector2 point)
  {
    Vector2 screen = new(point.x, ViewHeight - point.y);
    foreach (RectTransform control in _elementBarControls)
      if (
        control != null
        && control.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(control, screen, null)
      )
        return true;
    return false;
  }

  private void BeginElementBarDrag(Vector2 pointer)
  {
    _elementBarPosition = ElementBarBounds.position;
    _elementBarPlaced = _elementBarDragging = true;
    _elementBarGrab = pointer - _elementBarPosition;
  }

  private void MoveElementBar(Vector2 pointer)
  {
    _elementBarPosition = pointer - _elementBarGrab;
    _elementBarPosition = ElementBarBounds.position;
    UpdateSettingsPosition();
  }

  private RectTransform BeginSettings(IOverlayLayoutTarget target, float width)
  {
    CloseElementSettings();
    _settingsTarget = target;
    _settingsWidth = width;
    _settingsHeight = ElementBarHeight;
    _settingsUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    Image panel = _settingsUi.Panel(
      "Element toolbar",
      _root.transform,
      0,
      0,
      width,
      ElementBarHeight,
      new Color(.07f, .07f, .07f, 1)
    );
    panel.raycastTarget = true;
    _settingsPanel = panel.rectTransform;
    LayoutActionIcon grip = _settingsUi
      .Rect("Drag element toolbar", _settingsPanel, 10, 16, 14, 24)
      .gameObject.AddComponent<LayoutActionIcon>();
    grip.Kind = LayoutAction.Grip;
    grip.color = SettingsUiFactory.Muted;
    grip.raycastTarget = false;
    return _settingsPanel;
  }

  private T Control<T>(T control)
    where T : Component
  {
    _elementBarControls.Add((RectTransform)control.transform);
    return control;
  }

  private void Divider(float x) =>
    _settingsUi.Panel("Divider", _settingsPanel, x, 16, 1, 24, new Color(1, 1, 1, .1f), false);

  // Errors share the toolbar's help line, where scale errors already appear.
  private void ShowEditorError(string message)
  {
    _keyboardHelp.text = message;
    _scaleErrorUntil = Time.unscaledTime + 5;
  }

  private void BuildKeyViewerSettings(IOverlayLayoutTarget target)
  {
    RectTransform panel = BeginSettings(target, 216);
    _kindControl = new SettingsSegmentedControl(
      _settingsUi,
      panel,
      32,
      8,
      172,
      40,
      new[] { L("손", "Hand"), L("발", "Foot") },
      index => SwitchKeyViewer(index == 0 ? OverlayElementKinds.HandKeyViewer : OverlayElementKinds.FootKeyViewer)
    );
    _elementBarControls.Add(_kindControl.Root);
    _kindControl.Selected = target.TemplateId == OverlayElementKinds.FootKeyViewer ? 1 : 0;
  }

  private void SwitchKeyViewer(string templateId)
  {
    IOverlayLayoutTarget old = _settingsTarget;
    if (old == null || old.TemplateId == templateId)
      return;
    IOverlayLayoutTarget created = _keyViewers?.Switch(old, templateId);
    if (created == null)
    {
      _kindControl.Selected = old.TemplateId == OverlayElementKinds.FootKeyViewer ? 1 : 0;
      ShowEditorError(
        L(
          "키뷰어를 바꾸지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't switch the key viewer. Check that ElementNote is open."
        )
      );
      return;
    }
    (old as IExternalLayoutOutline)?.SetSelected(false);
    created.SetPreview(true);
    if (IndexOf(created) < 0)
      AddFrame(created);
    _selected = IndexOf(created);
    _settingsTarget = created;
    _history.Commit(_host.LayoutSettings);
    RefreshLayouts();
    UpdateFrames();
  }

  private void UpdateSettingsPosition()
  {
    if (_settingsPanel != null)
      PlacePopover(_settingsPanel, ElementBarBounds, _settingsWidth, _settingsHeight);
  }

  private void CloseElementSettings()
  {
    _elementBarDragging = false;
    if (_settingsPanel == null)
      return;
    _settingsPanel.gameObject.SetActive(false);
    SettingsUiFactory.Destroy(_settingsPanel.gameObject);
    _settingsPanel = null;
    _settingsUi?.Dispose();
    _settingsUi = null;
    _settingsTarget = null;
    _kindControl = null;
    _elementBarControls.Clear();
  }
}
