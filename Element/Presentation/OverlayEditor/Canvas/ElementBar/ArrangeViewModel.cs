using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Kinds;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Canvas.ElementBar;

/// <summary>
/// With several elements selected, the element toolbar lines them up with
/// each other or spaces them evenly. Each press is one undo step; a press
/// that would move nothing says so instead.
/// </summary>
internal sealed class ArrangeViewModel : ElementBarViewModel
{
  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly CanvasStatusLine _status;
  private readonly ICanvasEditing _editing;
  private readonly List<IOverlayLayoutTarget> _targets = new();
  private readonly List<LayoutBounds> _bounds = new();
  private readonly List<LayoutBounds> _before = new();
  private readonly List<Vector2> _offsets = new();

  private ArrangeViewModel(
    CanvasSession session,
    CanvasSelection selection,
    CanvasStatusLine status,
    ICanvasEditing editing
  )
  {
    _session = session;
    _selection = selection;
    _status = status;
    _editing = editing;
    AlignLeft = Align(LayoutAlignment.Left);
    AlignCenter = Align(LayoutAlignment.Center);
    AlignRight = Align(LayoutAlignment.Right);
    AlignTop = Align(LayoutAlignment.Top);
    AlignMiddle = Align(LayoutAlignment.Middle);
    AlignBottom = Align(LayoutAlignment.Bottom);
    // Spacing needs three; the buttons stay put and only grey out.
    Computed<bool> three = Computed(() => Count.Value >= 3);
    DistributeHorizontal = Command(
      () =>
        Arrange(
          boxes => LayoutArrange.Distribute(boxes, true),
          L(
            "가로 간격이 이미 같아요. 세로로 맞추려면 옆 버튼을 누르세요",
            "Already evenly spaced across. To space them down, use the next button"
          )
        ),
      canExecute: three
    );
    DistributeVertical = Command(
      () =>
        Arrange(
          boxes => LayoutArrange.Distribute(boxes, false),
          L(
            "세로 간격이 이미 같아요. 가로로 맞추려면 옆 버튼을 누르세요",
            "Already evenly spaced down. To space them across, use the previous button"
          )
        ),
      canExecute: three
    );
  }

  /// <summary>The bar for the selection: this model and its view.</summary>
  public static ElementBarContent Screen(
    CanvasSession session,
    CanvasSelection selection,
    CanvasStatusLine status,
    ICanvasEditing editing
  )
  {
    ArrangeViewModel model = new(session, selection, status, editing);
    return new ElementBarContent(model, new ArrangeView(model));
  }

  /// <summary>How many are selected.</summary>
  public readonly State<int> Count = new(0);

  public Command AlignLeft { get; }
  public Command AlignCenter { get; }
  public Command AlignRight { get; }
  public Command AlignTop { get; }
  public Command AlignMiddle { get; }
  public Command AlignBottom { get; }
  public Command DistributeHorizontal { get; }
  public Command DistributeVertical { get; }

  public override void Sync() => Count.Value = _selection.Count;

  private Command Align(LayoutAlignment alignment) =>
    Command(() =>
      Arrange(boxes => LayoutArrange.Align(boxes, alignment), L("이미 맞춰져 있어요", "They're already lined up"))
    );

  // Lines the selection up in one space: native viewers measure from their
  // own window origin, read from where the pointer is in both spaces.
  // When nothing would move, says so instead of looking like nothing happened.
  private void Arrange(Action<List<LayoutBounds>> arrange, string unchanged)
  {
    List<IOverlayLayoutTarget> selection = _selection.Targets;
    if (selection.Count < 2)
      return;
    bool native = false;
    foreach (IOverlayLayoutTarget target in selection)
      native |= target is IOverlayLayoutPointer;
    Vector2 mouse = native ? CanvasSession.Mouse : Vector2.zero;
    _targets.Clear();
    _bounds.Clear();
    _offsets.Clear();
    foreach (IOverlayLayoutTarget target in selection)
    {
      Vector2 offset = CanvasSession.PointerFor(target, mouse) - mouse;
      Rect bounds = target.Bounds;
      _targets.Add(target);
      _offsets.Add(offset);
      _bounds.Add(new LayoutBounds(bounds.x - offset.x, bounds.y - offset.y, bounds.width, bounds.height));
    }
    _before.Clear();
    _before.AddRange(_bounds);
    arrange(_bounds);
    bool moves = false;
    for (int i = 0; i < _bounds.Count; i++)
      moves |= Mathf.Abs(_bounds[i].X - _before[i].X) >= .5f || Mathf.Abs(_bounds[i].Y - _before[i].Y) >= .5f;
    if (!moves)
    {
      _status.ShowNote(unchanged);
      return;
    }
    for (int i = 0; i < _targets.Count; i++)
    {
      LayoutBounds box = _bounds[i];
      Vector2 offset = _offsets[i];
      _targets[i].SetBounds(new Rect(box.X + offset.x, box.Y + offset.y, box.Width, box.Height));
      _targets[i].CommitLayout(true);
    }
    _session.Commit();
    _editing.RefreshLayouts();
    _editing.UpdateFrames();
  }
}
