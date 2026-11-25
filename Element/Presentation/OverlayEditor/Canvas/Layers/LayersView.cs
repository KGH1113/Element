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
  public const float ListHeight = 360;

  private static readonly UiColor GameFill = new Color(.06f, .075f, .11f);

  private readonly UiColor _accent = UiPalette.Accent;
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
        spacing: 6,
        // How to read the stack, in one line: the button below already says what this is.
        HStack(
            spacing: 8,
            Image(ElementIcons.Of(UiIconKind.Up), 16).Foreground(Colors.Muted),
            Text(LayersViewModel.Caption).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1)
          )
          .Padding(new Insets(2, 6, 2, 6))
          .Frame(maxWidth: float.PositiveInfinity, height: 24, alignment: Alignment.Leading),
        ScrollView(
            VStack(
              spacing: 3,
              ForEach(() => VM.Rows, row => row.Key, slot => new LayerRowView(slot, VM, _track, _thumbnail))
            )
          )
          .Frame(maxHeight: ListHeight),
        Game()
      )
      .Padding(8)
      .DropDestination(_drop);

  // The game is the last card: everything else is drawn over it.
  private View Game() =>
    HStack(
        spacing: 8,
        Spacer()
          .Frame(width: LayerRowView.ThumbWidth, height: LayerRowView.ThumbHeight)
          .Background(GameFill, 5, UiPalette.Wash(UiPalette.EditorDim, .14f)),
        Text(L("게임 화면", "The game")).Font(Font.Callout.Sized(13)).Foreground(Colors.Muted).LineLimit(1),
        Spacer(),
        Text(L("맨 뒤", "Back")).Font(Font.Caption.Sized(11)).Foreground(Colors.Muted)
      )
      .Padding(new Insets(0, 6, 0, 8))
      .Frame(height: LayerRowView.Height)
      .Background(UiPalette.Wash(UiPalette.EditorDim, .045f), 8)
      .Overlay(
        Spacer()
          .Frame(maxWidth: float.PositiveInfinity, height: 2)
          .Background(_accent, 1)
          .Padding(new Insets(0, 2, 0, 2))
          .Hidden(() => !VM.InsertAtEnd),
        Alignment.Top
      );
}
