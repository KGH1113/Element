using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Canvas;
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
  private readonly ICanvasActions _canvas;
  private readonly List<IOverlayLayoutTarget> _targets = new();
  private readonly List<LayoutBounds> _bounds = new();
  private readonly List<LayoutBounds> _before = new();
  private readonly List<List<IOverlayLayoutTarget>> _units = new();
  private readonly Dictionary<string, int> _unitOfGroup = new();

  private ArrangeViewModel(
    CanvasSession session,
    CanvasSelection selection,
    CanvasStatusLine status,
    ICanvasActions canvas
  )
  {
    _session = session;
    _selection = selection;
    _status = status;
    _canvas = canvas;
    AlignLeft = Align(LayoutAlignment.Left);
    AlignCenter = Align(LayoutAlignment.Center);
    AlignRight = Align(LayoutAlignment.Right);
    AlignTop = Align(LayoutAlignment.Top);
    AlignMiddle = Align(LayoutAlignment.Middle);
    AlignBottom = Align(LayoutAlignment.Bottom);
    Group = Command(() => _canvas.Group());
    Ungroup = Command(() => _canvas.Ungroup());
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
    ICanvasActions canvas
  )
  {
    ArrangeViewModel model = new(session, selection, status, canvas);
    return new ElementBarContent(model, new ArrangeView(model));
  }

  /// <summary>How many are selected.</summary>
  public readonly State<int> Count = new(0);

  /// <summary>The group's name when the selection is one whole group; empty otherwise.</summary>
  public readonly State<string> GroupName = new("");

  /// <summary>Makes the selection one group.</summary>
  public Command Group { get; private set; }

  /// <summary>Takes the selected group apart.</summary>
  public Command Ungroup { get; private set; }

  public Command AlignLeft { get; }
  public Command AlignCenter { get; }
  public Command AlignRight { get; }
  public Command AlignTop { get; }
  public Command AlignMiddle { get; }
  public Command AlignBottom { get; }
  public Command DistributeHorizontal { get; }
  public Command DistributeVertical { get; }

  public override void Sync()
  {
    Count.Value = _selection.Count;
    string group = _selection.Group;
    GroupName.Value = group == null ? "" : GroupNames.Of(_session.Host.LayoutSettings, group);
  }

  // What lines up: each group of the selection as one, other elements alone.
  // When the selection is one whole group, its elements each stand alone.
  private void Units(List<IOverlayLayoutTarget> selection)
  {
    _units.Clear();
    _unitOfGroup.Clear();
    bool oneGroup = _selection.Group != null;
    foreach (IOverlayLayoutTarget target in selection)
    {
      string group = oneGroup ? null : _session.GroupOf(target);
      if (group != null && _unitOfGroup.TryGetValue(group, out int unit))
      {
        _units[unit].Add(target);
        continue;
      }
      if (group != null)
        _unitOfGroup[group] = _units.Count;
      _units.Add(new List<IOverlayLayoutTarget> { target });
    }
  }

  private Command Align(LayoutAlignment alignment) =>
    Command(() =>
      Arrange(boxes => LayoutArrange.Align(boxes, alignment), L("이미 맞춰져 있어요", "They're already lined up"))
    );

  // Lines the selection up in one space: native viewers measure from their
  // own window origin, read from where the pointer is in both spaces. A
  // group among several lines up as one box; a group alone lines up its own
  // elements. When nothing would move, says so instead of looking like nothing happened.
  private void Arrange(Action<List<LayoutBounds>> arrange, string unchanged)
  {
    List<IOverlayLayoutTarget> selection = _selection.Targets;
    if (selection.Count < 2)
      return;
    bool native = false;
    foreach (IOverlayLayoutTarget target in selection)
      native |= target is IOverlayLayoutPointer;
    Vector2 mouse = native ? CanvasSession.Mouse : Vector2.zero;
    Units(selection);
    _bounds.Clear();
    foreach (List<IOverlayLayoutTarget> unit in _units)
    {
      Rect union = default;
      for (int i = 0; i < unit.Count; i++)
      {
        Rect bounds = unit[i].Bounds;
        bounds.position -= CanvasSession.PointerFor(unit[i], mouse) - mouse;
        union =
          i == 0
            ? bounds
            : Rect.MinMaxRect(
              Mathf.Min(union.xMin, bounds.xMin),
              Mathf.Min(union.yMin, bounds.yMin),
              Mathf.Max(union.xMax, bounds.xMax),
              Mathf.Max(union.yMax, bounds.yMax)
            );
      }
      _bounds.Add(new LayoutBounds(union.x, union.y, union.width, union.height));
    }
    if (_bounds.Count < 2)
    {
      _status.ShowNote(
        L(
          "묶음 하나는 그대로 맞출 게 없어요. 그 안의 요소끼리 맞추려면 묶음을 한 번 더 누르세요",
          "One group has nothing to line up with. To line up what's inside, click into the group"
        )
      );
      return;
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
    _targets.Clear();
    for (int i = 0; i < _units.Count; i++)
    {
      float dx = _bounds[i].X - _before[i].X,
        dy = _bounds[i].Y - _before[i].Y;
      foreach (IOverlayLayoutTarget target in _units[i])
      {
        Rect bounds = target.Bounds;
        target.SetBounds(new Rect(bounds.x + dx, bounds.y + dy, bounds.width, bounds.height));
        _targets.Add(target);
      }
    }
    _session.CommitMoved(_targets);
    _session.Commit();
    _canvas.RefreshLayouts();
    _canvas.UpdateFrames();
  }
}
