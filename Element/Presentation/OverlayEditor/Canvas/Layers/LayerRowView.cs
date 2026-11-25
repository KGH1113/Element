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
  public const float Height = 32;
  public const float ThumbWidth = 52;
  public const float ThumbHeight = 24;
  private const float Indent = 22;

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
            spacing: 8,
            Rail(),
            Arrow(),
            new LayerPictureView(rect => _thumbnail(VM.Ids, rect), ThumbWidth, ThumbHeight),
            NameOrField(),
            Count()
          )
          .Padding(new Insets(0, VM.Child ? 8 : 6, 0, 8))
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
        8
      )
      .Overlay(InsertLine(), Alignment.Top)
      .Frame(height: Height);

  // An element inside a group sits in from the group's stripe.
  private View Rail() =>
    VM.Child
      ? Spacer()
        .Frame(width: 3, height: Height - 8)
        .Background(() => VM.Rail, 1.5f)
        .Padding(new Insets(0, Indent - 14, 0, 4))
    : VM.Header ? Spacer().Frame(width: 3, height: Height - 10).Background(() => VM.Rail, 1.5f)
    : Spacer().Frame(width: 0, height: 0);

  private View Arrow() =>
    VM.Header
      ? Button(
          If(
            () => VM.Open,
            () => Image(ElementIcons.Of(UiIconKind.Down), 16).Foreground(Colors.Muted),
            () => Image(ElementIcons.Of(UiIconKind.Forward), 16).Foreground(Colors.Muted)
          ),
          () => _list.Toggle(VM),
          ButtonStyle.Plain
        )
        .Named("Open group")
        .Frame(width: 20, height: 20)
      : Spacer().Frame(width: 0, height: 0);

  private View NameOrField() =>
    If(
      () => VM.Header && _list.RenamingGroup == VM.Group,
      () =>
        TextField(Bind(() => _list.RenameText))
          .CharacterLimit(40)
          .OnEditEnded((_, cancelled) => _list.EndRename(cancelled))
          .FocusOn(() => _list.RenamingGroup == VM.Group ? 1 : 0)
          .Frame(maxWidth: float.PositiveInfinity, height: 24),
      () =>
        Text(() => VM.Name)
          .Font(Font.Callout.Sized(13))
          .Foreground(Colors.Foreground)
          .LineLimit(1)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
    );

  private View Count() =>
    VM.Header
      ? Text(() => VM.Count)
        .Font(Font.Caption.Sized(11))
        .Foreground(new Color(.06f, .06f, .06f))
        .Padding(new Insets(0, 6, 0, 6))
        .Frame(height: 16)
        .Background(() => VM.Rail, 5)
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
      .Font(Font.Callout.Sized(13))
      .Foreground(Colors.Foreground)
      .LineLimit(1)
      .Padding(new Insets(0, 14, 0, 14))
      .Frame(height: 30)
      .Background(UiPalette.Wash(UiPalette.EditorDim, .16f), 8, UiPalette.Accent);
}
