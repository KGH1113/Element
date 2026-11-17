using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementKit;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// The parts a mod builds its element's editing screen from, as ElementUI
/// views in Element's look. Parts stand one under another in the editor's
/// property column (<see cref="ModEditorColumn"/>), drawn as its rows, unless
/// the mod puts them somewhere with <see cref="EditorControl.At"/>; cards and
/// tab pages group them under a heading. Every change goes through the
/// editor, which records undo and shows it on the preview.
/// </summary>
/// <remarks>
/// The mod makes its parts first (<see cref="OverlayElement.OnEdit"/>), then
/// the page mounts them (<see cref="Mount"/>): the column's in it, the placed
/// ones over the screen.
/// </remarks>
internal sealed class ModEditorPage : EditorPage, IModColumnPanel
{
  private readonly IModEditorActions _editor;
  private readonly IModEditorContext _context;
  private readonly RectTransform _root;
  private readonly RectTransform _parking;
  private readonly ScreenPanel _placedPanel;
  private readonly ModEditorColumn _column;
  private readonly Func<string, float> _measure;
  private readonly ModCommandsViewModel _commands = new();
  private readonly List<ModPart> _items = new();
  private readonly List<ModPart> _placed = new();
  private readonly Stack<ModGroup> _groups = new();
  private readonly ModInputParts _inputParts;
  private readonly ModChoiceParts _choiceParts;
  private readonly ModPickerParts _pickerParts;
  private int _count;
  private bool _mounted;
  private bool _remount;
  private bool _disposing;

  /// <param name="frame">The editor's controls, in frame units.</param>
  /// <param name="size">The screen in frame units.</param>
  /// <param name="preview">Where the preview sits.</param>
  /// <param name="column">Where the property column stands.</param>
  /// <param name="measure">How wide words are at the rows' size.</param>
  public ModEditorPage(
    RectTransform frame,
    Vector2 size,
    Rect preview,
    Rect column,
    IModEditorActions editor,
    IModEditorContext context,
    Func<string, float> measure
  )
  {
    Size = size;
    PreviewArea = preview;
    _editor = editor;
    _context = context;
    _measure = measure;
    _root = NewRect("Mod editor page", frame, size);
    // The objects mods asked for wait here between mounts.
    _parking = NewRect("Part objects", _root, Vector2.zero);
    _parking.gameObject.SetActive(false);
    // Clicks on the page's empty space reach the dim behind.
    _placedPanel = new ScreenPanel("Mod editor placed parts", _root, Color.clear, keyboard: false);
    _column = new ModEditorColumn(frame, column, context.NewBackend, null, null);
    Pickers.Column = _column;
    _inputParts = new ModInputParts(this);
    _choiceParts = new ModChoiceParts(this);
    _pickerParts = new ModPickerParts(this);
  }

  /// <summary>Where the page's parts send a change.</summary>
  internal IModEditorActions Editor => _editor;

  internal ModCommandsViewModel Commands => _commands;

  internal ModPickerContext Pickers => _context.Pickers;

  public override RectTransform Root => _root;

  public override Vector2 Size { get; }

  public override Rect PreviewArea { get; }

  /// <summary>The property column the parts stand in.</summary>
  public ModEditorColumn Column => _column;

  public override bool Korean => UiLocale.Korean;

  /// <summary>The parts the mod placed itself.</summary>
  public IReadOnlyList<ModPart> Placed => _placed;

  /// <summary>The parts in the column, in order, cards and tab pages holding theirs.</summary>
  public IReadOnlyList<ModPart> Items => _items;

  /// <summary>Whether the keyboard belongs to the page: typing in a field, or recording a shortcut.</summary>
  public bool Typing => Pickers.Recording || _column.Typing || ModKeyboard.FocusedField(_root) != null;

  public bool HasPopover =>
    _column.HasPopover
    || (_placedPanel.Host != null && _placedPanel.Host.Root != null && _placedPanel.Host.Root.HasOverlay);

  public bool CloseTopPopover() =>
    (_placedPanel.Host != null && _placedPanel.Host.HandleEscape()) || _column.CloseTopPopover();

  // ---- Structure ----

