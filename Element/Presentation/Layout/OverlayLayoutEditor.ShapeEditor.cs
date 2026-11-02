using System;
using Element.Configuration;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Shapes: picking a kind next to the add menu, and their editor.
internal sealed partial class OverlayLayoutEditor
{
  private const float ShapeMenuWidth = 220,
    ShapeMenuRow = 48;
  private OverlayShapeEditor _shapeEditor;
  private RectTransform _shapeMenu;
  private SettingsUiFactory _shapeMenuUi;

  private bool ContentEditorOpen => (_textEditor?.IsOpen ?? false) || (_shapeEditor?.IsOpen ?? false);

  /// <summary>Opens the editor for a text or a shape; false for other elements.</summary>
  private bool OpenContentEditor(IOverlayLayoutTarget target)
  {
    switch (target)
    {
      case TextOverlayElement text:
        OpenTextEditor(text, false);
        return true;
      case ShapeOverlayElement shape:
        OpenShapeEditor(shape, false);
        return true;
      default:
        return false;
    }
  }

  private void OpenShapeEditor(ShapeOverlayElement shape, bool isNew)
  {
    if (!CommitPopovers())
      return;
    ClosePopovers();
    FinishDrag();
    _toolbarDragging = false;
    _shapeEditor ??= new OverlayShapeEditor(_root.transform, _font, () => _preferences.ReduceUiMotion, _host.Values);
    _shapeEditor.Open(shape, committed => ShapeEditorClosed(shape, isNew, committed));
    UpdateFrames();
  }

  // Done keeps the shape (one undo step, with its placement when new); a new
  // shape that is cancelled is removed.
  private void ShapeEditorClosed(ShapeOverlayElement shape, bool isNew, bool committed)
  {
    if (committed)
      _history.Commit(_host.LayoutSettings);
    else if (isNew)
    {
      // Never placed, like a cancelled text, so undo has nothing to restore.
      if (shape.Settings != null)
        Domain.Elements.OverlayElementDefaults.ResetPlacement(shape.Settings);
      shape.SetEnabled(false);
      _selected = -1;
    }
    RefreshLayouts();
    UpdateFrames();
  }

  // Square, triangle or circle, beside the add menu's Shape row.
  private void ToggleShapeMenu()
  {
    if (_shapeMenu != null)
    {
      CloseShapeMenu();
      return;
    }
    _shapeMenuUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    float height = 16 + ShapeMenuRow * 3;
    Image panel = _shapeMenuUi.Panel(
      "Shape kind menu",
      _root.transform,
      0,
      0,
      ShapeMenuWidth,
      height,
      new Color(.105f, .105f, .105f)
    );
    panel.raycastTarget = true;
    _shapeMenu = panel.rectTransform;
    (OverlayShapeKind Kind, LayoutAction Icon, string Name)[] kinds =
    {
      (OverlayShapeKind.Square, LayoutAction.Square, L("사각형", "Square")),
      (OverlayShapeKind.Triangle, LayoutAction.Triangle, L("삼각형", "Triangle")),
      (OverlayShapeKind.Circle, LayoutAction.Circle, L("원", "Circle")),
    };
    Button first = null;
    for (int i = 0; i < kinds.Length; i++)
    {
      OverlayShapeKind kind = kinds[i].Kind;
      Button button = _shapeMenuUi.Button(
        "",
        _shapeMenu,
        8,
        8 + i * ShapeMenuRow,
        ShapeMenuWidth - 16,
        ShapeMenuRow - 4,
        () => AddShape(kind)
      );
      button.name = "Add " + kind;
      button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
      button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      LayoutActionIcon glyph = _shapeMenuUi
        .Rect("Icon", button.transform, 14, 11, 22, 22)
        .gameObject.AddComponent<LayoutActionIcon>();
      glyph.Kind = kinds[i].Icon;
      glyph.color = SettingsPalette.SoftAccent;
      glyph.raycastTarget = false;
      _shapeMenuUi.Text(kinds[i].Name, button.transform, 50, 0, ShapeMenuWidth - 76, ShapeMenuRow - 4, 15);
      first ??= button;
    }
    PlaceShapeMenu();
    UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  // Beside the add menu, bottoms aligned so it sits next to the Shape row and
  // clear of the toolbar; on the left when the view ends on the right.
  private void PlaceShapeMenu()
  {
    if (_shapeMenu == null || _addMenu == null)
      return;
    Rect menu = ScreenRect(_addMenu);
    float scale = _addMenu.localScale.x;
    float height = 16 + ShapeMenuRow * 3;
    float w = ShapeMenuWidth * scale,
      h = height * scale;
    float x = menu.xMax + 8;
    if (x + w > ViewWidth - 8)
      x = menu.x - w - 8;
    float y = menu.yMax - h;
    PlacePopover(
      _shapeMenu,
      new Rect(
        Mathf.Clamp(x, 8, Mathf.Max(8, ViewWidth - w - 8)),
        Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
        w,
        h
      ),
      ShapeMenuWidth,
      height
    );
  }

  private void CloseShapeMenu()
  {
    if (_shapeMenu == null)
      return;
    _shapeMenu.gameObject.SetActive(false);
    SettingsUiFactory.Destroy(_shapeMenu.gameObject);
    _shapeMenu = null;
    _shapeMenuUi?.Dispose();
    _shapeMenuUi = null;
  }
}
