using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Presentation.Settings;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>
/// The parts a mod builds its element's editing screen from, made with the
/// settings UI's own pieces. Parts flow into a column under the preview (a
/// second column when it is full) unless the mod puts them somewhere with
/// <see cref="EditorControl.At"/>. Every change goes through the editor, which
/// records undo and shows it on the preview.
/// </summary>
internal sealed class ModEditorPage : EditorPage
{
  public const float RowHeight = 40,
    RowGap = 10,
    LabelWidth = 170,
    ColumnWidth = 480,
    ColumnGap = 32;

  private readonly SettingsUiFactory _ui;
  private readonly RectTransform _root;
  private readonly Vector2 _size;
  private readonly Rect _preview;
  private readonly Rect _flow;
  private readonly bool _reduceMotion;
  private readonly Action<object, Action, bool> _change;
  private readonly Action<ModColorRequest> _openColor;
  private readonly List<ModEditorControl> _items = new();
  private readonly List<TMP_InputField> _inputs = new();
  private int _count;

  /// <param name="change">
  /// Runs a part's change as one edit: undo, then the mod's callback, then the
  /// preview; true lays the parts out again so they show what it changed.
  /// </param>
  public ModEditorPage(
    SettingsUiFactory ui,
    RectTransform frame,
    Vector2 size,
    Rect preview,
    Rect flow,
    bool reduceMotion,
    Action<object, Action, bool> change,
    Action<ModColorRequest> openColor
  )
  {
    _ui = ui;
    _size = size;
    _preview = preview;
    _flow = flow;
    _reduceMotion = reduceMotion;
    _change = change;
    _openColor = openColor;
    _root = ui.Rect("Mod editor page", frame, 0, 0, size.x, size.y);
    // The page holds parts; clicks on its empty space reach the dim behind.
  }

  public override RectTransform Root => _root;

  public override Vector2 Size => _size;

  public override Rect PreviewArea => _preview;

  public override bool Korean => SettingsLocale.Korean;

  /// <summary>Whether players are typing in one of the page's fields.</summary>
  public bool Typing
  {
    get
    {
      foreach (TMP_InputField input in _inputs)
        if (input != null && input.isFocused)
          return true;
      return false;
    }
  }

  public override EditorControl Label(string text)
  {
    ModEditorControl control = Row("Label", 0);
    TextMeshProUGUI label = _ui.Text(text ?? "", control.Rect, 0, 0, ColumnWidth, 24, 15, SettingsUiFactory.Foreground);
    label.textWrappingMode = TextWrappingModes.Normal;
    control.Layout = width =>
    {
      label.rectTransform.sizeDelta = new Vector2(width, 24);
      float height = Mathf.Max(24, label.GetPreferredValues(label.text, width, 0).y);
      label.rectTransform.sizeDelta = new Vector2(width, height);
      control.Height = height;
    };
    return Add(control);
  }

