using System.Collections.Generic;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Settings;
using ElementKit.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Elements other mods add: their list beside the add menu's More row, and
// their editing screen.
internal sealed partial class OverlayLayoutEditor
{
  private const float ModMenuWidth = 240,
    ModMenuRow = 56;
  private const int ModMenuRows = 6;
  private OverlayPropertyEditor _propertyEditor;
  private RectTransform _modMenu;
  private SettingsUiFactory _modMenuUi;

  private int OfferedModElements
  {
    get
    {
      ModElements types = _host.ModElements;
      if (types == null)
        return 0;
      types.Refresh();
      return types.Offered.Count;
    }
  }

  private float ModMenuHeight => 16 + ModMenuRow * Mathf.Min(ModMenuRows, Mathf.Max(1, OfferedModElements));

  // Each type with the mod it comes from, beside the More row.
  private void ToggleModMenu()
  {
    if (_modMenu != null)
    {
      CloseModMenu();
      return;
    }
    CloseShapeMenu();
    ModElements types = _host.ModElements;
    if (types == null)
      return;
    types.Refresh();
    IReadOnlyList<ElementType> offered = types.Offered;
    _modMenuUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    float height = ModMenuHeight;
    Image panel = _modMenuUi.Panel(
      "Mod element menu",
      _root.transform,
      0,
      0,
      ModMenuWidth,
      height,
      new Color(.105f, .105f, .105f)
    );
    panel.raycastTarget = true;
    _modMenu = panel.rectTransform;
    Button first = null;
    for (int i = 0; i < offered.Count && i < ModMenuRows; i++)
    {
      ElementType type = offered[i];
      Button button = _modMenuUi.Button(
        "",
        _modMenu,
        8,
        8 + i * ModMenuRow,
        ModMenuWidth - 16,
        ModMenuRow - 4,
        () => AddModElement(type)
      );
      button.name = "Add " + type.FullKey;
      button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
      button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      LayoutActionIcon glyph = _modMenuUi
        .Rect("Icon", button.transform, 14, 15, 22, 22)
        .gameObject.AddComponent<LayoutActionIcon>();
      glyph.Kind = LayoutAction.Extension;
      glyph.color = SettingsPalette.SoftAccent;
      glyph.raycastTarget = false;
      TextMeshProUGUI name = _modMenuUi.Text(
        L(type.KoreanName, type.EnglishName),
        button.transform,
        50,
        6,
        ModMenuWidth - 76,
        22,
        15
      );
      name.textWrappingMode = TextWrappingModes.NoWrap;
      TextMeshProUGUI mod = _modMenuUi.Text(
        type.ModId,
        button.transform,
        50,
        28,
        ModMenuWidth - 76,
        18,
        12,
        SettingsUiFactory.Muted
      );
      mod.textWrappingMode = TextWrappingModes.NoWrap;
      first ??= button;
    }
    PlaceModMenu();
    if (first != null)
      EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  // Beside the add menu, bottoms aligned so it sits next to the More row.
  private void PlaceModMenu()
  {
    if (_modMenu == null || _addMenu == null)
      return;
    Rect menu = ScreenRect(_addMenu);
    float scale = _addMenu.localScale.x;
    float height = ModMenuHeight;
    float w = ModMenuWidth * scale,
      h = height * scale;
    float x = menu.xMax + 8;
    if (x + w > ViewWidth - 8)
      x = menu.x - w - 8;
    float y = menu.yMax - h;
    PlacePopover(
      _modMenu,
      new Rect(
        Mathf.Clamp(x, 8, Mathf.Max(8, ViewWidth - w - 8)),
        Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
        w,
        h
      ),
      ModMenuWidth,
      height
    );
  }

  private void CloseModMenu()
  {
    if (_modMenu == null)
      return;
    _modMenu.gameObject.SetActive(false);
    SettingsUiFactory.Destroy(_modMenu.gameObject);
    _modMenu = null;
    _modMenuUi?.Dispose();
    _modMenuUi = null;
  }

  // Like a shape: placed in the middle, its editor open right away.
  private void AddModElement(ElementType type)
  {
    CloseAddMenu();
    ModOverlayElement element = _host.AddModElement(type);
    if (element == null)
      return;
    element.SetPreview(true);
    PlaceNewElement(element, false);
    // History records the element when its editor is done; cancelling removes it.
    OpenPropertyEditor(element, true);
  }

  private void OpenPropertyEditor(ModOverlayElement element, bool isNew)
  {
    if (!CommitPopovers())
      return;
    ClosePopovers();
    FinishDrag();
    _toolbarDragging = false;
    _propertyEditor ??= new OverlayPropertyEditor(
      _root.transform,
      _font,
      () => _preferences.ReduceUiMotion,
      _host.Values,
      () => _host.Fonts
    );
    if (!_propertyEditor.Open(element, committed => PropertyEditorClosed(element, isNew, committed)))
    {
      // Its mod is missing, off or failing: say which and what to do.
      _keyboardHelp.text = element.Unavailable;
      _scaleErrorUntil = Time.unscaledTime + 5;
      _helpIsNote = false;
      return;
    }
    UpdateFrames();
  }

  private void PropertyEditorClosed(ModOverlayElement element, bool isNew, bool committed)
  {
    if (committed)
      _history.Commit(_host.LayoutSettings);
    else if (isNew)
    {
      if (element.Settings != null)
        Domain.Elements.OverlayElementDefaults.ResetPlacement(element.Settings);
      element.SetEnabled(false);
      _selected = -1;
    }
    RefreshLayouts();
    UpdateFrames();
  }
}
