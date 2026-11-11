using System;
using System.Collections.Generic;
using Element.Presentation.Settings;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Words, buttons, on/off, and numbers and words typed or dragged.
internal sealed partial class ModEditorPage
{
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
      () => _host.Change(button, () => clicked?.Invoke(), true),
      false,
      _reduceMotion
    );
    Func<float> preferred = Preferred(button.GetComponentInChildren<TextMeshProUGUI>(), text);
    control.Layout = width =>
      SettingsUiFactory.Place(
        (RectTransform)button.transform,
        0,
        2,
        Mathf.Min(width, Mathf.Max(96, preferred() + 36)),
        36
      );
    return Add(control);
  }

  public override EditorControl IconButton(EditorIcon icon, string hint, Action clicked)
  {
    ModEditorControl control = Row("Icon button", 0);
    Button button = null;
    button = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Icon " + icon,
      "",
      0,
      2,
      36,
      36,
      14,
      () => _host.Change(button, () => clicked?.Invoke(), true),
      false,
      _reduceMotion
    );
    Icon(button.transform, LayoutIcon(icon), 6, 6, 24, SettingsUiFactory.Foreground);
    control.Layout = _ => SettingsUiFactory.Place((RectTransform)button.transform, 0, 2, 36, 36);
    Add(control);
    control.Hint(hint);
    return control;
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
        _host.Change(hit, () => changed?.Invoke(now), false);
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

  public override EditorControl Checkbox(string text, bool on, Action<bool> changed)
  {
    ModEditorControl control = Row("Checkbox", 0);
    bool state = on;
    Image box = null;
    LayoutActionIcon check = null;
    void Show()
    {
      box.color = state ? SettingsPalette.SoftAccent : new Color(.3f, .3f, .3f);
      check.gameObject.SetActive(state);
    }
    Button hit = null;
    hit = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Checkbox " + text,
      "",
      0,
      2,
      ColumnWidth,
      36,
      14,
      () =>
      {
        state = !state;
        Show();
        bool now = state;
        _host.Change(hit, () => changed?.Invoke(now), false);
      },
      true,
      _reduceMotion
    );
    box = _ui.Panel("Box", hit.transform, 8, 7, 22, 22, Color.white);
    check = Icon(box.transform, LayoutAction.Check, 1, 1, 20, SettingsUiFactory.Background);
    TextMeshProUGUI label = _ui.Text(
      text ?? "",
      hit.transform,
      42,
      0,
      ColumnWidth - 50,
      36,
      15,
      SettingsUiFactory.Foreground
    );
    label.alignment = TextAlignmentOptions.MidlineLeft;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.overflowMode = TextOverflowModes.Ellipsis;
    label.raycastTarget = false;
    Func<float> preferred = Preferred(label, text);
    control.Layout = width =>
    {
      float room = Mathf.Min(width, preferred() + 58);
      SettingsUiFactory.Place((RectTransform)hit.transform, 0, 2, room, 36);
      label.rectTransform.sizeDelta = new Vector2(room - 50, 36);
    };
    Show();
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
    float current = Snap(value, min, max, step);
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
        float next = Snap(min + normalized * (max - min), min, max, step);
        if (Mathf.Approximately(next, current))
          return;
        current = next;
        shown.text = Format(current, step);
        float now = current;
        _host.Change(slider, () => changed?.Invoke(now), false);
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

  public override EditorControl RangeSlider(
    string label,
    float low,
    float high,
    float min,
    float max,
    Action<float, float> changed,
    float step = 0
  )
  {
    if (!(max > min))
      max = min + 1;
    ModEditorControl control = Row("Range", 0);
    float x = Caption(control, label);
    float lower = Snap(Mathf.Min(low, high), min, max, step),
      upper = Snap(Mathf.Max(low, high), min, max, step);
    // The track with its fill and two handles; a clear area takes the pointer.
    RectTransform area = _ui.Rect("Range " + label, control.Rect, x, 2, 200, 36);
    Image hit = area.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    RectTransform track = _ui.Rect("Track area", area, 10, 0, 180, 36);
    SettingsCapsuleGraphic line = _ui.Capsule("Track", track, 0, 15, 180, 6, new Color(.28f, .28f, .28f));
    line.raycastTarget = false;
    SettingsCapsuleGraphic fill = _ui.Capsule("Fill", track, 0, 15, 10, 6, SettingsPalette.SoftAccent);
    fill.raycastTarget = false;
    SettingsCapsuleGraphic lowHandle = _ui.Capsule("Low handle", track, 0, 8, 20, 20, SettingsUiFactory.Foreground);
    SettingsCapsuleGraphic highHandle = _ui.Capsule("High handle", track, 0, 8, 20, 20, SettingsUiFactory.Foreground);
    lowHandle.raycastTarget = highHandle.raycastTarget = false;
    TextMeshProUGUI shown = _ui.Text("", control.Rect, 0, 0, 96, RowHeight, 14, SettingsUiFactory.Muted);
    shown.alignment = TextAlignmentOptions.MidlineRight;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    void Show()
    {
      float width = track.rect.width;
      float a = (lower - min) / (max - min) * width,
        b = (upper - min) / (max - min) * width;
      SettingsUiFactory.Place(fill.rectTransform, a, 15, Mathf.Max(1, b - a), 6);
      SettingsUiFactory.Place(lowHandle.rectTransform, a - 10, 8, 20, 20);
      SettingsUiFactory.Place(highHandle.rectTransform, b - 10, 8, 20, 20);
      shown.text = Format(lower, step) + " – " + Format(upper, step);
    }
    bool movingUpper = false;
    ModRangeDrag drag = area.gameObject.AddComponent<ModRangeDrag>();
    drag.Track = track;
    drag.Moved = (fraction, down) =>
    {
      float value = Snap(min + fraction * (max - min), min, max, step);
      // A press takes the nearer handle; from then the drag moves that one.
      if (down)
        movingUpper = Mathf.Abs(value - upper) < Mathf.Abs(value - lower) || (value > upper && value >= lower);
      float nextLower = lower,
        nextUpper = upper;
      if (movingUpper)
        nextUpper = Mathf.Max(value, lower);
      else
        nextLower = Mathf.Min(value, upper);
      if (Mathf.Approximately(nextLower, lower) && Mathf.Approximately(nextUpper, upper))
        return;
      lower = nextLower;
      upper = nextUpper;
      Show();
      float a = lower,
        b = upper;
      _host.Change(area, () => changed?.Invoke(a, b), false);
    };
    control.Layout = width =>
    {
      float room = Mathf.Max(80, width - x - 104);
      SettingsUiFactory.Place(area, x, 2, room, 36);
      SettingsUiFactory.Place(track, 10, 0, room - 20, 36);
      line.rectTransform.sizeDelta = new Vector2(room - 20, 6);
      SettingsUiFactory.Place(shown.rectTransform, x + room + 8, 0, 96, RowHeight);
      Show();
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
    void Apply(float next)
    {
      if (Mathf.Approximately(next, current))
        return;
      current = next;
      float now = current;
      _host.Change(input, () => changed?.Invoke(now), false);
    }
    input = _ui.Input(
      "Number " + label,
      Format(current, 0),
      control.Rect,
      x,
      0,
      160,
      // As it is typed: a number within the limits shows at once; one outside
      // them waits, so the preview doesn't jump while it is still being typed.
      typed =>
      {
        if (TryNumber(typed, out float parsed) && parsed >= min && parsed <= max)
          Apply(parsed);
      },
      // Typing done: whatever was typed is kept within the limits and shown so.
      typed =>
      {
        if (TryNumber(typed, out float parsed))
          Apply(Mathf.Clamp(parsed, min, max));
        input.SetTextWithoutNotify(Format(current, 0));
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

  public override EditorControl Stepper(string label, int value, int min, int max, Action<int> changed, int step = 1)
  {
    if (max < min)
      max = min;
    step = Mathf.Max(1, step);
    ModEditorControl control = Row("Stepper", 0);
    float x = Caption(control, label);
    int current = Mathf.Clamp(value, min, max);
    Button minus = null,
      plus = null;
    TextMeshProUGUI shown = _ui.Text("", control.Rect, x + 40, 0, 64, RowHeight, 16, SettingsUiFactory.Foreground);
    shown.alignment = TextAlignmentOptions.Midline;
    void Show()
    {
      shown.text = current.ToString(System.Globalization.CultureInfo.InvariantCulture);
      minus.interactable = current > min;
      plus.interactable = current < max;
    }
    void Move(int by, object key)
    {
      int next = Mathf.Clamp(current + by, min, max);
      if (next == current)
        return;
      current = next;
      Show();
      int now = current;
      _host.Change(key, () => changed?.Invoke(now), false);
    }
    minus = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Step down " + label,
      "",
      x,
      2,
      36,
      36,
      14,
      () => Move(-step, minus),
      false,
      _reduceMotion
    );
    Icon(minus.transform, LayoutAction.Minus, 6, 6, 24, SettingsUiFactory.Foreground);
    plus = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Step up " + label,
      "",
      x + 108,
      2,
      36,
      36,
      14,
      () => Move(step, plus),
      false,
      _reduceMotion
    );
    Icon(plus.transform, LayoutAction.Plus, 6, 6, 24, SettingsUiFactory.Foreground);
    Show();
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
      // Each letter shows on the preview as it is typed.
      typed =>
      {
        if (typed == current)
          return;
        current = typed;
        string now = current;
        _host.Change(input, () => changed?.Invoke(now), false);
      },
      null,
      200
    );
    _inputs.Add(input);
    control.Layout = width =>
      SettingsUiFactory.Place((RectTransform)input.transform, x, 0, Mathf.Max(80, width - x), RowHeight);
    return Add(control);
  }

  public override EditorControl List(string label, List<string> items, Action changed = null, string newItem = "")
  {
    items ??= new List<string>();
    ModEditorControl control = Row("List", 0);
    float top = 0;
    TextMeshProUGUI heading = null;
    if (!string.IsNullOrEmpty(label))
    {
      heading = _ui.Text(label, control.Rect, 0, 0, ColumnWidth, 28, 15, SettingsUiFactory.Foreground);
      heading.alignment = TextAlignmentOptions.MidlineLeft;
      heading.textWrappingMode = TextWrappingModes.NoWrap;
      top = 32;
    }
    List<(TMP_InputField Input, Button Up, Button Down, Button Delete)> rows = new();
    for (int i = 0; i < items.Count; i++)
    {
      int index = i;
      TMP_InputField input = null;
      input = _ui.Input(
        "List item " + (i + 1),
        items[i] ?? "",
        control.Rect,
        0,
        0,
        200,
        typed =>
        {
          if (index >= items.Count || items[index] == typed)
            return;
          _host.Change(
            input,
            () =>
            {
              items[index] = typed;
              changed?.Invoke();
            },
            false
          );
        },
        null,
        200
      );
      _inputs.Add(input);
      Button up = null,
        down = null,
        delete = null;
      up = ListButton(control, LayoutAction.Up, "Move up", index > 0, () => Swap(index, index - 1), () => up);
      down = ListButton(
        control,
        LayoutAction.Down,
        "Move down",
        index < items.Count - 1,
        () => Swap(index, index + 1),
        () => down
      );
      delete = ListButton(
        control,
        LayoutAction.Delete,
        "Remove",
        true,
        () =>
        {
          items.RemoveAt(index);
          changed?.Invoke();
        },
        () => delete
      );
      rows.Add((input, up, down, delete));
    }
    Button add = null;
    add = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "List add",
      L("+ 추가", "+ Add"),
      0,
      0,
      96,
      36,
      14,
      () =>
        _host.Change(
          add,
          () =>
          {
            items.Add(newItem ?? "");
            changed?.Invoke();
          },
          true
        ),
      false,
      _reduceMotion
    );
    void Swap(int a, int b)
    {
      (items[a], items[b]) = (items[b], items[a]);
      changed?.Invoke();
    }
    control.Layout = width =>
    {
      if (heading != null)
        heading.rectTransform.sizeDelta = new Vector2(width, 28);
      float y = top;
      foreach ((TMP_InputField input, Button up, Button down, Button delete) in rows)
      {
        float field = Mathf.Max(80, width - 3 * 40);
        SettingsUiFactory.Place((RectTransform)input.transform, 0, y, field, RowHeight);
        SettingsUiFactory.Place((RectTransform)up.transform, field + 4, y + 2, 36, 36);
        SettingsUiFactory.Place((RectTransform)down.transform, field + 44, y + 2, 36, 36);
        SettingsUiFactory.Place((RectTransform)delete.transform, field + 84, y + 2, 36, 36);
        y += RowHeight + 6;
      }
      SettingsUiFactory.Place((RectTransform)add.transform, 0, y + 2, 96, 36);
      control.Height = y + RowHeight;
    };
    return Add(control);
  }

  // A list row's icon button; reordering and removing make the parts again.
  private Button ListButton(
    ModEditorControl control,
    LayoutAction icon,
    string name,
    bool enabled,
    Action apply,
    Func<Button> self
  )
  {
    Button button = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "List " + name,
      "",
      0,
      0,
      36,
      36,
      14,
      () => _host.Change(self(), apply, true),
      true,
      _reduceMotion
    );
    button.interactable = enabled;
    Icon(button.transform, icon, 6, 6, 24, enabled ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted);
    return button;
  }

  private LayoutActionIcon Icon(Transform parent, LayoutAction kind, float x, float y, float size, Color color)
  {
    LayoutActionIcon icon = _ui.Rect("Icon", parent, x, y, size, size).gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.color = color;
    icon.raycastTarget = false;
    return icon;
  }

  private static LayoutAction LayoutIcon(EditorIcon icon) =>
    icon switch
    {
      EditorIcon.Add => LayoutAction.Plus,
      EditorIcon.Remove => LayoutAction.Minus,
      EditorIcon.Delete => LayoutAction.Delete,
      EditorIcon.Reset => LayoutAction.Reset,
      EditorIcon.Close => LayoutAction.Close,
      EditorIcon.Left => LayoutAction.Back,
      EditorIcon.Right => LayoutAction.Forward,
      EditorIcon.Up => LayoutAction.Up,
      EditorIcon.Down => LayoutAction.Down,
      EditorIcon.Undo => LayoutAction.Undo,
      EditorIcon.Redo => LayoutAction.Redo,
      EditorIcon.Text => LayoutAction.Text,
      _ => LayoutAction.Font,
    };

  private static float Snap(float value, float min, float max, float step)
  {
    value = Mathf.Clamp(value, min, max);
    return step > 0 ? Mathf.Clamp(min + Mathf.Round((value - min) / step) * step, min, max) : value;
  }

  // A label's one-line width, kept once it has been measured on screen: text
  // in a tab not picked yet measures short.
  private static Func<float> Preferred(TextMeshProUGUI label, string text)
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
}
