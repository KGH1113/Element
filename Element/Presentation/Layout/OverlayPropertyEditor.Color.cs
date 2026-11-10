using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using ElementKit.Elements;
using UnityEngine;

namespace Element.Presentation.Layout;

// Colors: the picker above the keypad for the color button pressed, with its
// opacity and, for a gradient, the picked color's position. A mod's colors
// fill boxes of its own, so the gradient has no handles over the element.
internal sealed partial class OverlayPropertyEditor
{
  private const string OpacityKey = "color-opacity",
    PositionKey = "color-position";

  private static bool PickerKey(string key) => key == OpacityKey || key == PositionKey;

  // As saved, or the mod's default when never changed; not to be changed in place.
  private OverlayColorSettings ColorOf(ColorProperty property) =>
    ModPropertyValues.Color(property, _working.Find(property.Key));

  // The saved color, made from the default the first time it is changed.
  private OverlayColorSettings EnsureColor(ColorProperty property)
  {
    OverlayPropertySetting saved = _working.Get(property.Key);
    saved.Color ??= ModPropertyValues.Color(property, null).Clone();
    return saved.Color;
  }

  private void OpenColor(ColorProperty property)
  {
    CloseEditing();
    // The same button closes its picker; another color button moves it over.
    if (_picker.Visible && _pickedColor == property)
    {
      _picker.Hide();
      Render();
      return;
    }
    _pickedColor = property;
    _pickerSnapshotTaken = false;
    ShowPicker();
    Render();
  }

  private void ShowPicker(bool keepStop = false)
  {
    ColorProperty property = _pickedColor;
    if (property == null)
      return;
    _picker.ShowSource(
      Name(property),
      ColorOf(property),
      color =>
      {
        if (!_pickerSnapshotTaken)
        {
          Remember(null);
          _pickerSnapshotTaken = true;
        }
        OverlayColorSettings before = ColorOf(property);
        if (!color.IsGradient)
          color = _opacity.Keep(before, color);
        // A moved slider replaces the opacity the dock is calculating.
        if (_field == OpacityKey && !OverlayOpacityField.Read(before).SameAs(OverlayOpacityField.Read(color)))
          _editing.Deactivate();
        ModPropertyValues.WriteColor(_working, property, color);
        Render();
      },
      keepStop
    );
  }

  private void PickerHidden()
  {
    _pickerSnapshotTaken = false;
    if (_screen == null || _picker == null)
      return;
    if (_field != null && PickerKey(_field))
      CloseEditing();
    Render();
  }

  private void BuildGradientPicker()
  {
    _position = new OverlayFormulaField(
      _ui,
      _picker.PositionSlot,
      "Color position",
      "%",
      () => Activate(PositionKey),
      _reduceMotion()
    );
    _picker.GradientBox = () => _size;
    _picker.ShownPosition = index => ShownStop(index, true);
    _picker.ShownAlpha = index => ShownStop(index, false);
    _picker.StopSelected = () =>
    {
      if (_field != null && PickerKey(_field))
        CloseEditing();
      Render();
    };
  }

  private float ShownStop(int index, bool position)
  {
    OverlayColorSettings color = _pickedColor == null ? null : ColorOf(_pickedColor);
    if (color == null || !color.IsGradient || index < 0 || index >= color.Gradient.Stops.Count)
      return position ? 0 : 1;
    OverlayGradientStop stop = color.Gradient.Stops[index];
    string formula = position ? stop.PositionFormula : stop.AlphaFormula;
    float plain = position ? stop.Position : stop.Alpha;
    double percent = OverlayFormulaText.Resolve(OverlayFormulaText.Compile(formula), _values.WithSamples, plain * 100);
    return double.IsNaN(percent) || double.IsInfinity(percent) ? plain : Mathf.Clamp01((float)percent / 100f);
  }

