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
    ZStack(Box(), Grid(3, Gap, Points()).Padding((Size - Cell * 3 - Gap * 2) * .5f))
      .Frame(width: Size, height: Size)
      .Background(UiPalette.Bar, Size * .5f);

  // The box the points belong to, its edges through their middles.
  private static View Box()
  {
    const float span = Size - Cell - Gap * 2;
    return VStack(
      spacing: 0,
      Edge(span, 1),
      HStack(spacing: 0, Edge(1, span - 2), Spacer().Frame(width: span - 2, height: span - 2), Edge(1, span - 2)),
      Edge(span, 1)
    );
  }

  private static View Edge(float width, float height) =>
    Spacer().Frame(width: width, height: height).Background(new Color(1, 1, 1, .16f));

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
            new OverlayGrowArrowView(() => VM.Direction.Value).Hidden(() => VM.Look.Value != GrowLook.Arrow)
          )
          .Frame(width: Cell, height: Cell),
        () => VM.Pick,
        ButtonStyle.Plain
      )
      .Named(VM.Name);

  private View Dot(float size) =>
    Spacer().Frame(width: size, height: size).Background(() => VM.DotColor.Value, size * .5f);
}
