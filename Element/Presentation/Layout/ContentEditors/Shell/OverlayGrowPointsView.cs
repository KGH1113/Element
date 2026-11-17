using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The grow picker's nine points on a round, dark surface, over the edges of
/// the box they belong to. Each point is a button.
/// </summary>
internal sealed class OverlayGrowPointsView : View<OverlayGrowPickerModel>
{
  public const float Size = 78;
  private const float Cell = 24,
    Gap = 2;

  public OverlayGrowPointsView(OverlayGrowPickerModel vm)
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
      points[i] = new OverlayGrowPointView(VM.Points[i]);
    return points;
  }
}

/// <summary>One point: a dot, larger and lit when chosen, or an arrow away from the chosen one.</summary>
internal sealed class OverlayGrowPointView : View<GrowPointModel>
{
  private const float Cell = 24;

  public OverlayGrowPointView(GrowPointModel vm)
    : base(vm) { }

  public override View Body =>
    Button(
        ZStack(
            Dot(5).Hidden(() => VM.Look.Value != GrowLook.Dot),
            Dot(12).Hidden(() => VM.Look.Value != GrowLook.Chosen),
            new OverlayGrowArrowView()
              .RotationEffect(() => Mathf.Atan2(VM.Direction.Value.y, VM.Direction.Value.x) * Mathf.Rad2Deg)
              .Hidden(() => VM.Look.Value != GrowLook.Arrow)
          )
          .Frame(width: Cell, height: Cell),
        () => VM.Pick,
        ButtonStyle.Plain
      )
      .Named(VM.Name);

  private View Dot(float size) => Circle().Fill(() => VM.DotColor.Value).Frame(width: size, height: size);
}
