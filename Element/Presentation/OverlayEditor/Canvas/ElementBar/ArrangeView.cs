using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.OverlayEditor.Canvas.CanvasParts;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Canvas.ElementBar;

/// <summary>The element toolbar for several: six ways to line them up, then two to space them evenly.</summary>
internal sealed class ArrangeView : View<ArrangeViewModel>
{
  public ArrangeView(ArrangeViewModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(
        spacing: 8,
        Grip("Drag element toolbar"),
        HStack(
          spacing: 4,
          IconButton(UiIconKind.AlignLeft, "Align left", L("왼쪽 맞춤", "Align left"), () => VM.AlignLeft, 36),
          IconButton(
            UiIconKind.AlignCenter,
            "Align center",
            L("가로 가운데 맞춤", "Align centers horizontally"),
            () => VM.AlignCenter,
            36
          ),
          IconButton(UiIconKind.AlignRight, "Align right", L("오른쪽 맞춤", "Align right"), () => VM.AlignRight, 36),
          IconButton(UiIconKind.AlignTop, "Align top", L("위쪽 맞춤", "Align top"), () => VM.AlignTop, 36),
          IconButton(
            UiIconKind.AlignMiddle,
            "Align middle",
            L("세로 가운데 맞춤", "Align centers vertically"),
            () => VM.AlignMiddle,
            36
          ),
          IconButton(UiIconKind.AlignBottom, "Align bottom", L("아래쪽 맞춤", "Align bottom"), () => VM.AlignBottom, 36)
        ),
        Separator(),
        HStack(
          spacing: 4,
          IconButton(
            UiIconKind.DistributeHorizontal,
            "Space across",
            L("가로 간격 똑같이 · 3개 이상", "Space evenly across · 3 or more"),
            () => VM.DistributeHorizontal,
            36
          ),
          IconButton(
            UiIconKind.DistributeVertical,
            "Space down",
            L("세로 간격 똑같이 · 3개 이상", "Space evenly down · 3 or more"),
            () => VM.DistributeVertical,
            36
          )
        )
      )
      .Padding(new Insets(10, 10, 10, 8));
}
