using System;
using System.Collections.Generic;
using Element.Application.Input;
using Element.Application.Layout;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorBar;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using ElementUI;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// The editing screen for an element another mod draws, in the frame of the
/// text and shape editors: the dim, Cancel and Done, Esc to finish, undo,
/// Grow from over a live preview (<see cref="ModPreview"/>), and on the
/// right the property column where the text editor keeps its own
/// (<see cref="ModEditorColumn"/>). In the column goes what the mod made,
/// drawn as the column's rows: the parts of an <see cref="EditorPage"/>
/// (<see cref="ModEditorPage"/>), or a screen it wrote with ElementUI
/// (<see cref="ModViewPanel"/>). The preview keeps to the room left of the
/// column, gliding aside while a wide tool (a gradient, the font list) is open.
/// </summary>
internal sealed class ModEditorScreen : IModEditorContext, IModEditorActions, IContentEditor
{
  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly Func<OverlayFonts> _fonts;
  private readonly ModFailureGuard _guard;
  private readonly ModPreview _preview;
  private readonly UndoStack<string> _history = new(100);
  private readonly Dictionary<string, int> _tabs = new();
  private readonly PopoverStack _popovers = new();
  private readonly Event _event = new();
  private readonly EditorGlide _space = new();
  private EditorFramePanel _frame;
  private EditorBarPanel _bar;
  private PropertyButtonViewModel _growButton;
  private GrowPopoverPanel _grow;
  private ModPickerContext _pickers;
  private Rect _column;
  private IModColumnPanel _content;
  private ModOverlayElement _element;
  private ElementClass _type;
  private Action<bool> _closed;
  private string _opening;
  private string _working;
  private OverlayGrowth _growth;
  private bool _rebuild;
  private object _drag;

  /// <param name="report">Logs a mod's failure once per kind; see <see cref="ModElements.Failed"/>.</param>
  public ModEditorScreen(
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

  public bool IsOpen => _frame != null;

  public void Raise() => _frame?.Root.transform.SetAsLastSibling();

  /// <summary>The page the mod built last, when it built one; for checks.</summary>
  internal ModEditorPage Page => _content as ModEditorPage;

  /// <summary>The screen the mod wrote with ElementUI, when it wrote one; for checks.</summary>
  internal ModViewPanel View => _content as ModViewPanel;

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
    _frame = new EditorFramePanel(
      "Mod element editor",
      _parent,
      _font,
      _reduceMotion,
      L(TypeName + " 편집", "Edit " + TypeName),
      () => Close(false, true),
      () => Close(true, true),
      noticesInDock: false
    );
    _guard.Screen = _frame;
    _pickers = new ModPickerContext(_fonts, _reduceMotion());
    // Element's pickers for a screen the mod writes with ElementUI.
    EditorParts.Host = new ModEditorParts(_pickers);
    UiFactory ui = _frame.Ui;
    BuildBar();
    _column = ColumnArea(_frame);
    _space.Snap();
    _preview.BuildHolder(ui, _frame, PreviewArea(_column.x), _fonts?.Invoke());
    _grow = new GrowPopoverPanel(_frame, PickGrowth) { Anchor = _bar.RectOf(_growButton) };
    _popovers.Clear();
    _popovers.Add(_grow);
    _preview.BuildGrowMark(ui, _frame.Frame);
    if (!_preview.Create(_type, _element.Settings.Mod, _working))
      return;
    if (!BuildView())
      BuildPage();
    FitPreview();
  }

  private void BuildBar()
  {
    _growButton = new PropertyButtonViewModel(
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      value: true,
      icon: UiIconKind.GrowFrom,
      under: UiPalette.Bar
    );
    _bar = new EditorBarPanel(_frame, "Element bar", _growButton);
  }

  /// <summary>
  /// The property column, where the text editor has its own: as wide as its
  /// keypad, at the right end of the header, from under the header to the bottom.
  /// </summary>
  private static Rect ColumnArea(EditorFramePanel screen)
  {
    float top = EditorFramePanel.TopBar + InspectorPanel.Gap;
    float left = screen.ContentLeft + screen.ContentWidth - ModEditorColumn.Width;
    return new Rect(left, top, ModEditorColumn.Width, screen.Height - top - BottomGap);
  }

