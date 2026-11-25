using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>One point: a dot, larger and lit when chosen, or an arrow away from the chosen one.</summary>
internal sealed class GrowPointView : View<GrowPointViewModel>
{
  private const float Cell = 24;

  public GrowPointView(GrowPointViewModel vm)
    : base(vm) { }

  public override View Body =>
    Button(
        ZStack(
            Dot(5).Hidden(() => VM.Look.Value != GrowLook.Dot),
            Dot(12).Hidden(() => VM.Look.Value != GrowLook.Chosen),
            new GrowArrowView()
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
