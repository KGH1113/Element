using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit.Hosting;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// The editing screen for an element another mod draws. Element keeps what
/// every editing screen has: the dim, Cancel and Done, Esc to finish, undo,
/// Grow from, and a live preview (<see cref="ModPreview"/>). The mod builds
/// the rest from <see cref="ModEditorPage"/>'s parts, and the editor opens
/// what those parts ask for beside them (<see cref="ModEditorPopovers"/>).
/// </summary>
internal sealed class OverlayModEditor : IModEditorHost, IContentEditor
{
  private const float BarHeight = 40;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly Func<OverlayFonts> _fonts;
  private readonly ModFailureGuard _guard;
  private readonly ModPreview _preview;
  private readonly UndoStack<string> _history = new(100);
  private readonly Dictionary<string, int> _tabs = new();
  private readonly Event _event = new();
  private OverlayEditorFrame _screen;
  private RectTransform _bar;
  private OverlayBarButton _growButton;
  private ModEditorPopovers _popovers;
  private ModEditorPage _page;
  private ModOverlayElement _element;
  private ElementClass _type;
  private Action<bool> _closed;
  private string _opening;
  private string _working;
  private OverlayGrowth _growth;

  /// <param name="report">Logs a mod's failure once per kind; see <see cref="ModElements.Failed"/>.</param>
  public OverlayModEditor(
    Transform parent,
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    Func<OverlayFonts> fonts,
    Action<ElementClass, string, Exception> report
  )
  {
    _parent = parent;
    _font = font;
    _reduceMotion = reduceMotion;
    _fonts = fonts;
    _guard = new ModFailureGuard(report);
    _preview = new ModPreview(_guard);
  }

  public bool IsOpen => _screen != null;

  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  /// <summary>The page the mod built last; for checks.</summary>
  internal ModEditorPage Page => _page;

  bool IContentEditor.Open(IOverlayLayoutTarget target, Action<bool> closed) =>
    target is ModOverlayElement element && Open(element, closed);

  /// <summary>Opens <paramref name="element"/>; false when its mod can't draw it now.</summary>
  public bool Open(ModOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    if (element?.Class == null || !element.Drawn || element.Settings == null)
      return false;
    _element = element;
    _type = element.Class;
    _guard.Type = _type;
    _closed = closed;
    _preview.ElementScale = Mathf.Max(.01f, element.EffectiveScale);
    _opening = element.Settings.Mod?.Data ?? string.Empty;
    _working = _opening;
    _growth = element.Settings.GrowFrom;
    _history.Clear();
    _guard.Clear();
    _tabs.Clear();
    Build();
    return true;
  }

  private string TypeName => L(_type.Info.KoreanName, _type.Info.EnglishName);

  private void Build()
  {
    TearDown();
    _screen = new OverlayEditorFrame(
      "Mod element editor",
      _parent,
      _font,
      _reduceMotion,
      L(TypeName + " 편집 · ⌘/Ctrl+Enter나 Esc로 완료", "Edit " + TypeName + " · ⌘/Ctrl+Enter or Esc to finish"),
      () => Close(false, true),
      () => Close(true, true)
    );
    _guard.Screen = _screen;
    SettingsUiFactory ui = _screen.Ui;
    BuildBar(ui);
    float top = OverlayEditorFrame.TopBar + 8 + BarHeight + 24;
    float height = Mathf.Clamp(_screen.Height * .3f, 120, 280);
    _preview.BuildHolder(
      ui,
      _screen,
      new Rect(OverlayEditorFrame.Margin, top, _screen.Width - OverlayEditorFrame.Margin * 2, height),
      _fonts?.Invoke()
    );
    _popovers = new ModEditorPopovers(_screen, _font, _reduceMotion, _fonts, _growButton, PickGrowth);
    _preview.BuildGrowMark(ui, _screen.Frame);
    if (!_preview.Create(_type, _element.Settings.Mod, _working))
      return;
    BuildPage();
    FitPreview();
  }

  private void BuildBar(SettingsUiFactory ui)
  {
    SettingsCapsuleGraphic surface = ui.Capsule("Element bar", _screen.Frame, 0, 0, 10, BarHeight, OverlayEditorUi.Bar);
    surface.raycastTarget = true;
    _bar = surface.rectTransform;
    _growButton = new OverlayBarButton(
      ui,
      _bar,
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      _reduceMotion(),
      icon: LayoutAction.GrowFrom
    );
  }

  // The mod's parts, laid out fresh; again after undo so they show the restored settings.
  private void BuildPage()
  {
    if (_page != null)
      SettingsUiFactory.Destroy(_page.Root.gameObject);
    Rect preview = _preview.Area;
    float flowTop = preview.yMax + 28;
    _page = new ModEditorPage(
      _screen.Ui,
      _screen.Frame,
      new Vector2(_screen.Width, _screen.Height),
      preview,
      new Rect(
        OverlayEditorFrame.Margin,
        flowTop,
        _screen.Width - OverlayEditorFrame.Margin * 2,
        _screen.Height - flowTop - OverlayEditorFrame.Margin
      ),
      _reduceMotion(),
      this
    );
    _guard.Run("laying out its editing screen", () => _preview.Instance.RunEdit(_page));
    _page.Layout();
    _popovers.Raise();
  }

  OverlayFonts IModEditorHost.Fonts => _fonts?.Invoke();

  int IModEditorHost.TabOf(string key) => _tabs.TryGetValue(key, out int index) ? index : 0;

  void IModEditorHost.SetTab(string key, int index) => _tabs[key] = index;

  void IModEditorHost.ShowTip(string text, RectTransform anchor)
  {
    if (_screen == null || anchor == null)
      return;
    Rect rect = _screen.FrameRect(anchor);
    _screen.ShowTip(text, new Vector2(rect.center.x, rect.y));
  }

