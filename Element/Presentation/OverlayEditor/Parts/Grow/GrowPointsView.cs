using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// The grow picker's nine points on a round, dark surface, over the edges of
/// the box they belong to. Each point is a button.
/// </summary>
internal sealed class GrowPointsView : View<GrowPickerViewModel>
{
  public const float Size = 78;
  private const float Cell = 24,
    Gap = 2;

  public GrowPointsView(GrowPickerViewModel vm)
    : base(vm) { }

  public override View Body =>
    Grid(3, Gap, Points())
      .Padding((Size - Cell * 3 - Gap * 2) * .5f)
      .Background(
        Rectangle().Stroke(new Color(1, 1, 1, .16f)).Frame(width: Size - Cell - Gap * 2, height: Size - Cell - Gap * 2)
      )
      .Frame(width: Size, height: Size)
      .Background(UiPalette.Bar, Shapes.Circle);

  private View[] Points()
  {
    View[] points = new View[VM.Points.Count];
    for (int i = 0; i < points.Length; i++)
      points[i] = new GrowPointView(VM.Points[i]);
    return points;
  }
}
