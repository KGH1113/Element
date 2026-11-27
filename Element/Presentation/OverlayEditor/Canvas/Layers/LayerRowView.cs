using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// One card: the element as it looks, small, and its name; a group's card
/// with its arrow, its elements together and how many; an element inside an
/// open group set in beside the group's stripe. A line above it shows where
/// a carried card would land.
/// </summary>
internal sealed class LayerRowView : View<LayerRowViewModel>
{
  // As large as the editor's toolbar under it: a name in its size, and what it is under that.
  public const float Height = 58;
  public const float ThumbWidth = 84;
  public const float ThumbHeight = 42;
  private const float Indent = 24,
    Radius = 10;

  private static readonly UiColor CardFill = UiPalette.Wash(UiPalette.EditorDim, .1f);
  private static readonly UiColor HeaderFill = UiPalette.Wash(UiPalette.EditorDim, .075f);

  // The highlight color it is drawn in; the panel is drawn again when that changes.
  private readonly UiColor _accent = UiPalette.Accent;
  private readonly UiColor _picked = UiPalette.Wash(UiPalette.AccentSurface, .06f);
  private readonly LayersViewModel _list;
  private readonly Func<string, RectTransform, IDisposable> _track;
  private readonly Func<IReadOnlyList<string>, RectTransform, IDisposable> _thumbnail;

  /// <param name="track">Keeps the card's frame known while it shows, for carrying and pointing.</param>
  /// <param name="thumbnail">Draws the elements, small, into the card's picture.</param>
  public LayerRowView(
    ItemSlot<LayerRowViewModel> slot,
    LayersViewModel list,
    Func<string, RectTransform, IDisposable> track,
    Func<IReadOnlyList<string>, RectTransform, IDisposable> thumbnail
  )
    : base(slot)
  {
    _list = list;
    _track = track;
    _thumbnail = thumbnail;
  }

  public override View Body =>
    Button(
        HStack(
            spacing: 10,
            Rail(),
            Arrow(),
            new LayerPictureView(rect => _thumbnail(VM.Ids, rect), ThumbWidth, ThumbHeight),
            NameOrField(),
            Count()
          )
          .Padding(new Insets(0, 8, 0, 12))
          .Frame(maxWidth: float.PositiveInfinity, height: Height, alignment: Alignment.Leading),
        () => _list.Pick(VM, EditorKeys.ShiftHeld || EditorKeys.CommandHeld),
        ButtonStyle.Plain
      )
      .Configure<RectTransform>(rect => _track(VM.Key, rect))
      .Named(VM.Key)
      .Draggable(() => _list.DragOf(VM), Ghost)
      .Background(
        () =>
          VM.Selected ? _picked
          : VM.Header ? HeaderFill
          : CardFill,
        Radius
      )
      .Overlay(InsertLine(), Alignment.Top)
      .Frame(height: Height);

  // An element inside a group sits in from the group's stripe.
  private View Rail() =>
    VM.Child
      ? Spacer()
        .Frame(width: 3, height: Height - 16)
        .Background(() => VM.Rail, 1.5f)
        .Padding(new Insets(0, Indent - 14, 0, 4))
    : VM.Header ? Spacer().Frame(width: 3, height: Height - 18).Background(() => VM.Rail, 1.5f)
    : Spacer().Frame(width: 0, height: 0);

  private View Arrow() =>
    VM.Header
      ? Button(
          If(
            () => VM.Open,
            () => Image(ElementIcons.Of(UiIconKind.Down), 18).Foreground(Colors.Muted),
            () => Image(ElementIcons.Of(UiIconKind.Forward), 18).Foreground(Colors.Muted)
          ),
          () => _list.Toggle(VM),
          ButtonStyle.Plain
        )
        .Named("Open group")
        .Frame(width: 24, height: 24)
      : Spacer().Frame(width: 0, height: 0);

  private View NameOrField() =>
    If(
      () => VM.Header && _list.RenamingGroup == VM.Group,
      () =>
        TextField(Bind(() => _list.RenameText))
          .CharacterLimit(40)
          .OnEditEnded((_, cancelled) => _list.EndRename(cancelled))
          .FocusOn(() => _list.RenamingGroup == VM.Group ? 1 : 0)
          .Frame(maxWidth: float.PositiveInfinity, height: 34),
      () =>
        VStack(
            spacing: 2,
            HAlignment.Leading,
            Text(() => VM.Name).Font(Font.Body.Sized(16)).Foreground(Colors.Foreground).LineLimit(1).Named("Card name"),
            // What it is, quietly: the kind, a mod's name, or how many a group holds.
            Text(() => VM.What).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1).Named("Card kind")
          )
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
    );

  private View Count() =>
    VM.Header
      ? Text(() => VM.Count)
        .Font(Font.Caption.Sized(13))
        .Foreground(new Color(.06f, .06f, .06f))
        .Padding(new Insets(0, 7, 0, 7))
        .Frame(minWidth: 24, height: 22)
        .Background(() => VM.Rail, 7)
      : Spacer().Frame(width: 0, height: 0);

  // The line a carried card would land on; set in when it joins a group.
  private View InsertLine() =>
    Spacer()
      .Frame(maxWidth: float.PositiveInfinity, height: 2)
      .Background(() => VM.InsertInside ? VM.Rail : _accent, 1)
      .Padding(new Insets(0, VM.Child ? Indent : 2, 0, 2))
      .Hidden(() => !VM.InsertAbove);

  // The carried card beside the pointer: its name on an opaque pill.
  private static View Ghost(LayerDrag drag) =>
    Text(drag.Name)
      .Font(Font.Body.Sized(16))
      .Foreground(Colors.Foreground)
      .LineLimit(1)
      .Padding(new Insets(0, 16, 0, 16))
      .Frame(height: 40)
      .Background(UiPalette.Wash(UiPalette.EditorDim, .16f), Radius, UiPalette.Accent);
}
