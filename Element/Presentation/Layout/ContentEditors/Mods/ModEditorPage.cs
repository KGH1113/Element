using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Theme;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// The parts a mod builds its element's editing screen from, made with the
/// settings UI's own pieces. Parts flow into a column under the preview (a
/// second column when it is full) unless the mod puts them somewhere with
/// <see cref="EditorControl.At"/>; cards and tab pages group them. Every
/// change goes through the editor, which records undo and shows it on the preview.
/// </summary>
internal sealed class ModEditorPage : EditorPage
{
  public const float RowHeight = 40,
    RowGap = 10,
    LabelWidth = 170,
    ColumnWidth = 480,
    ColumnGap = 32,
    CardPadding = 14;

  private readonly SettingsUiFactory _ui;
  private readonly RectTransform _root;
  private readonly Vector2 _size;
  private readonly Rect _preview;
  private readonly Rect _flow;
  private readonly bool _reduceMotion;
  private readonly IModEditorHost _host;
  private readonly List<ModEditorControl> _items = new();
  private readonly List<TMP_InputField> _inputs = new();
  private readonly Stack<ModEditorGroup> _groups = new();
  private readonly ModInputParts _inputParts;
  private readonly ModChoiceParts _choiceParts;
  private readonly ModPickerParts _pickerParts;
  private int _count;

  public ModEditorPage(
    SettingsUiFactory ui,
    RectTransform frame,
    Vector2 size,
    Rect preview,
    Rect flow,
    bool reduceMotion,
    IModEditorHost host
  )
  {
    _ui = ui;
    _size = size;
    _preview = preview;
    _flow = flow;
    _reduceMotion = reduceMotion;
    _host = host;
    // Clicks on the page's empty space reach the dim behind.
    _root = ui.Rect("Mod editor page", frame, 0, 0, size.x, size.y);
    _inputParts = new ModInputParts(this);
    _choiceParts = new ModChoiceParts(this);
    _pickerParts = new ModPickerParts(this);
  }

  internal SettingsUiFactory Ui => _ui;

  internal IModEditorHost Host => _host;

  internal bool ReduceMotion => _reduceMotion;

  public override RectTransform Root => _root;

  public override Vector2 Size => _size;

  public override Rect PreviewArea => _preview;

  public override bool Korean => UiLocale.Korean;

  /// <summary>Whether the keyboard belongs to the page: typing in a field, or recording a shortcut.</summary>
  public bool Typing
  {
    get
    {
      if (_pickerParts.Capturing)
        return true;
      foreach (TMP_InputField input in _inputs)
        if (input != null && input.isFocused)
          return true;
      return false;
    }
  }

  /// <summary>Once a frame: records a shortcut while one is being set.</summary>
  public void Tick() => _pickerParts.TickShortcut();

  // ---- Structure ----

  public override EditorControl Section(string title)
  {
    ModEditorControl control = Row("Section", 30);
    TextMeshProUGUI text = _ui.Text(title ?? "", control.Rect, 0, 6, ColumnWidth, 24, 13, UiPalette.Muted);
    text.fontStyle = FontStyles.Bold;
    text.textWrappingMode = TextWrappingModes.NoWrap;
    text.overflowMode = TextOverflowModes.Ellipsis;
    control.Layout = width => text.rectTransform.sizeDelta = new Vector2(width, 24);
    return Add(control);
  }

  public override EditorControl Divider()
  {
    ModEditorControl control = Row("Divider", 9);
    Image line = _ui.Panel("Line", control.Rect, 0, 4, ColumnWidth, 1, new Color(1, 1, 1, .12f), false);
    control.Layout = width => SettingsUiFactory.Place(line.rectTransform, 0, 4, width, 1);
    return Add(control);
  }

  public override void Gap(float height = 16)
  {
    ModEditorControl gap = Row("Gap", Mathf.Max(1, height));
    gap.Height = Mathf.Max(0, Mathf.Max(0, height) - RowGap);
    Add(gap);
  }