  private const float BottomGap = 16;

  // The room left of the column (or a tool reaching further left), under the bar, where the preview centers.
  private Rect PreviewArea(float right)
  {
    float top = EditorFramePanel.TopBar + 8 + EditorBarPanel.Height + 24;
    float left = EditorFramePanel.Margin;
    return new Rect(left, top, Mathf.Max(80, right - 24 - left), _frame.Height - top - EditorFramePanel.Margin);
  }

  // How wide words are in the column's rows.
  private float MeasureRowText(string text)
  {
    TMPro.TextMeshProUGUI measure = _frame.Measure;
    float size = measure.fontSize;
    measure.fontSize = 13;
    float width = Mathf.Ceil(measure.GetPreferredValues(text ?? "").x);
    measure.fontSize = size;
    return width;
  }

  /// <summary>The screen the mod wrote with ElementUI; false when it builds an <see cref="EditorPage"/> instead.</summary>
  private bool BuildView()
  {
    View view = null;
    ViewModel model = null;
    if (!_guard.Run("laying out its editing screen", () => view = _preview.Instance.RunEditor(Korean, out model)))
      return true;
    if (view == null)
      return false;
    _pickers.Edits = ViewEdited;
    _guard.Run(
      "laying out its editing screen",
      () =>
        _content = new ModViewPanel(
          _frame.Frame,
          _column,
          view,
          model,
          _frame.NewBackend,
          _pickers,
          MeasureRowText,
          ViewEdited,
          dragging => _drag = dragging ? new object() : null
        )
    );
    _grow.Root.SetAsLastSibling();
    return true;
  }

  // The mod's parts, laid out fresh; again after undo so they show the restored settings.
  private void BuildPage()
  {
    _content?.Dispose();
    _content = null;
    _pickers.Clear();
    ModEditorPage page = new(
      _frame.Frame,
      new Vector2(_frame.Width, _frame.Height),
      _preview.Area,
      _column,
      this,
      this,
      MeasureRowText
    );
    _content = page;
    if (_guard.Run("laying out its editing screen", () => _preview.Instance.RunEdit(page)))
      _guard.Run("laying out its editing screen", page.Mount);
    _grow.Root.SetAsLastSibling();
  }

  // ---- What the page reads ----

  ElementUI.IBackend IModEditorContext.NewBackend() => _frame.NewBackend();

  ModPickerContext IModEditorContext.Pickers => _pickers;

  int IModEditorContext.TabOf(string key) => _tabs.TryGetValue(key, out int index) ? index : 0;

  // ---- What the page asks for (and Change, below) ----

  void IModEditorActions.SetTab(string key, int index) => _tabs[key] = index;

  private bool Typing => _content != null && _content.Typing;

  /// <summary>
  /// One edit of a page's part: what it changes is one undo step (a part's
  /// edits within a second merge), and the preview shows it right away. A
  /// button may change several settings, so the parts are made again, from the
  /// next frame, to show them.
  /// </summary>
  public void Change(object key, Action apply, bool rebuild)
  {
    if (_preview.Instance == null)
      return;
    string before = _working;
    if (!_guard.Run("changing a setting", apply))
      return;
    // Typing in a field is one step for as long as it stays focused.
    if (Record(before, key, Typing) && rebuild)
      _rebuild = true;
  }

  // An edit on a screen the mod wrote: a drag, or typing in one field, is one step.
  private void ViewEdited()
  {
    if (_preview.Instance == null)
      return;
    // Typing in a field of the rows or of the open tool (a HEX, the font search) is one step.
    object key =
      _drag ?? ModKeyboard.FocusedField(_content?.Root) ?? ModKeyboard.FocusedField(_content?.Column?.ToolRoot);
    Record(_working, key, key != null);
  }

  /// <summary>Saves what the edit changed as one undo step, and shows it; false when nothing changed.</summary>
  private bool Record(string before, object key, bool held)
  {
    string after = Save();
    bool merge = _history.Continues(key, Time.unscaledTime, held);
    bool changed = after != before;
    if (changed)
    {
      _history.Record(before, merge);
      _history.DropRedo();
      _working = after;
    }
    _guard.Run("showing a setting", _preview.Instance.RunSettingsChanged);
    FitPreview();
    return changed;
  }

