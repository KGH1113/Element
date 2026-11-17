using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Formula;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>
/// Every number field on the shape editor's screen with the key of what it
/// edits: W, H, the corners' roundings, and the picker's and the handles'
/// numbers while they show. Refilled each time it is asked, without new memory.
/// </summary>
internal sealed class ShapeFieldList
{
  private readonly ShapeMeasures _measures;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly List<FormulaFieldView> _fields = new();
  private readonly List<string> _keys = new();

  public ShapeFieldList(ShapeMeasures measures, ShapeColors colors, ShapeGradient gradient)
  {
    _measures = measures;
    _colors = colors;
    _gradient = gradient;
  }

  public int Count => _fields.Count;

  public FormulaFieldView this[int index] => _fields[index];

  public string Key(int index) => _keys[index];

  /// <summary>Lists the fields as the screen shows them now.</summary>
  public ShapeFieldList Collect()
  {
    _fields.Clear();
    _keys.Clear();
    Add(_measures.WidthField, ShapeMeasures.WidthKey);
    Add(_measures.HeightField, ShapeMeasures.HeightKey);
    for (int i = 0; i < _measures.RadiusFields.Count; i++)
      Add(_measures.RadiusFields[i], _measures.RadiusKey(i));
    if (_colors.Picker != null && _colors.Picker.Visible)
      Add(_colors.Opacity.Field, _colors.OpacityKey);
    if (_colors.Picker != null && _colors.Picker.Gradient.IsGradient)
      Add(_gradient.Position, _gradient.PositionKey);
    if (_gradient.Handles != null && _gradient.Handles.Visible)
      Add(_gradient.Handles.Angle, _gradient.AngleKey);
    return this;
  }

  private void Add(FormulaFieldView field, string key)
  {
    _fields.Add(field);
    _keys.Add(key);
  }
}
