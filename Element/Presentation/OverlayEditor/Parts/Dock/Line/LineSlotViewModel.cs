using System;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// One variable on the line: the field's own, or a slot of a condition or a
/// group laid out in place. Empty, it shows what goes in it and its type.
/// A line keeps the same one while the route to it stays.
/// </summary>
internal sealed class LineSlotViewModel : ViewModel
{
  public LineSlotViewModel(string key) => Key = key;

  public readonly string Key;

  /// <summary>How to get to it from the field's variable.</summary>
  public FormulaStep[] Route = Array.Empty<FormulaStep>();

  /// <summary>What it must give.</summary>
  public OverlayValueType Type;

  /// <summary>What goes in it, said while it is empty: "결과값", "숫자 넣기".</summary>
  public string Placeholder = "";

  /// <summary>The field's own variable, drawn without a box.</summary>
  public bool Root;

  /// <summary>Picked as a place, with no piece in it picked: a value goes in at its end.</summary>
  public bool Targeted;

  /// <summary>It is empty where something must be: the box reads warm.</summary>
  public bool Missing;

  public LinePieceViewModel[] Pieces = Array.Empty<LinePieceViewModel>();

  public bool Empty => Pieces.Length == 0;
}