  // The picked color's position pill, while the picker shows a gradient.
  private void ShowGradient()
  {
    if (!_picker.Visible || _pickedColor == null)
      return;
    OverlayColorSettings color = ColorOf(_pickedColor);
    _picker.Refresh(color);
    if (!color.IsGradient)
      return;
    Show(_position, PositionKey);
    _picker.PlacePosition(_position.Width);
    _position.Place(0, 0);
  }

  private void ShowOpacity()
  {
    if (!_picker.Visible || _pickedColor == null)
      return;
    OverlayFormula formula = _field == OpacityKey && _editing.Active ? _editing.Working.Trimmed() : Read(OpacityKey);
    OverlayColorSettings color = ColorOf(_pickedColor);
    _opacity.Show(formula, Result(formula), PickedStop?.Alpha ?? color.Alpha, _field == OpacityKey);
  }

  private OverlayGradientStop PickedStop
  {
    get
    {
      OverlayColorSettings color = _pickedColor == null ? null : ColorOf(_pickedColor);
      int index = _picker?.SelectedStop ?? -1;
      return color != null && color.IsGradient && index >= 0 && index < color.Gradient.Stops.Count
        ? color.Gradient.Stops[index]
        : null;
    }
  }

  private int PickedRank
  {
    get
    {
      OverlayColorSettings color = _pickedColor == null ? null : ColorOf(_pickedColor);
      return color != null && color.IsGradient
        ? OverlayGradientEdits.Order(color.Gradient).IndexOf(_picker.SelectedStop) + 1
        : 1;
    }
  }

  // The picker's numbers: the color's (or the picked stop's) opacity, a stop's position.
  private OverlayFormula ReadColorNumber(string key)
  {
    if (!PickerKey(key) || _pickedColor == null)
      return null;
    OverlayColorSettings color = ColorOf(_pickedColor);
    OverlayGradientStop stop = PickedStop;
    if (key == OpacityKey)
    {
      if (stop == null)
        return OverlayOpacityField.Read(color);
      return string.IsNullOrEmpty(stop.AlphaFormula)
        ? OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(stop.Alpha) * 100))
        : OverlayFormula.Parse(stop.AlphaFormula);
    }
    if (stop == null)
      return new OverlayFormula();
    return string.IsNullOrEmpty(stop.PositionFormula)
      ? OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(stop.Position) * 1000) / 10)
      : OverlayFormula.Parse(stop.PositionFormula);
  }

  private bool WriteColorNumber(string key, OverlayFormula formula)
  {
    if (!PickerKey(key) || _pickedColor == null)
      return false;
    int stopIndex = _picker.SelectedStop;
    OverlayColorSettings color = EnsureColor(_pickedColor);
    bool plain = formula.IsPlainNumber(out double value);
    string text = plain ? "" : formula.Serialize();
    if (color.IsGradient && stopIndex >= 0 && stopIndex < color.Gradient.Stops.Count)
    {
      OverlayGradientStop stop = color.Gradient.Stops[stopIndex];
      if (key == OpacityKey)
      {
        if (plain)
          stop.Alpha = Mathf.Clamp01((float)value / 100f);
        stop.AlphaFormula = text;
      }
      else
      {
        if (plain)
          stop.Position = Mathf.Clamp01((float)value / 100f);
        stop.PositionFormula = text;
      }
      OverlayGradientEdits.Sync(color);
    }
    else if (key == OpacityKey)
      _working.Get(_pickedColor.Key).Color = OverlayOpacityField.Write(color, formula);
    _picker.Refresh(ColorOf(_pickedColor));
    return true;
  }

  // Delete removes the picked gradient color.
  private bool GradientKey(KeyCode code)
  {
    if (_editing.Active || !_picker.Visible || !_picker.IsGradient || _picker.IsTyping)
      return false;
    if (code != KeyCode.Delete && code != KeyCode.Backspace)
      return false;
    Remember(null);
    _picker.RemoveSelectedStop();
    return true;
  }
}