  public override IDisposable Card(string title = null)
  {
    bool titled = !string.IsNullOrEmpty(title);
    RectTransform rect = _ui.Rect("Card " + ++_count, CurrentParent, 0, 0, ColumnWidth, 10);
    Image surface = _ui.Panel("Card surface", rect, 0, 0, ColumnWidth, 10, new Color(1, 1, 1, .045f));
    SettingsUiFactory.Stretch(surface.rectTransform, 0, 0, 0, 0);
    if (titled)
    {
      TextMeshProUGUI heading = _ui.Text(
        title,
        rect,
        CardPadding,
        CardPadding - 4,
        ColumnWidth - CardPadding * 2,
        24,
        14
      );
      heading.fontStyle = FontStyles.Bold;
      heading.textWrappingMode = TextWrappingModes.NoWrap;
      heading.overflowMode = TextOverflowModes.Ellipsis;
    }
    ModEditorGroup card = new(this, rect, CardPadding, titled ? 30 : 0, null);
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
    ModEditorTabs tabs = new(this, key, Mathf.Clamp(_host.TabOf(key), 0, labels.Count - 1));
    ModEditorControl bar = Row("Tabs", RowHeight);
    SettingsSegmentedControl segments = new(
      _ui,
      bar.Rect,
      0,
      2,
      ColumnWidth,
      36,
      labels,
      index => tabs.Selected = index
    );
    segments.Select(tabs.Selected, false);
    bar.Layout = width =>
    {
      SettingsUiFactory.Place(segments.Root, 0, 2, width, 36);
      float segment = (width - 6) / labels.Count;
      for (int i = 0; i < segments.Buttons.Count; i++)
        SettingsUiFactory.Place((RectTransform)segments.Buttons[i].transform, 3 + segment * i, 3, segment, 30);
    };
    tabs.BarControl = Add(bar);
    tabs.Segments = segments;
    for (int i = 0; i < labels.Count; i++)
    {
      int index = i;
      RectTransform rect = _ui.Rect("Tab page " + ++_count, CurrentParent, 0, 0, ColumnWidth, 10);
      // At the top of the column a tab's parts flow one by one; inside a card it is one block.
      ModEditorGroup page = new(this, rect, 0, 0, () => tabs.Selected == index, _groups.Count == 0);
      tabs.Pages.Add(page);
      Add(page);
    }
    return tabs;
  }

  /// <summary>Parts made until the scope ends go into <paramref name="group"/>.</summary>
  internal IDisposable Into(ModEditorGroup group)
  {
    if (group == null)
      return new Scope(null);
    _groups.Push(group);
    return new Scope(() =>
    {
      if (_groups.Count > 0 && _groups.Peek() == group)
        _groups.Pop();
      Layout();
    });
  }

  internal void TabPicked(string key, int index)
  {
    _host.SetTab(key, index);
    HideTip();
    Layout();
  }

  internal void ShowTip(string text, RectTransform anchor) => _host.ShowTip(text, anchor);

  internal void HideTip() => _host.HideTip();

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

  /// <summary>Records <paramref name="input"/>: the page has the keyboard while it is focused.</summary>
  internal void TrackInput(TMP_InputField input) => _inputs.Add(input);

  internal LayoutActionIcon Icon(Transform parent, LayoutAction kind, float x, float y, float size, Color color)
  {
    LayoutActionIcon icon = _ui.Rect("Icon", parent, x, y, size, size).gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.color = color;
    icon.raycastTarget = false;
    return icon;
  }

  // A label's one-line width, kept once it has been measured on screen: text
  // in a tab not picked yet measures short.
  internal static Func<float> Preferred(TextMeshProUGUI label, string text)
  {
    float? measured = null;
    return () =>
    {
      if (measured.HasValue)
        return measured.Value;
      if (label == null)
        return 0;
      float width = label.GetPreferredValues(text ?? "", 1000, 36).x;
      if (label.gameObject.activeInHierarchy)
        measured = width;
      return width;
    };
  }

  // ---- Layout ----

