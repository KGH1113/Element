using System;
using System.Collections.Generic;
using Element.Domain.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// Large keys for a formula: 0–9, a point, + − × ÷, backspace, AC to start
/// over and Done. Keys that would do nothing are dimmed.
/// </summary>
internal sealed class OverlayKeypad
{
  public const float Key = 68,
    KeyHeight = 46,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    Height = 5 * KeyHeight + 4 * Gap;

  // Display symbols and the characters they type; backspace is '\b'.
  private static readonly (string Label, char Key)[] Keys =
  {
    ("7", '7'),
    ("8", '8'),
    ("9", '9'),
    ("÷", '/'),
    ("4", '4'),
    ("5", '5'),
    ("6", '6'),
    ("×", '*'),
    ("1", '1'),
    ("2", '2'),
    ("3", '3'),
    ("−", '-'),
    (".", '.'),
    ("0", '0'),
    ("", '\b'),
    ("+", '+'),
  };

  private readonly List<(Button Button, char Key)> _buttons = new();
  private readonly Button _clear,
    _done;

  public OverlayKeypad(
    SettingsUiFactory ui,
    Transform parent,
    Action<char> press,
    Action clear,
    Action done,
    bool reduceMotion
  )
  {
    Root = ui.Rect("Keypad", parent, 0, 0, Width, Height);
    for (int i = 0; i < Keys.Length; i++)
    {
      (string label, char key) = Keys[i];
      int row = i / 4,
        column = i % 4;
      bool symbol = column == 3;
      Button button = OverlayEditorUi.CapsuleButton(
        ui,
        Root,
        "Key " + (key == '\b' ? "backspace" : label),
        label,
        column * (Key + Gap),
        row * (KeyHeight + Gap),
        Key,
        KeyHeight,
        symbol ? 22 : 20,
        () => press(key),
        false,
        reduceMotion
      );
      // Operators read as a group in the accent.
      if (symbol)
        button.GetComponentInChildren<TextMeshProUGUI>().color = SettingsPalette.SoftAccent;
      if (key == '\b')
        Icon(ui, button.transform, LayoutAction.Backspace, 24);
      _buttons.Add((button, key));
    }
    float last = 4 * (KeyHeight + Gap);
    _clear = OverlayEditorUi.CapsuleButton(
      ui,
      Root,
      "Key clear",
      "AC",
      0,
      last,
      Key,
      KeyHeight,
      17,
      clear,
      false,
      reduceMotion
    );
    Button finish = _done = OverlayEditorUi.CapsuleButton(
      ui,
      Root,
      "Key done",
      L("완료", "Done"),
      Key + Gap,
      last,
      Key * 3 + Gap * 2,
      KeyHeight,
      17,
      done,
      false,
      reduceMotion
    );
    // Done is the one filled key: the way out of the keypad.
    Color accent = SettingsPalette.SoftAccent;
    ColorBlock colors = finish.colors;
    colors.normalColor = colors.selectedColor = new Color(accent.r, accent.g, accent.b, .32f);
    colors.highlightedColor = new Color(accent.r, accent.g, accent.b, .46f);
    colors.pressedColor = new Color(accent.r, accent.g, accent.b, .58f);
    ((SettingsButton)finish).SetPalette(colors);
  }

  public RectTransform Root { get; }

  public void Place(float x, float y) => SettingsUiFactory.Place(Root, x, y, Width, Height);

  public void Refresh(OverlayFormulaEditing editing)
  {
    foreach ((Button button, char key) in _buttons)
      button.interactable = key == '\b' ? editing.CanBackspace : editing.CanPress(key);
    _clear.interactable = editing.CanClear;
    _done.interactable = editing.Active;
  }

  /// <summary>Keyboard keys that act like the keypad: digits, point, + - * / and backspace.</summary>
  public static bool TryKeyboard(Event e, out char key)
  {
    key = '\0';
    if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete)
    {
      key = '\b';
      return true;
    }
    char c = e.character;
    if (char.IsDigit(c) || c == '.' || c == ',')
    {
      key = c == ',' ? '.' : c;
      return true;
    }
    char op = OverlayFormula.NormalizeOperator(c);
    if (op == '\0')
      return false;
    key = op;
    return true;
  }

  public static string ValueName(OverlayValueDefinition value) => Korean ? value.KoreanName : value.EnglishName;

  private static void Icon(SettingsUiFactory ui, Transform parent, LayoutAction kind, float size)
  {
    LayoutActionIcon icon = ui.Rect("Icon", parent, 0, 0, size, size).gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
    icon.rectTransform.anchoredPosition = Vector2.zero;
    icon.color = SettingsUiFactory.Foreground;
    icon.raycastTarget = false;
  }
}