  private string Save()
  {
    string data = _working;
    _guard.Run("saving its settings", () => data = _preview.Instance.RunSave());
    return data ?? string.Empty;
  }

  // One of the content's popovers is open at a time: a click elsewhere closes it first.
  private void ClosePopovers()
  {
    _popovers.CloseAll();
    _content?.CloseTopPopover();
  }

  public void Tick()
  {
    if (_frame == null)
      return;
    if (_frame.ViewChanged)
    {
      Build();
      if (_frame == null)
        return;
    }
    // Who had the keyboard as the frame began: Esc that ends recording a shortcut doesn't also finish.
    bool typing = Typing;
    _frame.Tick();
    ApplyPending();
    _pickers.Tick();
    if (_preview.Instance != null)
    {
      _preview.Tick(_frame.Scale);
      FitPreview();
    }
    // Typing goes to the field; Esc there only ends the typing.
    if (typing || Typing)
      return;
    if (Input.GetMouseButtonDown(0))
      _popovers.PressOutside(Input.mousePosition);
    HandleKeys();
  }

  /// <summary>What changed since the last frame shows: the parts made again after a button, or laid out again.</summary>
  private void ApplyPending()
  {
    if (_rebuild)
    {
      _rebuild = false;
      BuildPage();
    }
    _content?.Tick();
  }

  // The preview, and the bar above it with Grow from.
  private void FitPreview()
  {
    // A tool wider than the column (a gradient, the font list) moves the preview aside while it is open.
    float right = _space.Toward(_content?.Column != null ? _content.Column.Left : _column.x, _reduceMotion());
    Rect area = PreviewArea(right);
    _preview.Area = area;
    if (!_preview.Fit(_frame.Scale, out Vector2 box))
      return;
    _growButton.Set(GrowPickerViewModel.ShortName(_growth), false, _grow.Visible);
    float barWidth = _bar.LayOut();
    _bar.Place(Mathf.Max(area.x, area.center.x - barWidth * .5f), EditorFramePanel.TopBar + 8);
    bool grow = _grow.Visible;
    _preview.PlaceGrowMark(box, grow, grow ? GrowPointNow() : default);
  }

  private void ToggleGrow()
  {
    if (_grow.Visible)
    {
      _grow.Hide();
      return;
    }
    ClosePopovers();
    _grow.Show(_frame.FrameRect(_bar.RectOf(_growButton)));
    _grow.Refresh(_growth, GrowPointNow());
    _grow.Root.SetAsLastSibling();
  }

  private void PickGrowth(OverlayGrowth growth)
  {
    _growth = growth;
    _grow.Refresh(_growth, GrowPointNow());
  }

  private Anchor9 GrowPointNow() =>
    OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  private bool AnyOpen => _popovers.AnyOpen || (_content != null && _content.HasPopover);

  /// <summary>
  /// Esc closes what is open; with nothing open it finishes like Done. Only
  /// Cancel leaves without saving.
  /// </summary>
  public void Escape()
  {
    if (_popovers.AnyOpen)
      _popovers.CloseAll();
    else if (_content == null || !_content.CloseTopPopover())
      Close(true, true);
  }

  private void HandleKeys()
  {
    bool mac = EditorKeys.Mac;
    while (_frame != null && EditorKeys.NextKeyDown(_event))
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
      bool finishing = !AnyOpen;
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
    _pickers.CancelRecording();
    _guard.Run(
      "loading its settings",
      () =>
      {
        _preview.Instance.RunLoad(data);
        _preview.Instance.RunSettingsChanged();
      }
    );
    FitPreview();
    // A screen the mod wrote reads the settings again; a page is made again.
    if (_content is ModViewPanel view)
      view.Restored();
    else
      BuildPage();
  }

  /// <summary>Done saves the settings and where the element grows from; Cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_frame == null)
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
    InputGate.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  private void TearDown()
  {
    _content?.Dispose();
    _content = null;
    _preview.TearDown();
    _popovers.Clear();
    _grow = null;
    _pickers?.Clear();
    _pickers = null;
    EditorParts.Host = null;
    _rebuild = false;
    _drag = null;
    _frame?.Dispose();
    _frame = null;
    _guard.Screen = null;
  }

  // Torn down while open (the mod or Element turned off), the edit is kept.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }
}
