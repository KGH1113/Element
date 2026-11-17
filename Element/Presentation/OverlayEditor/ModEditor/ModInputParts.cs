using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementKit;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// The page's inputs: labels, buttons, switches, sliders, numbers, text
/// fields and lists. Each is ElementUI's own control, so the column draws
/// it as its row (<see cref="ModColumnStyle"/>) as it does a screen a mod
/// writes with ElementUI.
/// </summary>
internal sealed class ModInputParts
{
  private const float IconSide = 30;

  private readonly ModEditorPage _page;
  private readonly IModEditorActions _editor;
  private readonly ModCommandsViewModel _commands;

  public ModInputParts(ModEditorPage page)
  {
    _page = page;
    _editor = page.Editor;
    _commands = page.Commands;
  }

  // Words about the parts near them: quieter than a row's name.
  public EditorControl Label(string text) =>
    _page.Add(
      _page.Part(() =>
        UI.Text(text ?? "")
          .Font(Font.Caption.Sized(13))
          .Foreground(Colors.Muted)
          .Padding(new Insets(2, 4, 2, 4))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      )
    );

  /// <summary>A button; after its click the parts are made again, to show what it changed.</summary>
  public EditorControl Button(string text, Action clicked)
  {
    ModPart part = _page.Part();
    Command click = _commands.Make(() => _editor.Change(part, () => clicked?.Invoke(), true));
    part.Control = () =>
      UI.Button(
          UI.Text(text ?? "").Font(Font.Caption.Sized(13)).LineLimit(1).Frame(maxWidth: float.PositiveInfinity),
          () => click
        )
        .Named("Button " + text);
    return _page.Add(part);
  }

  public EditorControl IconButton(EditorIcon icon, string hint, Action clicked)
  {
    ModPart part = _page.Part();
    Command click = _commands.Make(() => _editor.Change(part, () => clicked?.Invoke(), true));
    part.Control = () => Icon(IconOf(icon), "Icon " + icon, () => click);
    _page.Add(part);
    part.Hint(hint);
    return part;
  }

  public EditorControl Switch(string label, bool on, Action<bool> changed)
  {
    ModPart part = _page.Part();
    ModValueViewModel<bool> value = _page.Value(part, on, changed);
    part.Control = () => Toggle(label ?? "", Bind(() => value.Value));
    return _page.Add(part);
  }

  public EditorControl Checkbox(string text, bool on, Action<bool> changed)
  {
    ModPart part = _page.Part();
    ModValueViewModel<bool> value = _page.Value(part, on, changed);
    part.Control = () => Toggle(text ?? "", Bind(() => value.Value), ToggleAppearance.Checkbox);
    return _page.Add(part);
  }

  public EditorControl Slider(string label, float value, float min, float max, Action<float> changed, float step = 0)
  {
    if (!(max > min))
      max = min + 1;
    ModPart part = _page.Part();
    ModValueViewModel<float> current = _page.Value(part, Snap(value, min, max, step), changed);
    part.Control = () => UI.Slider(label ?? "", Bind(() => current.Value), min, max, step);
    return _page.Add(part);
  }

  public EditorControl RangeSlider(
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
    ModPart part = _page.Part();
    ModRange range = new(
      Snap(Mathf.Min(low, high), min, max, step),
      Snap(Mathf.Max(low, high), min, max, step),
      (a, b) => _editor.Change(part, () => changed?.Invoke(a, b), false)
    );
    part.Control = () => UI.RangeSlider(label ?? "", Bind(() => range.Low), Bind(() => range.High), min, max, step);
    return _page.Add(part);
  }

  public EditorControl Number(string label, float value, float min, float max, Action<float> changed)
  {
    if (!(max >= min))
      max = min;
    ModPart part = _page.Part();
    ModValueViewModel<float> current = _page.Value(part, Mathf.Clamp(value, min, max), changed);
    // As it is typed a number within the limits shows at once; when typing is
    // done one outside them is brought to the nearest.
    part.Control = () => NumberField(label ?? "", Bind(() => current.Value), min, max);
    return _page.Add(part);
  }

  public EditorControl Stepper(string label, int value, int min, int max, Action<int> changed, int step = 1)
  {
    if (max < min)
      max = min;
    step = Mathf.Max(1, step);
    ModPart part = _page.Part();
    ModValueViewModel<int> current = _page.Value(part, Mathf.Clamp(value, min, max), changed);
    part.Control = () => UI.Stepper(label ?? "", Bind(() => current.Value), min, max, step);
    return _page.Add(part);
  }

  public EditorControl TextField(string label, string value, Action<string> changed)
  {
    ModPart part = _page.Part();
    // Each letter shows on the preview as it is typed.
    ModValueViewModel<string> current = _page.Value(part, value ?? "", changed);
    part.Control = () => UI.TextField(label ?? "", Bind(() => current.Value));
    return _page.Add(part);
  }

