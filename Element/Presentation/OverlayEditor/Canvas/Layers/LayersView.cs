using System;
using System.Collections.Generic;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// The layers panel: what it is and how to read it on top, the cards
/// front-most first, and the game itself as the last card, behind everything.
/// </summary>
internal sealed class LayersView : View<LayersViewModel>
{
  /// <summary>How tall the cards may stand before they scroll.</summary>
  public const float ListHeight = 440;

  private const float Gap = 6,
    HeadHeight = 36;

  private static readonly UiColor GameFill = new Color(.06f, .075f, .11f);

  private readonly UiColor _accent = UiPalette.Accent;
  private readonly UiColor _lit = UiPalette.SoftAccent;
  private readonly UiColor _litFill = UiPalette.Wash(UiPalette.AccentSurface, .06f);
  private readonly UiColor _buttonFill = UiPalette.Wash(UiPalette.EditorDim, .1f);
  private readonly UiColor _clear = new Color(0, 0, 0, 0);
  private readonly Func<string, RectTransform, IDisposable> _track;
  private readonly Func<IReadOnlyList<string>, RectTransform, IDisposable> _thumbnail;
  private readonly IDropDestination<LayerDrag> _drop;

  public LayersView(
    LayersViewModel vm,
    Func<string, RectTransform, IDisposable> track,
    Func<IReadOnlyList<string>, RectTransform, IDisposable> thumbnail,
    IDropDestination<LayerDrag> drop
  )
    : base(vm)
  {
    _track = track;
    _thumbnail = thumbnail;
    _drop = drop;
  }

  public override View Body =>
    VStack(
        spacing: Gap,
        Head(),
        ScrollView(
            VStack(
              spacing: Gap,
              ForEach(() => VM.Rows, row => row.Key, slot => new LayerRowView(slot, VM, _track, _thumbnail))
            )
          )
          .Frame(maxHeight: ListHeight),
        Game()
      )
      .Padding(12)
      .DropDestination(_drop);

  // Over the list: the word for its top end, what is in front, and Float view, which shows the same order in depth.
  private View Head() =>
    HStack(
        spacing: 8,
        Image(ElementIcons.Of(UiIconKind.Up), 16).Foreground(Colors.Muted),
        Text(LayersViewModel.Front).Font(Font.Callout.Sized(15)).Foreground(Colors.Muted).LineLimit(1).Named("Front"),
        Spacer(),
        FloatButton()
      )
      .Padding(new Insets(0, 6, 0, 0))
      .Frame(maxWidth: float.PositiveInfinity, height: HeadHeight);

  // Lit while it is on; the same press turns it off.
  private View FloatButton() =>
    Button(
        HStack(
            spacing: 8,
            Image(ElementIcons.Of(UiIconKind.Float), 18).Foreground(() => VM.Floating.Value ? _lit : Colors.Foreground),
            Text(L("띄워 보기", "Float view"))
              .Font(Font.Callout.Sized(15))
              .Foreground(() => VM.Floating.Value ? _lit : Colors.Foreground)
              .LineLimit(1)
          )
          .Padding(new Insets(0, 14, 0, 16))
          .Frame(height: HeadHeight),
        () => VM.ToggleFloatView,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.Floating.Value ? _litFill : _buttonFill, HeadHeight / 2))
      .Overlay(Capsule().Stroke(() => VM.Floating.Value ? _lit : _clear, 2).AllowsHitTesting(false))
      .Named("Float view")
      .Frame(height: HeadHeight);

  // The game is the last card: everything else is drawn over it.
  private View Game() =>
    HStack(
        spacing: 8,
        Spacer()
          .Frame(width: LayerRowView.ThumbWidth, height: LayerRowView.ThumbHeight)
          .Background(GameFill, 7, UiPalette.Wash(UiPalette.EditorDim, .14f)),
        Text(L("게임 화면", "The game")).Font(Font.Body.Sized(16)).Foreground(Colors.Muted).LineLimit(1),
        Spacer(),
        Text(LayersViewModel.Back).Font(Font.Callout.Sized(14)).Foreground(Colors.Muted).Named("Back")
      )
      .Padding(new Insets(0, 8, 0, 12))
      .Frame(height: LayerRowView.Height)
      .Background(UiPalette.Wash(UiPalette.EditorDim, .045f), 10)
      .Overlay(
        Spacer()
          .Frame(maxWidth: float.PositiveInfinity, height: 2)
          .Background(_accent, 1)
          .Padding(new Insets(0, 2, 0, 2))
          .Hidden(() => !VM.InsertAtEnd),
        Alignment.Top
      );
}
