using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>A kind with its elements in a list of their own type.</summary>
internal abstract class OverlayElementKind<T> : OverlayElementKind
  where T : OverlayElementTarget
{
  /// <summary>This kind's elements, oldest first.</summary>
  public List<T> Elements { get; } = new();

  public override OverlayElementTarget Create()
  {
    T element = Make();
    Elements.Add(element);
    return element;
  }

  protected abstract T Make();

  public override bool Forget(OverlayElementTarget element) => element is T typed && Elements.Remove(typed);

  public override void Tick(OverlayValueMask changes, bool settle)
  {
    for (int i = 0; i < Elements.Count; i++)
      Elements[i].Refresh(changes, settle);
  }

  public override void Clear() => Elements.Clear();
}
