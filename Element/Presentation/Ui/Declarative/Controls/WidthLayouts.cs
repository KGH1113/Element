using System;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Layouts that change shape with the width they are offered. Bring them in with
/// <c>using static Element.Presentation.Ui.Declarative.Controls.WidthLayouts;</c>.
/// </summary>
internal static class WidthLayouts
{
  /// <summary>
  /// Views in equal columns, as many as fit at <paramref name="minColumnWidth"/>
  /// each and at most <paramref name="maxColumns"/>: side by side on a wide
  /// window, one under another on a narrow one. <paramref name="items"/> makes
  /// the views; it runs once for each column count the window has needed.
  /// </summary>
  public static View Columns(float minColumnWidth, int maxColumns, float spacing, Func<View[]> items)
  {
    State<int> columns = new(maxColumns);
    return VStack(
      spacing: 0,
      new WidthProbe(width => columns.Value = ColumnsThatFit(width, minColumnWidth, maxColumns, spacing)),
      Laid(ColumnsOf(columns, maxColumns, spacing, items))
    );
  }

  /// <summary><paramref name="wide"/> while the width offered is at least <paramref name="minWidth"/>, else <paramref name="narrow"/>.</summary>
  public static View WideOrNarrow(float minWidth, Func<View> wide, Func<View> narrow)
  {
    State<bool> isWide = new(true);
    return VStack(
      spacing: 0,
      new WidthProbe(width => isWide.Value = width >= minWidth),
      Laid(If(() => isWide.Value, wide, narrow))
    );
  }

  // An If straight in a stack hands its branch to the stack and is never laid
  // out itself, so a branch that changes after the first layout would not be
  // laid out (ElementUI stops invalidating at it). A frame lays the If out.
  private static View Laid(View branches) =>
    branches.Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);

  private static int ColumnsThatFit(float width, float minColumnWidth, int maxColumns, float spacing) =>
    Mathf.Clamp(Mathf.FloorToInt((width + spacing) / (minColumnWidth + spacing)), 1, maxColumns);

  // A grid of `count` columns while that many fit, else the next smaller one.
  private static View ColumnsOf(State<int> columns, int count, float spacing, Func<View[]> items) =>
    count <= 1
      ? Grid(1, spacing, items())
      : If(
        () => columns.Value >= count,
        () => Grid(count, spacing, items()),
        () => ColumnsOf(columns, count - 1, spacing, items)
      );

  /// <summary>
  /// Draws nothing and says the width it was placed at. ElementUI has no layout
  /// that picks its shape by the width offered; this tells one through a State.
  /// The width changes only with the window, so it settles after one layout.
  /// </summary>
  private sealed class WidthProbe : NativeView
  {
    private readonly Action<float> _placed;

    public WidthProbe(Action<float> placed) => _placed = placed;

    protected override void Build(RectTransform rect) { }

    protected override ElementUI.Size PreferredSize(Proposal proposal) =>
      new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 0, 0);

    protected override void Resized(float width, float height)
    {
      if (width > 0)
        _placed(width);
    }
  }
}