  /// <summary>Words to add to, remove from, reorder and type in; adding, moving and removing make the parts again.</summary>
  public EditorControl List(string label, List<string> items, Action changed = null, string newItem = "")
  {
    items ??= new List<string>();
    ModPart part = _page.Part();
    List<View> rows = new();
    View Rows()
    {
      rows.Clear();
      if (!string.IsNullOrEmpty(label))
        rows.Add(InspectorRowStyle.Heading(label));
      for (int i = 0; i < items.Count; i++)
        rows.Add(ListRow(part, items, i, changed));
      Command add = _commands.Make(() =>
        _editor.Change(
          part,
          () =>
          {
            items.Add(newItem ?? "");
            changed?.Invoke();
          },
          true
        )
      );
      rows.Add(
        UI.Button(
            UI.Text(L("+ 추가", "+ Add")).Font(Font.Caption.Sized(13)).Frame(maxWidth: float.PositiveInfinity),
            () => add
          )
          .Named("List add")
      );
      return VStack(spacing: InspectorRowStyle.Gap, HAlignment.Leading, rows.ToArray())
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
    }
    part.Control = Rows;
    return _page.Add(part);
  }

  // A list row: the words, then move up, move down and remove.
  private View ListRow(ModPart part, List<string> items, int index, Action changed)
  {
    ModValueViewModel<string> text = _page.Value<string>(
      part,
      items[index] ?? "",
      typed =>
      {
        if (index >= items.Count)
          return;
        items[index] = typed;
        changed?.Invoke();
      }
    );
    void Swap(int a, int b)
    {
      (items[a], items[b]) = (items[b], items[a]);
      changed?.Invoke();
    }
    Command up = _commands.Make(() => _editor.Change(part, () => Swap(index, index - 1), true));
    Command down = _commands.Make(() => _editor.Change(part, () => Swap(index, index + 1), true));
    Command remove = _commands.Make(() =>
      _editor.Change(
        part,
        () =>
        {
          items.RemoveAt(index);
          changed?.Invoke();
        },
        true
      )
    );
    return HStack(
        spacing: 2,
        UI.TextField(Bind(() => text.Value))
          .ControlStyle(ControlStyle.Standard)
          .Named("List item " + (index + 1))
          .Frame(maxWidth: float.PositiveInfinity, height: 30)
          .Padding(new Insets(0, 0, 0, 6)),
        Icon(UiIconKind.Up, "List Move up", () => up).Disabled(index == 0),
        Icon(UiIconKind.Down, "List Move down", () => down).Disabled(index == items.Count - 1),
        Icon(UiIconKind.Delete, "List Remove", () => remove)
      )
      .Padding(new Insets(0, 5, 0, 5))
      .Frame(maxWidth: float.PositiveInfinity, height: InspectorRowStyle.Height)
      .Background(InspectorRowStyle.Fill, InspectorRowStyle.Radius);
  }

  /// <summary>A small square button with an icon, on an opaque fill so the dim behind never shows through it.</summary>
  private static View Icon(UiIconKind icon, string name, System.Linq.Expressions.Expression<Func<Command>> command) =>
    UI.Button(
        Image(ElementIcons.Of(icon), 18).Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        command,
        ButtonStyle.Plain
      )
      .Background(UiPalette.Wash(UiPalette.EditorControl, .06f), 8)
      .Named(name)
      .Frame(width: IconSide, height: IconSide);

  private static UiIconKind IconOf(EditorIcon icon) =>
    icon switch
    {
      EditorIcon.Add => UiIconKind.Plus,
      EditorIcon.Remove => UiIconKind.Minus,
      EditorIcon.Delete => UiIconKind.Delete,
      EditorIcon.Reset => UiIconKind.Reset,
      EditorIcon.Close => UiIconKind.Close,
      EditorIcon.Left => UiIconKind.Back,
      EditorIcon.Right => UiIconKind.Forward,
      EditorIcon.Up => UiIconKind.Up,
      EditorIcon.Down => UiIconKind.Down,
      EditorIcon.Undo => UiIconKind.Undo,
      EditorIcon.Redo => UiIconKind.Redo,
      EditorIcon.Text => UiIconKind.Text,
      _ => UiIconKind.Font,
    };

  private static float Snap(float value, float min, float max, float step)
  {
    value = Mathf.Clamp(value, min, max);
    return step > 0 ? Mathf.Clamp(min + Mathf.Round((value - min) / step) * step, min, max) : value;
  }

  /// <summary>Two numbers on one slider: the lower never past the higher; either moving hands both on.</summary>
  private sealed class ModRange : ViewModel
  {
    private readonly Action<float, float> _changed;
    private float _low,
      _high;

    public ModRange(float low, float high, Action<float, float> changed)
    {
      _low = low;
      _high = high;
      _changed = changed;
    }

    public float Low
    {
      [ElementUI.Pure]
      get => _low;
      set => Set(Mathf.Min(value, _high), _high);
    }

    public float High
    {
      [ElementUI.Pure]
      get => _high;
      set => Set(_low, Mathf.Max(value, _low));
    }

    private void Set(float low, float high)
    {
      if (Mathf.Approximately(low, _low) && Mathf.Approximately(high, _high))
        return;
      _low = low;
      _high = high;
      _changed(low, high);
    }
  }
}