  void IModEditorHost.HideTip() => _screen?.HideTip();

  /// <summary>
  /// One edit: what it changes is one undo step (a part's edits within a
  /// second merge), and the preview shows it right away. A button may change
  /// several settings, so the parts are made again to show them.
  /// </summary>
  public void Change(object key, Action apply, bool rebuild)
  {
    if (_preview.Instance == null)
      return;
    string before = _working;
    if (!_guard.Run("changing a setting", apply))
      return;
    string after = Save();
    // Typing in a field is one step for as long as it stays focused.
    bool typing = key is TMP_InputField field && field.isFocused;
    bool merge = _history.Continues(key, Time.unscaledTime, typing);
    if (after != before)
    {
      _history.Record(before, merge);
      _history.DropRedo();
      _working = after;
    }
    _guard.Run("showing a setting", _preview.Instance.RunSettingsChanged);
    FitPreview();
    if (rebuild && after != before)
      BuildPage();
  }

  private string Save()
  {
    string data = _working;
    _guard.Run("saving its settings", () => data = _preview.Instance.RunSave());
    return data ?? string.Empty;
  }

  public void OpenColor(ModColorRequest request) => _popovers.OpenColor(request);

  public void OpenPaint(ModPaintRequest request) => _popovers.OpenPaint(request);

  public void OpenList(ModListRequest request) => _popovers.OpenList(request);

  public void OpenFont(ModFontRequest request) => _popovers.OpenFont(request);

  private void ClosePopovers() => _popovers.CloseAll();

  public void Tick()
  {
    if (_screen == null)
      return;
    if (_screen.ViewChanged)
    {
      Build();
      if (_screen == null)
        return;
    }
    _screen.Tick();
    _page?.Tick();
    _popovers.Tick();
    if (_preview.Instance != null)
    {
      _preview.Tick(_screen.Scale);
      FitPreview();
    }
    // Typing goes to the field; Esc there only ends the typing.
    if (_popovers.Typing || (_page != null && _page.Typing))
      return;
    if (Input.GetMouseButtonDown(0))
      _popovers.PressOutside(Input.mousePosition);
    HandleKeys();
  }

  // The preview, and the bar above it with Grow from.
  private void FitPreview()
  {
    if (!_preview.Fit(_screen.Scale, out Vector2 box))
      return;
    float barWidth = (BarHeight - OverlayBarButton.Height) + _growButton.Width;
    _growButton.Set(OverlayGrowPicker.ShortName(_growth), false, _popovers.GrowOpen);
    _growButton.Place((BarHeight - OverlayBarButton.Height) * .5f, (BarHeight - OverlayBarButton.Height) * .5f);
    SettingsUiFactory.Place(_bar, (_screen.Width - barWidth) * .5f, OverlayEditorFrame.TopBar + 8, barWidth, BarHeight);
    bool grow = _popovers.GrowOpen;
    _preview.PlaceGrowMark(box, grow, grow ? GrowPointNow() : default);
  }

  private void ToggleGrow() => _popovers.ToggleGrow(_growth, GrowPointNow());

  private void PickGrowth(OverlayGrowth growth)
  {
    _growth = growth;
    _popovers.RefreshGrow(_growth, GrowPointNow());
  }

  private Anchor9 GrowPointNow() =>
    OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  /// <summary>
  /// Esc closes what is open; with nothing open it finishes like Done. Only
  /// Cancel leaves without saving.
  /// </summary>
  public void Escape()
  {
    if (_popovers.AnyOpen)
      ClosePopovers();
    else
      Close(true, true);
  }

  private void HandleKeys()
  {
    bool mac = EditorKeys.Mac;
    while (_screen != null && EditorKeys.NextKeyDown(_event))
    {
      if (EditorKeys.Command(_event, mac))
      {
        if (EditorKeys.IsEnter(_event.keyCode))
        {
          Close(true, true);
          return;
        }
        if (_event.keyCode == KeyCode.Z)
        {
          if (_event.shift)
            Redo();
          else
            Undo();
        }
        else if (_event.keyCode == KeyCode.Y)
          Redo();
        continue;
      }
      if (_event.keyCode != KeyCode.Escape)
        continue;
      bool finishing = !_popovers.AnyOpen;
      Escape();
      if (finishing)
        return;
    }
  }

  private void Undo()
  {
    if (_history.Undo(_working, out string data))
      Restore(data);
  }

  private void Redo()
  {
    if (_history.Redo(_working, out string data))
      Restore(data);
  }

  private void Restore(string data)
  {
    _working = data;
    _history.Break();
    ClosePopovers();
    _guard.Run(
      "loading its settings",
      () =>
      {
        _preview.Instance.RunLoad(data);
        _preview.Instance.RunSettingsChanged();
      }
    );
    FitPreview();
    BuildPage();
  }

  /// <summary>Done saves the settings and where the element grows from; Cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && !_popovers.Commit())
      return;
    ModOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null)
    {
      if (_preview.Instance != null)
        _working = Save();
      if (_working != _opening)
      {
        element.Settings.Mod.Data = _working;
        element.Apply();
      }
      element.SetGrowFrom(_growth);
    }
    TearDown();
    _element = null;
    _type = null;
    _guard.Type = null;
    _closed = null;
    UIInteractionState.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  private void TearDown()
  {
    _preview.TearDown();
    _page = null;
    _popovers?.Dispose();
    _popovers = null;
    _screen?.Dispose();
    _screen = null;
    _guard.Screen = null;
  }

  // Torn down while open (the mod or Element turned off), the edit is kept.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }
}
