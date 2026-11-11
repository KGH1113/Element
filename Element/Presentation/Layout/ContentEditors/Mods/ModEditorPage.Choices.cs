using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Settings;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

// Options: side by side, in a list, several at once, and a point of a box.
internal sealed partial class ModEditorPage
{
  public override EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    ModEditorControl control = Row("Choice", 0);
    float x = Caption(control, label);
    List<string> names = Names(options);
    SettingsSegmentedControl segments = null;
    segments = new SettingsSegmentedControl(
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
      SettingsUiFactory.Place(segments.Root, x, 2, room, 36);
      float segment = (room - 6) / names.Count;
      for (int i = 0; i < segments.Buttons.Count; i++)
        SettingsUiFactory.Place((RectTransform)segments.Buttons[i].transform, 3 + segment * i, 3, segment, 30);
    };
    return Add(control);
  }

  public override EditorControl Dropdown(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    ModEditorControl control = Row("Dropdown", 0);
    float x = Caption(control, label);
    List<string> names = Names(options);
    int current = Mathf.Clamp(selected, 0, names.Count - 1);
    Button button = null;
    TextMeshProUGUI shown = null;
    button = OverlayEditorUi.CapsuleButton(
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
            string.IsNullOrEmpty(label) ? SettingsLocale.L("고르기", "Choose") : label,
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
    shown = _ui.Text(names[current], button.transform, 14, 0, 200, 36, 14, SettingsUiFactory.Foreground);
    shown.alignment = TextAlignmentOptions.MidlineLeft;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    shown.overflowMode = TextOverflowModes.Ellipsis;
    shown.raycastTarget = false;
    LayoutActionIcon chevron = Icon(button.transform, LayoutAction.Down, 0, 9, 18, SettingsUiFactory.Muted);
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x);
      SettingsUiFactory.Place((RectTransform)button.transform, x, 2, room, 36);
      shown.rectTransform.sizeDelta = new Vector2(room - 48, 36);
      SettingsUiFactory.Place(chevron.rectTransform, room - 30, 9, 18, 18);
    };
    return Add(control);
  }

  public override EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  )
  {
    ModEditorControl control = Row("Multi choice", 0);
    float x = Caption(control, label, 36);
    List<string> names = Names(options);
    List<(Button Chip, Func<float> Width)> chips = new();
    for (int i = 0; i < names.Count; i++)
    {
      int index = i;
      bool state = on != null && index < on.Count && on[index];
      Button chip = null;
      chip = OverlayEditorUi.CapsuleButton(
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
      chips.Add((chip, Preferred(chip.GetComponentInChildren<TextMeshProUGUI>(), names[i])));
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
        SettingsUiFactory.Place((RectTransform)chip.transform, x + left, top, w, 32);
        left += w + 6;
      }
      control.Height = top + 36;
    };
    return Add(control);
  }

  public override EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed)
  {
    ModEditorControl control = Row("Anchor", OverlayGrowPicker.Size + 4);
    float x = Caption(control, label, OverlayGrowPicker.Size);
    Anchor current = value;
    OverlayGrowPicker picker = null;
    picker = new OverlayGrowPicker(
      _ui,
      control.Rect,
      _reduceMotion,
      growth =>
      {
        if (!Enum.IsDefined(typeof(Anchor), (int)growth) || (Anchor)(int)growth == current)
          return;
        current = (Anchor)(int)growth;
        picker.Show(growth, (Anchor9)(int)growth);
        Anchor now = current;
        _host.Change(picker, () => changed?.Invoke(now), false);
      }
    );
    // Only the nine points: an element's own Auto and caption don't apply here.
    picker.Auto.gameObject.SetActive(false);
    picker.Caption.gameObject.SetActive(false);
    picker.Show((OverlayGrowth)(int)current, (Anchor9)(int)current);
    control.Layout = _ => SettingsUiFactory.Place(picker.Root, x, 2, OverlayGrowPicker.Size, OverlayGrowPicker.Size);
    return Add(control);
  }

  // A picked chip is lit in the accent; others look like any capsule button.
  private void Tint(Button chip, bool on)
  {
    if (chip is not SettingsButton button)
      return;
    Color accent = SettingsPalette.SoftAccent;
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
