using System;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// One piece on the line, at <see cref="Index"/> of the variable
/// <see cref="Route"/> leads to: a chip, an operator, a condition laid out
/// with its slots, or a boxed calculation. A click on it picks it.
/// </summary>
internal sealed class LinePieceViewModel : ViewModel
{
  public LinePieceViewModel(string key, LinePieceKind kind)
  {
    Key = key;
    Kind = kind;
  }

  public readonly string Key;

  public readonly LinePieceKind Kind;

  public FormulaStep[] Route = Array.Empty<FormulaStep>();

  public int Index;

  /// <summary>What it says: "120", "“%”", "X-정확도", "×".</summary>
  public string Label = "";

  /// <summary>What it gives, for its mark.</summary>
  public OverlayValueType Type;

  /// <summary>A color: drawn as a swatch instead of a mark.</summary>
  public bool HasSwatch;

  public UiColor Swatch;

  public bool Selected;

  /// <summary>A condition's slots in order (compared value, comparison, compared with, result, otherwise), or a group's one.</summary>
  public LineSlotViewModel[] Slots = Array.Empty<LineSlotViewModel>();
}
