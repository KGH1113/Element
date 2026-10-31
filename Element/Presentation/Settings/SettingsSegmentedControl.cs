using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

// shadcn's toggle group: one track, the chosen segment raised in the accent surface.
internal sealed class SettingsSegmentedControl
{
  private readonly SettingsUiFactory _ui;
  private readonly List<Button> _buttons = new();
  private readonly List<TextMeshProUGUI> _labels = new();
  private readonly Action<int> _changed;
  private int _selected = -1;

  public SettingsSegmentedControl(
    SettingsUiFactory ui,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    IReadOnlyList<string> labels,
    Action<int> changed,
    float fontSize = 14
  )
  {
    _ui = ui;
    _changed = changed;
    Image track = ui.Panel("Segments", parent, x, y, width, height, new Color(.16f, .16f, .16f));
    track.raycastTarget = true;
    Root = track.rectTransform;
    float segment = (width - 6) / Mathf.Max(1, labels.Count);
    for (int i = 0; i < labels.Count; i++)
    {
      int index = i;
      Button button = ui.Button(
        labels[i],
        Root,
        3 + segment * i,
        3,
        segment,
        height - 6,
        () => Select(index, true),
        themed: false
      );
      button.name = labels[i];
      TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
      label.fontSize = fontSize;
      label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
      SettingsUiFactory.Stretch(label.rectTransform, 6, 0, 6, 0);
      _buttons.Add(button);
      _labels.Add(label);
    }
    ui.Bind(Restyle);
  }

  public RectTransform Root { get; }

  public int Selected
  {
    get => _selected;
    set => Select(value, false);
  }

  public IReadOnlyList<Button> Buttons => _buttons;

  public void Select(int index, bool notify)
  {
    if (index < -1 || index >= _buttons.Count)
      return;
    bool changed = _selected != index;
    _selected = index;
    Restyle();
    if (notify && changed)
      _changed?.Invoke(index);
  }

  private void Restyle()
  {
    for (int i = 0; i < _buttons.Count; i++)
    {
      bool selected = i == _selected;
      Color fill = selected ? _ui.AccentSurface : new Color(.16f, .16f, .16f, 0);
      ColorBlock colors = _buttons[i].colors;
      colors.normalColor = fill;
      colors.selectedColor = fill;
      colors.highlightedColor = selected ? fill : new Color(1, 1, 1, .06f);
      colors.pressedColor = Color.Lerp(_ui.AccentSurface, Color.black, .2f);
      colors.disabledColor = new Color(0, 0, 0, 0);
      colors.fadeDuration = _ui.ReduceMotion() ? 0 : .1f;
      _buttons[i].targetGraphic.color = Color.white;
      if (_buttons[i] is SettingsButton button)
        button.SetPalette(colors);
      else
        _buttons[i].colors = colors;
      _labels[i].color = selected ? _ui.SoftAccent : SettingsUiFactory.Muted;
    }
  }
}
