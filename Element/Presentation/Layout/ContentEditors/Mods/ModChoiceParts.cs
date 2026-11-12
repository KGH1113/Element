using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>The page's choices: segments, dropdowns, chips and the anchor picker.</summary>
internal sealed class ModChoiceParts
{
  private readonly ModEditorPage _page;
  private readonly UiFactory _ui;
  private readonly IModEditorHost _host;
  private readonly bool _reduceMotion;

  public ModChoiceParts(ModEditorPage page)
  {
    _page = page;
    _ui = page.Ui;
    _host = page.Host;
    _reduceMotion = page.ReduceMotion;
  }

  public EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    ModEditorControl control = _page.Row("Choice", 0);
    float x = _page.Caption(control, label);
    List<string> names = Names(options);
    UiSegmentedControl segments = null;
    segments = new UiSegmentedControl(
      _ui,
      control.Rect,
      x,
      2,
      240,
      36,
      names,
      index => _host.Change(segments, () => changed?.Invoke(index), false)
    );
    segments.Select(Mathf.Clamp(selected, 0, names.Count - 1), false);
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x);
      UiFactory.Place(segments.Root, x, 2, room, 36);
      float segment = (room - 6) / names.Count;
      for (int i = 0; i < segments.Buttons.Count; i++)
        UiFactory.Place((RectTransform)segments.Buttons[i].transform, 3 + segment * i, 3, segment, 30);
    };
    return _page.Add(control);
  }

  public EditorControl Dropdown(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    ModEditorControl control = _page.Row("Dropdown", 0);
    float x = _page.Caption(control, label);
    List<string> names = Names(options);
    int current = Mathf.Clamp(selected, 0, names.Count - 1);
    Button button = null;
    TextMeshProUGUI shown = null;
    button = UiCapsules.CapsuleButton(
      _ui,
      control.Rect,
      "Dropdown " + label,
      "",
      x,
      2,
      240,
      36,
      14,
      () =>
        _host.OpenList(
          new ModListRequest(
            string.IsNullOrEmpty(label) ? UiLocale.L("고르기", "Choose") : label,
            names,
            current,
            (RectTransform)button.transform,
            index =>
            {
              if (index == current)
                return;
              current = index;
              shown.text = names[current];
              int now = current;
              _host.Change(button, () => changed?.Invoke(now), false);
            }
          )
        ),
      false,
      _reduceMotion
    );
    shown = _ui.Text(names[current], button.transform, 14, 0, 200, 36, 14, UiPalette.Foreground);
    shown.alignment = TextAlignmentOptions.MidlineLeft;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    shown.overflowMode = TextOverflowModes.Ellipsis;
    shown.raycastTarget = false;
    UiIcon chevron = _page.Icon(button.transform, UiIconKind.Down, 0, 9, 18, UiPalette.Muted);
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x);
      UiFactory.Place((RectTransform)button.transform, x, 2, room, 36);
      shown.rectTransform.sizeDelta = new Vector2(room - 48, 36);
      UiFactory.Place(chevron.rectTransform, room - 30, 9, 18, 18);
    };
    return _page.Add(control);
  }

  public EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  )
  {
    ModEditorControl control = _page.Row("Multi choice", 0);
    float x = _page.Caption(control, label, 36);
    List<string> names = Names(options);
    List<(Button Chip, Func<float> Width)> chips = new();
    for (int i = 0; i < names.Count; i++)
    {
      int index = i;
      bool state = on != null && index < on.Count && on[index];
      Button chip = null;
      chip = UiCapsules.CapsuleButton(
        _ui,
        control.Rect,
        "Chip " + names[i],
        names[i],
        0,
        0,
        80,
        32,
        14,
        () =>
        {
          state = !state;
          Tint(chip, state);
          bool now = state;
          _host.Change(chip, () => changed?.Invoke(index, now), false);
        },
        false,
        _reduceMotion
      );
      Tint(chip, state);
      chips.Add((chip, ModEditorPage.Preferred(chip.GetComponentInChildren<TextMeshProUGUI>(), names[i])));
    }
    control.Layout = width =>
    {
      // Chips wrap onto more lines, each line as wide as the room beside the label.
      float room = Mathf.Max(120, width - x),
        left = 0,
        top = 2;
      foreach ((Button chip, Func<float> chipWidth) in chips)
      {
        float w = Mathf.Min(Mathf.Max(48, chipWidth() + 28), room);
        if (left > 0 && left + w > room)
        {
          left = 0;
          top += 38;
        }
        UiFactory.Place((RectTransform)chip.transform, x + left, top, w, 32);
        left += w + 6;
      }
      control.Height = top + 36;
    };
    return _page.Add(control);
  }

  public EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed)
  {
    const float size = OverlayGrowPointsView.Size;
    ModEditorControl control = _page.Row("Anchor", size + 4);
    float x = _page.Caption(control, label, size);
    Anchor current = value;
    OverlayGrowPickerModel picker = null;
    picker = new OverlayGrowPickerModel(growth =>
    {
      if (!Enum.IsDefined(typeof(Anchor), (int)growth) || (Anchor)(int)growth == current)
        return;
      current = (Anchor)(int)growth;
      picker.Show(growth, (Anchor9)(int)growth);
      Anchor now = current;
      _host.Change(picker, () => changed?.Invoke(now), false);
    });
    picker.Show((OverlayGrowth)(int)current, (Anchor9)(int)current);
    // Only the nine points: an element's own Auto and caption don't apply here.
    ScreenPanel points = new("Anchor points", control.Rect, Color.clear, keyboard: false);
    points.Mount(_host.NewBackend(), new OverlayGrowPointsView(picker), picker, size);
    control.Layout = _ => UiFactory.Place(points.Root, x, 2, size, size);
    return _page.Add(control);
  }

  // A picked chip is lit in the accent; others look like any capsule button.
  private void Tint(Button chip, bool on)
  {
    if (chip is not UiButton button)
      return;
    Color accent = UiPalette.SoftAccent;
    button.SetPalette(
      new ColorBlock
      {
        normalColor = on ? new Color(accent.r, accent.g, accent.b, .38f) : new Color(1, 1, 1, .07f),
        highlightedColor = on ? new Color(accent.r, accent.g, accent.b, .5f) : new Color(1, 1, 1, .15f),
        pressedColor = on ? new Color(accent.r, accent.g, accent.b, .6f) : new Color(1, 1, 1, .22f),
        selectedColor = on ? new Color(accent.r, accent.g, accent.b, .38f) : new Color(1, 1, 1, .07f),
        disabledColor = new Color(1, 1, 1, .03f),
        colorMultiplier = 1,
        fadeDuration = _reduceMotion ? 0 : .08f,
      }
    );
  }

  private static List<string> Names(IReadOnlyList<string> options)
  {
    List<string> names = new();
    if (options != null)
      foreach (string option in options)
        names.Add(option ?? "");
    if (names.Count == 0)
      names.Add("");
    return names;
  }
}
