using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>One track of segments, the chosen one raised in the accent surface; <see cref="SettingsSegmentStyle"/> sets the look.</summary>
internal sealed class SettingsSegmentedControl
{
  private readonly SettingsUiFactory _ui;
  private readonly SettingsSegmentStyle _style;
  private readonly List<Button> _buttons = new();
  private readonly List<TextMeshProUGUI> _labels = new();
  private readonly Action<int> _changed;
  private readonly float _segmentHeight;
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
    float fontSize = 14,
    SettingsSegmentStyle style = null
  )
  {
    _ui = ui;
    _style = style ?? SettingsSegmentStyle.Toggle;
    _changed = changed;
    float inset = _style.Inset;
    _segmentHeight = height - inset * 2;
    Image track = ui.Panel(_style.Name, parent, x, y, width, height, _style.Track);
    track.raycastTarget = _style.TrackTakesClicks;
    Root = track.rectTransform;
    float segment = (width - inset * 2) / Mathf.Max(1, labels.Count);
    for (int i = 0; i < labels.Count; i++)
    {
      int index = i;
      Button button = ui.Button(
        labels[i],
        Root,
        inset + segment * i,
        inset,
        segment,
        _segmentHeight,
        () => Select(index, true),
        themed: false
      );
      button.name = labels[i];
      TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
      label.fontSize = fontSize;
      if (!_style.LabelWraps)
        label.textWrappingMode = TextWrappingModes.NoWrap;
      SettingsUiFactory.Stretch(label.rectTransform, _style.LabelInset, 0, _style.LabelInset, 0);
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
    if (notify && (changed || _style.ClicksRepeat))
      _changed?.Invoke(index);
  }

  /// <summary>
  /// Places the control again, its segments in rows of <paramref name="columns"/>
  /// (all in one row when 0) that keep their first height.
  /// </summary>
  public void Place(float x, float y, float width, float height, int columns = 0)
  {
    SettingsUiFactory.Place(Root, x, y, width, height);
    if (columns <= 0)
      columns = _buttons.Count;
    float inset = _style.Inset;
    float segment = (width - inset * 2) / columns;
    for (int i = 0; i < _buttons.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_buttons[i].transform,
        inset + i % columns * segment,
        inset + i / columns * (_segmentHeight + inset),
        segment,
        _segmentHeight
      );
  }

  private void Restyle()
  {
    for (int i = 0; i < _buttons.Count; i++)
    {
      bool selected = i == _selected;
      ColorBlock colors = _style.Colors(_buttons[i].colors, selected, _ui);
      _buttons[i].targetGraphic.color = Color.white;
      if (_buttons[i] is SettingsButton button)
        button.SetPalette(colors);
      else
        _buttons[i].colors = colors;
      _labels[i].color = selected ? _ui.SoftAccent : SettingsUiFactory.Muted;
    }
  }
}