  /// <summary>
  /// Puts the flowing parts in columns under the preview, centered, starting a
  /// new column when one is full. Cards and tab pages lay out their own parts
  /// and take one place; parts placed with At stay where they are.
  /// </summary>
  public void Layout()
  {
    List<(ModEditorControl Item, int Column, float Y)> placed = new();
    int column = 0;
    float y = _flow.y;
    foreach (ModEditorControl item in Flowing(_items))
    {
      if (!item.Flows)
      {
        item.Layout?.Invoke(item.WidthOverride ?? ColumnWidth);
        continue;
      }
      bool shown = item.Shown;
      if (item is ModEditorGroup && item.Rect.gameObject.activeSelf != shown)
        item.Rect.gameObject.SetActive(shown);
      if (!shown)
        continue;
      item.Layout?.Invoke(item.WidthOverride ?? ColumnWidth);
      float height = item.Height;
      if (y > _flow.y && y + height > _flow.yMax)
      {
        column++;
        y = _flow.y;
      }
      placed.Add((item, column, y));
      y += height + RowGap;
    }
    int columns = column + 1;
    float total = columns * ColumnWidth + (columns - 1) * ColumnGap;
    float left = Mathf.Max(OverlayEditorFrame.Margin, _flow.center.x - total * .5f);
    foreach ((ModEditorControl item, int at, float top) in placed)
      SettingsUiFactory.Place(
        item.Rect,
        left + at * (ColumnWidth + ColumnGap),
        top,
        item.WidthOverride ?? ColumnWidth,
        item.Height
      );
  }

  /// <summary>Lays the parts out again after one changed its width or place.</summary>
  internal void Changed() => Layout();

  // The column's parts in order, a picked tab's parts in its place; the
  // other tabs' pages are hidden.
  private IEnumerable<ModEditorControl> Flowing(List<ModEditorControl> items)
  {
    foreach (ModEditorControl item in items)
    {
      if (item is not ModEditorGroup { Transparent: true } tab)
      {
        yield return item;
        continue;
      }
      bool shown = tab.Shown;
      if (tab.Rect.gameObject.activeSelf != shown)
        tab.Rect.gameObject.SetActive(shown);
      if (!shown)
        continue;
      // Spans the screen so its parts are placed in the screen's own units.
      SettingsUiFactory.Place(tab.Rect, 0, 0, _size.x, _size.y);
      foreach (ModEditorControl child in Flowing(tab.Children))
        yield return child;
    }
  }

  // The card or tab page parts go into now, or the screen.
  private Transform CurrentParent => _groups.Count > 0 ? _groups.Peek().Rect : _root;

  // A row in the column; parts lay themselves out across its width.
  internal ModEditorControl Row(string kind, float height)
  {
    float rowHeight = height > 0 ? height : RowHeight;
    RectTransform rect = _ui.Rect(kind + " " + ++_count, CurrentParent, 0, 0, ColumnWidth, rowHeight);
    return new ModEditorControl(this, rect, rowHeight);
  }

  // The label left of a control; where the control starts.
  internal float Caption(ModEditorControl control, string label, float height = RowHeight)
  {
    if (string.IsNullOrEmpty(label))
      return 0;
    TextMeshProUGUI text = _ui.Text(label, control.Rect, 0, 0, LabelWidth - 12, height, 15, UiPalette.Foreground);
    text.alignment = TextAlignmentOptions.MidlineLeft;
    text.textWrappingMode = TextWrappingModes.NoWrap;
    text.overflowMode = TextOverflowModes.Ellipsis;
    return LabelWidth;
  }

  internal ModEditorControl Add(ModEditorControl control)
  {
    if (_groups.Count > 0)
    {
      ModEditorGroup group = _groups.Peek();
      control.Group = group;
      group.Children.Add(control);
    }
    else
      _items.Add(control);
    control.Layout?.Invoke(control.WidthOverride ?? ColumnWidth);
    Layout();
    return control;
  }

  internal static bool TryNumber(string typed, out float number) =>
    float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
    && !float.IsNaN(number)
    && !float.IsInfinity(number);

  internal static string Format(float value, float step) =>
    step >= 1 || Mathf.Approximately(value, Mathf.Round(value))
      ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
      : value.ToString("0.##", CultureInfo.InvariantCulture);

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