  public override EditorControl Section(string title) => Add(Part(() => UI.Section(title ?? "")));

  public override EditorControl Divider() => Add(Part(() => HairLine.Across().Padding(4, 0)));

  public override void Gap(float height = 16)
  {
    float room = Mathf.Max(0, Mathf.Max(0, height) - InspectorRowStyle.Gap);
    Add(Part(() => UI.Spacer().Frame(height: room)));
  }

  public override IDisposable Card(string title = null)
  {
    ModGroup card = new(this, ++_count, string.IsNullOrEmpty(title) ? null : title);
    card.Control = () => ModEditorPageView.Card(card);
    Add(card);
    return Into(card);
  }

  public override EditorTabs Tabs(IReadOnlyList<string> names)
  {
    List<string> labels = new();
    if (names != null)
      foreach (string name in names)
        labels.Add(name ?? "");
    if (labels.Count == 0)
      labels.Add("");
    string key = string.Join("\u001f", labels);
    ModEditorTabs tabs = new(this, key, Mathf.Clamp(_context.TabOf(key), 0, labels.Count - 1));
    // Tabs are side by side across the column, whatever their names.
    tabs.BarPart = Add(
      Part(() =>
        UI.Picker(null, labels, UI.Bind(() => tabs.Pick.Value)).ControlStyle(ControlStyle.Standard).Named("Tabs")
      )
    );
    for (int i = 0; i < labels.Count; i++)
    {
      ModGroup page = new(this, ++_count, tabs, i);
      page.Control = () => ModEditorPageView.TabPage(page);
      tabs.Pages.Add(page);
      Add(page);
    }
    return tabs;
  }

  /// <summary>Parts made until the scope ends go into <paramref name="group"/>.</summary>
  internal IDisposable Into(ModGroup group)
  {
    if (group == null)
      return new Scope(null);
    _groups.Push(group);
    return new Scope(() =>
    {
      if (_groups.Count > 0 && _groups.Peek() == group)
        _groups.Pop();
    });
  }

  internal void TabPicked(string key, int index) => _editor.SetTab(key, index);

  // ---- Parts ----

  public override EditorControl Label(string text) => _inputParts.Label(text);

  public override EditorControl Button(string text, Action clicked) => _inputParts.Button(text, clicked);

  public override EditorControl IconButton(EditorIcon icon, string hint, Action clicked) =>
    _inputParts.IconButton(icon, hint, clicked);

  public override EditorControl Switch(string label, bool on, Action<bool> changed) =>
    _inputParts.Switch(label, on, changed);

  public override EditorControl Checkbox(string text, bool on, Action<bool> changed) =>
    _inputParts.Checkbox(text, on, changed);

  public override EditorControl Slider(
    string label,
    float value,
    float min,
    float max,
    Action<float> changed,
    float step = 0
  ) => _inputParts.Slider(label, value, min, max, changed, step);

  public override EditorControl RangeSlider(
    string label,
    float low,
    float high,
    float min,
    float max,
    Action<float, float> changed,
    float step = 0
  ) => _inputParts.RangeSlider(label, low, high, min, max, changed, step);

  public override EditorControl Number(string label, float value, float min, float max, Action<float> changed) =>
    _inputParts.Number(label, value, min, max, changed);

  public override EditorControl Stepper(string label, int value, int min, int max, Action<int> changed, int step = 1) =>
    _inputParts.Stepper(label, value, min, max, changed, step);

  public override EditorControl TextField(string label, string value, Action<string> changed) =>
    _inputParts.TextField(label, value, changed);

  public override EditorControl List(string label, List<string> items, Action changed = null, string newItem = "") =>
    _inputParts.List(label, items, changed, newItem);

  public override EditorControl Choice(
    string label,
    IReadOnlyList<string> options,
    int selected,
    Action<int> changed
  ) => _choiceParts.Choice(label, options, selected, changed);

  public override EditorControl Dropdown(
    string label,
    IReadOnlyList<string> options,
    int selected,
    Action<int> changed
  ) => _choiceParts.Dropdown(label, options, selected, changed);

