using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Exact values for one handle: a title and one or two number fields. Enter
// applies, Escape closes. Two linked fields keep their ratio as either changes.
internal sealed class LayoutValuePopover : IDisposable
{
  public const float Width = 232;
  private const float Row = 48;
  private readonly SettingsUiFactory _ui;
  private readonly TextMeshProUGUI _title,
    _error;
  private readonly TMP_InputField[] _inputs = new TMP_InputField[2];
  private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[2];
  private readonly TextMeshProUGUI[] _units = new TextMeshProUGUI[2];
  private readonly float[] _min = new float[2],
    _max = new float[2];
  private Func<float[], string> _apply;
  private int _count;
  private float _ratio;
  private bool _linking;

  public LayoutValuePopover(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion)
  {
    _ui = new SettingsUiFactory(font) { ReduceMotion = reduceMotion };
    Image surface = _ui.Panel("Exact values", parent, 0, 0, Width, 160, new Color(.105f, .105f, .105f));
    surface.raycastTarget = true;
    Root = surface.rectTransform;
    _title = _ui.Text("", Root, 14, 8, Width - 60, 32, 15);
    _ui.IconButton("Close exact values", Root, Width - 48, 4, 40, Hide, 5);
    for (int i = 0; i < 2; i++)
    {
      int index = i;
      _labels[i] = _ui.Text("", Root, 14, 46 + i * Row, 70, 40, 14, SettingsUiFactory.Muted);
      _inputs[i] = _ui.Input(
        "Value " + (i + 1),
        "",
        Root,
        88,
        46 + i * Row,
        104,
        value => Linked(index, value),
        _ => { },
        8
      );
      _inputs[i].characterValidation = TMP_InputField.CharacterValidation.Decimal;
      _inputs[i].onSubmit.AddListener(_ => Commit());
      _inputs[i].gameObject.AddComponent<LayoutTextCapture>();
      _units[i] = _ui.Text("px", Root, 198, 46 + i * Row, 26, 40, 13, SettingsUiFactory.Muted);
    }
    _error = _ui.Text("", Root, 14, 0, Width - 28, 32, 11, new Color(1, .66f, .53f));
    Root.gameObject.SetActive(false);
  }

  public RectTransform Root { get; }
  public bool Visible => Root.gameObject.activeSelf;
  public bool IsTyping => Visible && (_inputs[0].isFocused || _inputs[1].isFocused);
  public float Height => 52 + _count * Row + 30;

  public readonly struct Field
  {
    public readonly string Label;
    public readonly float Value;
    public readonly float Min;
    public readonly float Max;
    public readonly string Unit;

    public Field(string label, float value, float min, float max, string unit = "px")
    {
      Label = label;
      Value = value;
      Min = min;
      Max = max;
      Unit = unit;
    }
  }

  /// <param name="apply">Returns an error to show, or null when the values were applied.</param>
  /// <param name="ratio">First over second field, kept while typing; zero for independent fields.</param>
  public void Show(string title, IReadOnlyList<Field> fields, Func<float[], string> apply, float ratio = 0)
  {
    _apply = apply;
    _count = Mathf.Clamp(fields.Count, 1, 2);
    _ratio = _count == 2 ? ratio : 0;
    _title.text = title;
    for (int i = 0; i < 2; i++)
    {
      bool used = i < _count;
      _labels[i].gameObject.SetActive(used);
      _inputs[i].gameObject.SetActive(used);
      _units[i].gameObject.SetActive(used);
      if (!used)
        continue;
      _labels[i].text = fields[i].Label;
      _units[i].text = fields[i].Unit;
      _min[i] = fields[i].Min;
      _max[i] = fields[i].Max;
      _inputs[i].SetTextWithoutNotify(Format(fields[i].Value));
    }
    _error.text = "";
    SettingsUiFactory.Place(_error.rectTransform, 14, 50 + _count * Row, Width - 28, 28);
    Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
    Root.gameObject.SetActive(true);
    Root.SetAsLastSibling();
    _inputs[0].Select();
    _inputs[0].ActivateInputField();
  }

  /// <summary>Validates and applies; false keeps the popover open with the reason.</summary>
  public bool Commit()
  {
    if (!Visible)
      return true;
    float[] values = new float[_count];
    for (int i = 0; i < _count; i++)
    {
      if (
        !float.TryParse(_inputs[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])
        || float.IsNaN(values[i])
        || float.IsInfinity(values[i])
        || values[i] < _min[i]
        || values[i] > _max[i]
      )
      {
        _error.text = L(
          _labels[i].text + "에 " + Format(_min[i]) + "–" + Format(_max[i]) + " 사이 숫자를 입력하세요",
          "Enter " + _labels[i].text + " from " + Format(_min[i]) + " to " + Format(_max[i])
        );
        _inputs[i].Select();
        _inputs[i].ActivateInputField();
        return false;
      }
    }
    string error = _apply?.Invoke(values);
    if (!string.IsNullOrEmpty(error))
    {
      _error.text = error;
      return false;
    }
    Hide();
    return true;
  }

  public void Hide()
  {
    if (!Visible)
      return;
    Root.gameObject.SetActive(false);
    UIInteractionState.SetTextEditing(false);
  }

  private void Linked(int index, string value)
  {
    _error.text = "";
    if (_linking || _ratio <= 0 || _count < 2)
      return;
    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
      return;
    _linking = true;
    int other = 1 - index;
    _inputs[other].SetTextWithoutNotify(Format(index == 0 ? number / _ratio : number * _ratio));
    _linking = false;
  }

  private static string Format(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

  public void Dispose()
  {
    _ui.Dispose();
    SettingsUiFactory.Destroy(Root.gameObject);
  }
}
