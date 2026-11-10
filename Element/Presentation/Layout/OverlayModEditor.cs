using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit;
using ElementKit.Hosting;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// The editing screen for an element another mod draws. Element keeps what
/// every editing screen has: the dim, Cancel and Done, Esc to finish, undo,
/// Grow from, and a live preview, a second instance of the mod's element
/// that shows each change as it happens. The mod builds the rest from
/// <see cref="ModEditorPage"/>'s parts.
/// </summary>
internal sealed class OverlayModEditor : IDisposable
{
  private const float BarHeight = 40;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly Func<OverlayFonts> _fonts;
  private readonly Action<ElementClass, string, Exception> _report;
  private readonly HashSet<string> _failed = new();
  private readonly List<string> _undo = new();
  private readonly List<string> _redo = new();
  private readonly Event _event = new();
  private OverlayEditorFrame _screen;
  private SettingsUiFactory _ui;
  private RectTransform _frame;
  private RectTransform _bar;
  private OverlayBarButton _growButton;
  private OverlayGrowPopover _growPopover;
  private SettingsCapsuleGraphic _growMark;
  private SettingsColorPicker _picker;
  private ModColorRequest _pickerFor;
  private RectTransform _previewHolder;
  private RectTransform _previewRoot;
  private ModDrawing _drawing;
  private OverlayElement _instance;
  private ModEditorPage _page;
  private ModOverlayElement _element;
  private ElementClass _type;
  private Action<bool> _closed;
  private Rect _previewArea;
  private string _opening;
  private string _working;
  private OverlayGrowth _growth;
  private object _lastKey;
  private float _lastAt = -1;
  private float _elementScale = 1;
  private Vector2 _boxTopLeft;
  private float _unitsToFrame = 1;

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
    _report = report;
  }

  public bool IsOpen => _screen != null;

  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  /// <summary>The page the mod built last; for checks.</summary>
  internal ModEditorPage Page => _page;

  /// <summary>Opens <paramref name="element"/>; false when its mod can't draw it now.</summary>
  public bool Open(ModOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    if (element?.Class == null || !element.Drawn || element.Settings == null)
      return false;
    _element = element;
    _type = element.Class;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _opening = element.Settings.Mod?.Data ?? string.Empty;
    _working = _opening;
    _growth = element.Settings.GrowFrom;
    _undo.Clear();
    _redo.Clear();
    _failed.Clear();
    _lastKey = null;
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
    _ui = _screen.Ui;
    _frame = _screen.Frame;
    BuildBar();
    float top = OverlayEditorFrame.TopBar + 8 + BarHeight + 24;
    float height = Mathf.Clamp(_screen.Height * .3f, 120, 280);
    _previewArea = new Rect(OverlayEditorFrame.Margin, top, _screen.Width - OverlayEditorFrame.Margin * 2, height);
    _previewHolder = _ui.Rect("Preview", _screen.Stage, 0, 0, 10, 10);
    _previewHolder.anchorMin = _previewHolder.anchorMax = _previewHolder.pivot = new Vector2(0, 1);
    _drawing = new ModDrawing(_fonts?.Invoke());
    _picker = new SettingsColorPicker(_frame, _font, _reduceMotion);
    _growPopover = new OverlayGrowPopover(_ui, _frame, _reduceMotion(), PickGrowth);
    _growMark = _ui.Capsule("Grow point", _frame, 0, 0, 12, 12, SettingsPalette.SoftAccent);
    _growMark.gameObject.SetActive(false);
    if (!CreatePreview())
      return;
    BuildPage();
    FitPreview();
  }

  private void BuildBar()
  {
    SettingsCapsuleGraphic surface = _ui.Capsule("Element bar", _frame, 0, 0, 10, BarHeight, OverlayEditorUi.Bar);
    surface.raycastTarget = true;
    _bar = surface.rectTransform;
    _growButton = new OverlayBarButton(
      _ui,
      _bar,
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      _reduceMotion(),
      icon: LayoutAction.GrowFrom
    );
  }

  // A second instance of the mod's element, with the settings being edited.
  private bool CreatePreview()
  {
    OverlayElement instance = _type.Create(out Exception error);
    if (instance == null)
    {
      Failed("being made", error);
      return false;
    }
    GameObject root = new("Mod drawing", typeof(RectTransform));
    _previewRoot = root.GetComponent<RectTransform>();
    _previewRoot.SetParent(_previewHolder, false);
    ElementKit.Drawing.Place(
      _previewRoot,
      0,
      0,
      Mathf.Max(1, _element.Settings.Mod.Width),
      Mathf.Max(1, _element.Settings.Mod.Height)
    );
    _instance = instance;
    instance.Bind(_previewRoot, _drawing, true);
    return Run(
      "starting",
      () =>
      {
        instance.RunLoad(_working);
        instance.RunCreate();
        instance.RunSettingsChanged();
      }
    );
  }

  // The mod's parts, laid out fresh; again after undo so they show the restored settings.
  private void BuildPage()
  {
    if (_page != null)
      SettingsUiFactory.Destroy(_page.Root.gameObject);
    float flowTop = _previewArea.yMax + 28;
    _page = new ModEditorPage(
      _ui,
      _frame,
      new Vector2(_screen.Width, _screen.Height),
      _previewArea,
      new Rect(
        OverlayEditorFrame.Margin,
        flowTop,
        _screen.Width - OverlayEditorFrame.Margin * 2,
        _screen.Height - flowTop - OverlayEditorFrame.Margin
      ),
      _reduceMotion(),
      Change,
      OpenColor
    );
    Run("laying out its editing screen", () => _instance.RunEdit(_page));
    _page.Layout();
    _picker.Root.SetAsLastSibling();
    _growPopover.Root.SetAsLastSibling();
  }

  /// <summary>
  /// One edit: what it changes is one undo step (a part's edits within a
  /// second merge), and the preview shows it right away. A button may change
  /// several settings, so the parts are made again to show them.
  /// </summary>
  private void Change(object key, Action apply, bool rebuild)
  {
    if (_instance == null)
      return;
    string before = _working;
    if (!Run("changing a setting", apply))
      return;
    string after = Save();
    if (after != before)
    {
      // Typing in a field is one step for as long as it stays focused.
      bool typing = key is TMPro.TMP_InputField field && field.isFocused;
      bool merge = key != null && key == _lastKey && (typing || Time.unscaledTime - _lastAt < 1f) && _undo.Count > 0;
      if (!merge)
      {
        _undo.Add(before);
        if (_undo.Count > 100)
          _undo.RemoveAt(0);
      }
      _redo.Clear();
      _working = after;
    }
    _lastKey = key;
    _lastAt = Time.unscaledTime;
    Run("showing a setting", _instance.RunSettingsChanged);
    FitPreview();
    if (rebuild && after != before)
      BuildPage();
  }

  private string Save()
  {
    string data = _working;
    Run("saving its settings", () => data = _instance.RunSave());
    return data ?? string.Empty;
  }

  private void OpenColor(ModColorRequest request)
  {
    CloseGrow();
    if (_picker.Visible && _pickerFor != null && _pickerFor.Anchor == request.Anchor)
    {
      _picker.Hide();
      return;
    }
    _pickerFor = request;
    _picker.Show(request.Title, request.Color, request.Opacity, request.Changed);
    Rect anchor = _screen.FrameRect(request.Anchor);
    float width = _picker.CurrentWidth,
      height = _picker.Height;
    float x = anchor.xMax + 16;
    if (x + width > _screen.Width - 8)
      x = anchor.x - width - 16;
    float y = Mathf.Clamp(anchor.center.y - height * .5f, 8, Mathf.Max(8, _screen.Height - height - 8));
    SettingsUiFactory.Place(_picker.Root, Mathf.Clamp(x, 8, Mathf.Max(8, _screen.Width - width - 8)), y, width, height);
    _picker.Root.SetAsLastSibling();
  }

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
    if (_instance != null)
    {
      // A preview that fails to update stays as it last drew.
      if (!_failed.Contains("updating"))
        Run("updating", _instance.RunUpdate);
      _drawing.Tick(_unitsToFrame * _screen.Scale);
      FitPreview();
    }
    // Typing goes to the field; Esc there only ends the typing.
    if ((_picker.Visible && _picker.IsTyping) || (_page != null && _page.Typing))
      return;
    HandleMouse();
    HandleKeys();
  }

  // The element at its in-game size, made smaller only to fit its area.
  private void FitPreview()
  {
    if (_previewRoot == null)
      return;
    Vector2 size = _previewRoot.sizeDelta;
    size = new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    float units = _elementScale * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height) / _screen.Scale;
    float fit = Mathf.Min(1, _previewArea.width / (size.x * units), _previewArea.height / (size.y * units));
    _unitsToFrame = units * fit;
    Vector2 box = size * _unitsToFrame;
    _boxTopLeft = new Vector2(_previewArea.center.x - box.x * .5f, _previewArea.center.y - box.y * .5f);
    float pixels = _unitsToFrame * _screen.Scale;
    _previewHolder.anchoredPosition = new Vector2(_boxTopLeft.x * _screen.Scale, -_boxTopLeft.y * _screen.Scale);
    _previewHolder.sizeDelta = size;
    _previewHolder.localScale = new Vector3(pixels, pixels, 1);
    float barWidth = (BarHeight - OverlayBarButton.Height) + _growButton.Width;
    _growButton.Set(OverlayGrowPicker.ShortName(_growth), false, _growPopover.Visible);
    _growButton.Place((BarHeight - OverlayBarButton.Height) * .5f, (BarHeight - OverlayBarButton.Height) * .5f);
    SettingsUiFactory.Place(_bar, (_screen.Width - barWidth) * .5f, OverlayEditorFrame.TopBar + 8, barWidth, BarHeight);
    PlaceGrowMark(box);
  }

  private void PlaceGrowMark(Vector2 box)
  {
    bool show = _growPopover.Visible;
    if (_growMark.gameObject.activeSelf != show)
      _growMark.gameObject.SetActive(show);
    if (!show)
      return;
    Anchor9 point = GrowPointNow();
    Vector2 at =
      _boxTopLeft
      + new Vector2(box.x * OverlayLayoutPosition.AnchorX(point), box.y * OverlayLayoutPosition.AnchorY(point));
    SettingsUiFactory.Place(_growMark.rectTransform, at.x - 6, at.y - 6, 12, 12);
    _growMark.transform.SetAsLastSibling();
  }

  private void ToggleGrow()
  {
    if (_growPopover.Visible)
    {
      CloseGrow();
      return;
    }
    _picker.Hide();
    _growPopover.Show(_screen.FrameRect(_growButton.Root), _screen.Width, _screen.Height);
    _growPopover.Refresh(_growth, GrowPointNow());
  }

  private void PickGrowth(OverlayGrowth growth)
  {
    _growth = growth;
    _growPopover.Refresh(_growth, GrowPointNow());
  }

  private void CloseGrow() => _growPopover?.Hide();

  private Anchor9 GrowPointNow() =>
    OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  private void HandleKeys()
  {
    bool mac =
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;
    while (_screen != null && Event.PopEvent(_event))
    {
      if (_event.rawType != EventType.KeyDown)
        continue;
      bool command = mac ? _event.command : _event.control;
      if (command)
      {
        if (_event.keyCode == KeyCode.Return || _event.keyCode == KeyCode.KeypadEnter)
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
      // Esc closes what is open; with nothing open it finishes like Done.
      // Only Cancel leaves without saving.
      if (_picker.Visible)
        _picker.Hide();
      else if (_growPopover.Visible)
        CloseGrow();
      else
      {
        Close(true, true);
        return;
      }
    }
  }

  // A click away from the picker or Grow from closes it.
  private void HandleMouse()
  {
    if (!Input.GetMouseButtonDown(0))
      return;
    Vector2 screen = Input.mousePosition;
    if (_picker.Visible && !OverlayEditorUi.Contains(_picker.Root, screen) && !Over(_pickerFor?.Anchor, screen))
      _picker.Hide();
    if (_growPopover.Visible && !_growPopover.Contains(screen) && !Over(_growButton.Root, screen))
      CloseGrow();
  }

  private static bool Over(RectTransform rect, Vector2 screen) =>
    rect != null && OverlayEditorUi.Contains(rect, screen);

  private void Undo()
  {
    if (_undo.Count == 0)
      return;
    _redo.Add(_working);
    Restore(_undo[_undo.Count - 1]);
    _undo.RemoveAt(_undo.Count - 1);
  }

  private void Redo()
  {
    if (_redo.Count == 0)
      return;
    _undo.Add(_working);
    Restore(_redo[_redo.Count - 1]);
    _redo.RemoveAt(_redo.Count - 1);
  }

  private void Restore(string data)
  {
    _working = data;
    _lastKey = null;
    _picker.Hide();
    Run(
      "loading its settings",
      () =>
      {
        _instance.RunLoad(data);
        _instance.RunSettingsChanged();
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
    if (commit && _picker.Visible && !_picker.Commit())
      return;
    ModOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null)
    {
      if (_instance != null)
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
    _closed = null;
    UIInteractionState.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  private void TearDown()
  {
    if (_instance != null)
      Run("going away", _instance.RunDestroy);
    _instance = null;
    _drawing?.Clear();
    _drawing = null;
    _page = null;
    _previewRoot = null;
    _pickerFor = null;
    SettingsColorPicker picker = _picker;
    _picker = null;
    picker?.Dispose();
    _screen?.Dispose();
    _screen = null;
    _ui = null;
  }

  // A mod's code runs here; its failure shows as a notice and is logged once.
  private bool Run(string doing, Action action)
  {
    try
    {
      action();
      return true;
    }
    catch (Exception exception)
    {
      Failed(doing, exception);
      return false;
    }
  }

  private void Failed(string doing, Exception exception)
  {
    if (!_failed.Add(doing))
      return;
    _report?.Invoke(_type, doing + " in its editing screen", exception);
    _screen?.Notice(
      L(
        _type.ModId
          + " 모드가 이 요소를 "
          + KoreanDoing(doing)
          + " 오류가 났어요. 모드를 업데이트하거나 만든 분께 알려 주세요.",
        _type.ModId + " stopped while " + doing + ". Update the mod or tell its author."
      )
    );
  }

  private static string KoreanDoing(string doing) =>
    doing switch
    {
      "being made" or "starting" => "여는 중에",
      "laying out its editing screen" => "편집 화면을 만드는 중에",
      "updating" => "그리는 중에",
      _ => "바꾸는 중에",
    };

  // Torn down while open (the mod or Element turned off), the edit is kept.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }
}