  public override EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  ) => _choiceParts.MultiChoice(label, options, on, changed);

  public override EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed) =>
    _choiceParts.AnchorPicker(label, value, changed);

  public override EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true) =>
    _pickerParts.ColorPicker(label, value, changed, opacity);

  public override EditorControl ColorPicker(string label, Paint value, Action<Paint> changed) =>
    _pickerParts.ColorPicker(label, value, changed);

  public override EditorControl FontPicker(string label, string font, Action<string> changed) =>
    _pickerParts.FontPicker(label, font, changed);

  public override EditorControl Shortcut(
    string label,
    ElementKit.Shortcut value,
    Action<ElementKit.Shortcut> changed
  ) => _pickerParts.Shortcut(label, value, changed);

  /// <summary>A new part; give it a <see cref="ModPart.Control"/>, then <see cref="Add"/> it.</summary>
  internal ModPart Part(Func<View> control = null) => new(this, ++_count) { Control = control };

  /// <summary>
  /// A value a part shows, starting at <paramref name="value"/>: a change
  /// the player makes is one edit of <paramref name="part"/>, handed to the mod.
  /// </summary>
  internal ModValueViewModel<T> Value<T>(ModPart part, T value, Action<T> changed) =>
    new(value, next => _editor.Change(part, () => changed?.Invoke(next), false));

  /// <summary>Puts <paramref name="part"/> in the card or tab page parts go into now, or the column.</summary>
  internal ModPart Add(ModPart part)
  {
    if (_groups.Count > 0)
    {
      ModGroup group = _groups.Peek();
      part.Group = group;
      group.Children.Add(part);
    }
    else
      _items.Add(part);
    StructureChanged();
    return part;
  }

  /// <summary>Out of the column and any card: <paramref name="part"/> stands where the mod put it.</summary>
  internal void Free(ModPart part)
  {
    if (part.Group != null)
    {
      part.Group.Children.Remove(part);
      part.Group = null;
    }
    else
      _items.Remove(part);
    if (!_placed.Contains(part))
      _placed.Add(part);
    StructureChanged();
  }

  /// <summary>The object a part's <c>Rect</c> gives the mod; it covers the part once the page shows it.</summary>
  internal RectTransform NewPartRect(ModPart part) => NewRect("Part " + part.Id, _parking, Vector2.zero);

  internal void Park(RectTransform rect)
  {
    // While the page goes away, so does what it keeps.
    if (!_disposing && rect != null && _parking != null)
      rect.SetParent(_parking, false);
  }

  /// <summary>After the page is shown, a change to where parts stand shows from the next frame.</summary>
  internal void StructureChanged()
  {
    if (_mounted)
      _remount = true;
  }

  // ---- Showing ----

  /// <summary>Shows the parts the mod made: the column's in the column, the placed ones where the mod put them.</summary>
  public void Mount()
  {
    _column.Mount(new ModEditorPageView(this), new ModColumnStyle(_column, _measure), null);
    _placedPanel.Mount(_context.NewBackend(), ModEditorPageView.Placed(this), null, Size.x);
    _mounted = true;
    _remount = false;
  }

  /// <summary>Once a frame: what changed since the last one is laid out again.</summary>
  public void Tick()
  {
    if (!_mounted)
      return;
    if (_remount)
      Mount();
    _column.Tick();
    _placedPanel.Sync();
  }

  public void Dispose()
  {
    _mounted = false;
    _disposing = true;
    if (Pickers.Column == _column)
      Pickers.Column = null;
    _column.Dispose();
    _placedPanel.Dispose();
    _commands.Dispose();
    RectPeer.DestroyObject(_root.gameObject);
  }

  private static RectTransform NewRect(string name, Transform parent, Vector2 size)
  {
    GameObject holder = new(name, typeof(RectTransform));
    holder.layer = parent.gameObject.layer;
    RectTransform rect = (RectTransform)holder.transform;
    rect.SetParent(parent, false);
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = size;
    return rect;
  }

  private sealed class Scope : IDisposable
  {
    private Action _end;

    public Scope(Action end) => _end = end;

    public void Dispose()
    {
      _end?.Invoke();
      _end = null;
    }
  }
}
