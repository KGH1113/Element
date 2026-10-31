using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>
/// A labeled slider with a field for typing the exact whole number. The field
/// applies on Enter or when focus leaves, so typing "1" on the way to "15" is
/// never rejected. A number outside the range becomes the nearest end.
/// </summary>
internal sealed class SettingsValueRow
{
  public const float Height = 92;
  private const float FieldWidth = 84;

  private readonly int _minimum,
    _maximum;
  private readonly Func<int> _read;
  private readonly Action<int> _write;

  public RectTransform Row { get; }
  public Slider Slider { get; }
  public TMP_InputField Field { get; }

  public SettingsValueRow(
    SettingsUiFactory ui,
    Transform parent,
    string name,
    string label,
    string unit,
    int minimum,
    int maximum,
    float step,
    Func<int> read,
    Action<int> write
  )
  {
    _minimum = minimum;
    _maximum = maximum;
    _read = read;
    _write = write;
    int value = Clamp(read());

    Row = ui.Panel(name, parent, 0, 0, 936, Height, SettingsUiFactory.Surface).rectTransform;
    ui.Text(label, Row, 16, 12, 300, 24, 14, SettingsUiFactory.Muted);
    Slider = ui.Slider(name + " slider", Row, 8, 44, 920 - FieldWidth - 8, 0, Slide, step);
    // Unity reports a range change that clamps the value as a user change. Place
    // the saved value before raising the minimum so building the row never
    // overwrites the saved setting.
    Slider.maxValue = maximum;
    Slider.wholeNumbers = true;
    Slider.SetValueWithoutNotify(value);
    Slider.minValue = minimum;

    Field = ui.Input(
      name + " value",
      Format(value),
      Row,
      936 - 16 - FieldWidth,
      42,
      FieldWidth,
      null,
      Commit,
      maximum.ToString(CultureInfo.InvariantCulture).Length
    );
    Field.contentType = TMP_InputField.ContentType.IntegerNumber;
    SettingsUiFactory.Stretch(Field.textViewport, 10, 4, 36, 4);
    Field.textComponent.alignment = TextAlignmentOptions.Right;
    // Right-aligned text puts the caret on the field's edge; keep room for it.
    Field.textComponent.margin = new Vector4(0, 0, 2, 0);
    Field.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    ui.Text(unit, Field.transform, FieldWidth - 32, 0, 22, 40, 13, SettingsUiFactory.Muted);
  }

  /// <summary>Shows the saved value unless the player is typing.</summary>
  public void Sync(bool interactable)
  {
    Slider.interactable = interactable;
    Field.interactable = interactable;
    Field.textComponent.color = interactable ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted;
    if (Field.isFocused)
      return;
    int value = Clamp(_read());
    Slider.SetValueWithoutNotify(value);
    Field.SetTextWithoutNotify(Format(value));
  }

  public void SetVisible(bool visible) => Row.gameObject.SetActive(visible);

  public void Layout(float x, float y, float width)
  {
    SettingsUiFactory.Place(Row, x, y, width, Height);
    SettingsUiFactory.Place((RectTransform)Slider.transform, 8, 44, width - 16 - FieldWidth - 8, 36);
    SettingsUiFactory.Place((RectTransform)Field.transform, width - 16 - FieldWidth, 42, FieldWidth, 40);
  }

  private void Slide(float value)
  {
    int rounded = Clamp(Mathf.RoundToInt(value));
    if (rounded != _read())
      Apply(rounded);
  }

  // An empty or unreadable entry keeps the saved value.
  private void Commit(string text)
  {
    int value = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int typed)
      ? Clamp(typed)
      : Clamp(_read());
    Slider.SetValueWithoutNotify(value);
    Field.SetTextWithoutNotify(Format(value));
    if (value != _read())
      _write(value);
  }

  private void Apply(int value)
  {
    Field.SetTextWithoutNotify(Format(value));
    _write(value);
  }

  private int Clamp(int value) => Math.Max(_minimum, Math.Min(_maximum, value));

  private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