  public override EditorControl Button(string text, Action clicked)
  {
    ModEditorControl control = Row("Button", 0);
    Button button = null;
    button = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Button " + text,
      text ?? "",
      0,
      2,
      120,
      36,
      14,
      () => _change(button, () => clicked?.Invoke(), true),
      false,
      _reduceMotion
    );
    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
    float preferred = label != null ? label.GetPreferredValues(text ?? "", 1000, 36).x : 0;
    control.Layout = width =>
      SettingsUiFactory.Place(
        (RectTransform)button.transform,
        0,
        2,
        Mathf.Min(width, Mathf.Max(96, preferred + 36)),
        36
      );
    return Add(control);
  }

  public override EditorControl Switch(string label, bool on, Action<bool> changed)
  {
    ModEditorControl control = Row("Switch", 0);
    float x = Caption(control, label);
    bool state = on;
    SettingsCapsuleGraphic track = null;
    SettingsCapsuleGraphic thumb = null;
    SettingsSwitchMotion motion = null;
    void Show(bool immediate)
    {
      track.color = state ? SettingsPalette.AccentSurface : new Color(.3f, .3f, .3f);
      thumb.color = state ? SettingsPalette.SoftAccent : SettingsUiFactory.Foreground;
      motion.Set(state ? 24 : 4, immediate || _reduceMotion);
    }
    Button hit = null;
    hit = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Switch " + label,
      "",
      x,
      2,
      56,
      36,
      14,
      () =>
      {
        state = !state;
        Show(false);
        bool now = state;
        _change(hit, () => changed?.Invoke(now), false);
      },
      true,
      _reduceMotion
    );
    track = _ui.Capsule("Track", hit.transform, 4, 4, 48, 28, SettingsUiFactory.Surface);
    track.raycastTarget = false;
    thumb = _ui.Capsule("Thumb", track.transform, 4, 4, 20, 20, SettingsUiFactory.Foreground);
    thumb.raycastTarget = false;
    motion = thumb.gameObject.AddComponent<SettingsSwitchMotion>();
    Show(true);
    return Add(control);
  }

  public override EditorControl Slider(
    string label,
    float value,
    float min,
    float max,
    Action<float> changed,
    float step = 0
  )
  {
    if (!(max > min))
      max = min + 1;
    ModEditorControl control = Row("Slider", 0);
    float x = Caption(control, label);
    float Snap(float v)
    {
      v = Mathf.Clamp(v, min, max);
      return step > 0 ? Mathf.Clamp(min + Mathf.Round((v - min) / step) * step, min, max) : v;
    }
    float current = Snap(value);
    TextMeshProUGUI shown = _ui.Text(
      Format(current, step),
      control.Rect,
      0,
      0,
      64,
      RowHeight,
      14,
      SettingsUiFactory.Muted
    );
    shown.alignment = TextAlignmentOptions.MidlineRight;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    Slider slider = null;
    slider = _ui.Slider(
      "Slider " + label,
      control.Rect,
      x,
      2,
      200,
      (current - min) / (max - min),
      normalized =>
      {
        float next = Snap(min + normalized * (max - min));
        if (Mathf.Approximately(next, current))
          return;
        current = next;
        shown.text = Format(current, step);
        float now = current;
        _change(slider, () => changed?.Invoke(now), false);
      }
    );
    if (slider is SettingsSlider settings)
      settings.Step = step > 0 ? step / (max - min) : 1f / 255f;
    control.Layout = width =>
    {
      float room = Mathf.Max(80, width - x - 72);
      SettingsUiFactory.Place((RectTransform)slider.transform, x, 2, room, 36);
      SettingsUiFactory.Place(shown.rectTransform, x + room + 8, 0, 64, RowHeight);
    };
    return Add(control);
  }

  public override EditorControl Number(string label, float value, float min, float max, Action<float> changed)
  {
    if (!(max >= min))
      max = min;
    ModEditorControl control = Row("Number", 0);
    float x = Caption(control, label);
    float current = Mathf.Clamp(value, min, max);
    TMP_InputField input = null;
    input = _ui.Input(
      "Number " + label,
      Format(current, 0),
      control.Rect,
      x,
      0,
      160,
      null,
      typed =>
      {
        float next =
          float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
          && !float.IsNaN(parsed)
          && !float.IsInfinity(parsed)
            ? Mathf.Clamp(parsed, min, max)
            : current;
        input.SetTextWithoutNotify(Format(next, 0));
        if (Mathf.Approximately(next, current))
          return;
        current = next;
        float now = current;
        _change(input, () => changed?.Invoke(now), false);
      },
      12
    );
    input.contentType = TMP_InputField.ContentType.DecimalNumber;
    _inputs.Add(input);
    control.Layout = width =>
      SettingsUiFactory.Place(
        (RectTransform)input.transform,
        x,
        0,
        Mathf.Min(160, Mathf.Max(80, width - x)),
        RowHeight
      );
    return Add(control);
  }

  public override EditorControl TextField(string label, string value, Action<string> changed)
  {
    ModEditorControl control = Row("Text", 0);
    float x = Caption(control, label);
    string current = value ?? "";
    TMP_InputField input = null;
    input = _ui.Input(
      "Text " + label,
      current,
      control.Rect,
      x,
      0,
      240,
      null,
      typed =>
      {
        if (typed == current)
          return;
        current = typed;
        string now = current;
        _change(input, () => changed?.Invoke(now), false);
      },
      200
    );
    _inputs.Add(input);
    control.Layout = width =>
      SettingsUiFactory.Place((RectTransform)input.transform, x, 0, Mathf.Max(80, width - x), RowHeight);
    return Add(control);
  }

  public override EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    ModEditorControl control = Row("Choice", 0);
    float x = Caption(control, label);
    List<string> names = new();
    if (options != null)
      foreach (string option in options)
        names.Add(option ?? "");
    if (names.Count == 0)
      names.Add("");
    SettingsSegmentedControl segments = null;
    segments = new SettingsSegmentedControl(
      _ui,
      control.Rect,
      x,
      2,
      240,
      36,
      names,
      index => _change(segments, () => changed?.Invoke(index), false)
    );
    segments.Select(Mathf.Clamp(selected, 0, names.Count - 1), false);
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x);
      SettingsUiFactory.Place(segments.Root, x, 2, room, 36);
      float segment = (room - 6) / names.Count;
      for (int i = 0; i < segments.Buttons.Count; i++)
        SettingsUiFactory.Place((RectTransform)segments.Buttons[i].transform, 3 + segment * i, 3, segment, 30);
    };
    return Add(control);
  }

  public override EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true)
  {
    ModEditorControl control = Row("Color", 0);
    float x = Caption(control, label);
    Color current = value;
    if (!opacity)
      current.a = 1;
    TextMeshProUGUI hex = _ui.Text("", control.Rect, x + 40, 0, 160, RowHeight, 14, SettingsUiFactory.Muted);
    hex.textWrappingMode = TextWrappingModes.NoWrap;
    SettingsCapsuleGraphic swatch = null;
    void Show()
    {
      Color opaque = current;
      opaque.a = 1;
      swatch.color = opaque;
      hex.text =
        "#"
        + ColorUtility.ToHtmlStringRGB(current)
        + (opacity && current.a < .995f ? "  " + Mathf.RoundToInt(current.a * 100) + "%" : "");
    }
    Button button = null;
    swatch = OverlayEditorUi.Swatch(
      _ui,
      control.Rect,
      "Color " + label,
      x,
      4,
      () =>
        _openColor(
          new ModColorRequest(
            string.IsNullOrEmpty(label) ? SettingsLocale.L("색", "Color") : label,
            current,
            opacity,
            (RectTransform)button.transform,
            picked =>
            {
              current = picked;
              Show();
              Color now = current;
              _change(button, () => changed?.Invoke(now), false);
            }
          )
        ),
      _reduceMotion,
      out button
    );
    Show();
    control.Layout = width =>
      SettingsUiFactory.Place(hex.rectTransform, x + 40, 0, Mathf.Max(60, width - x - 40), RowHeight);
    return Add(control);
  }

  public override void Gap(float height = 16)
  {
    ModEditorControl gap = Row("Gap", Mathf.Max(0, height));
    gap.Height = Mathf.Max(0, Mathf.Max(0, height) - RowGap);
    Add(gap);
  }

  /// <summary>
  /// Puts the flowing parts in columns under the preview, centered, starting a
  /// new column when one is full. Parts placed with At stay where they are.
  /// </summary>
  public void Layout()
  {
    foreach (ModEditorControl item in _items)
      item.Layout?.Invoke(item.WidthOverride ?? ColumnWidth);
    List<(ModEditorControl Item, int Column, float Y)> placed = new();
    int column = 0;
    float y = _flow.y;
    foreach (ModEditorControl item in _items)
    {
      if (!item.Flows)
        continue;
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

  // A row in the column; parts lay themselves out across its width.
  private ModEditorControl Row(string kind, float height)
  {
    RectTransform rect = _ui.Rect(kind + " " + ++_count, _root, 0, 0, ColumnWidth, height > 0 ? height : RowHeight);
    return new ModEditorControl(this, rect, height > 0 ? height : RowHeight);
  }

  // The label left of a control; where the control starts.
  private float Caption(ModEditorControl control, string label)
  {
    if (string.IsNullOrEmpty(label))
      return 0;
    TextMeshProUGUI text = _ui.Text(
      label,
      control.Rect,
      0,
      0,
      LabelWidth - 12,
      RowHeight,
      15,
      SettingsUiFactory.Foreground
    );
    text.alignment = TextAlignmentOptions.MidlineLeft;
    text.textWrappingMode = TextWrappingModes.NoWrap;
    text.overflowMode = TextOverflowModes.Ellipsis;
    return LabelWidth;
  }

  private ModEditorControl Add(ModEditorControl control)
  {
    _items.Add(control);
    control.Layout?.Invoke(control.WidthOverride ?? ColumnWidth);
    Layout();
    return control;
  }

  private static string Format(float value, float step) =>
    step >= 1 || Mathf.Approximately(value, Mathf.Round(value))
      ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
      : value.ToString("0.##", CultureInfo.InvariantCulture);

  /// <summary>Lays the parts out again after one changed its width.</summary>
  internal void Changed() => Layout();
}

/// <summary>A part on a mod's editing screen.</summary>
internal sealed class ModEditorControl : EditorControl
{
  private readonly ModEditorPage _page;

  public ModEditorControl(ModEditorPage page, RectTransform rect, float height)
  {
    _page = page;
    Rect = rect;
    Height = height;
  }

  public override RectTransform Rect { get; }

  /// <summary>Lays the part's pieces across a row this wide.</summary>
  public Action<float> Layout { get; set; }

  public float Height { get; set; }

  public float? WidthOverride { get; private set; }

  public bool Flows { get; private set; } = true;

  public override EditorControl At(float x, float y)
  {
    Flows = false;
    SettingsUiFactory.Place(Rect, x, y, WidthOverride ?? ModEditorPage.ColumnWidth, Height);
    _page.Changed();
    return this;
  }

  public override EditorControl Width(float width)
  {
    WidthOverride = Mathf.Max(40, width);
    Layout?.Invoke(WidthOverride.Value);
    if (!Flows)
      Rect.sizeDelta = new Vector2(WidthOverride.Value, Height);
    _page.Changed();
    return this;
  }

  public override bool Visible
  {
    get => Rect.gameObject.activeSelf;
    set
    {
      if (Rect.gameObject.activeSelf != value)
        Rect.gameObject.SetActive(value);
    }
  }
}

/// <summary>A color part asking the editor to open the picker beside it.</summary>
internal sealed class ModColorRequest
{
  public ModColorRequest(string title, Color color, bool opacity, RectTransform anchor, Action<Color> changed)
  {
    Title = title;
    Color = color;
    Opacity = opacity;
    Anchor = anchor;
    Changed = changed;
  }

  public string Title { get; }
  public Color Color { get; }
  public bool Opacity { get; }
  public RectTransform Anchor { get; }
  public Action<Color> Changed { get; }
}
