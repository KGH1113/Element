using System.Collections.Generic;
using Element.Configuration.Overlay;

namespace Element.Domain.Overlay.Text;

/// <summary>
/// An If in a text being edited: a stretch of the text that has one look per
/// case and one for otherwise. The look being shown lives in the document,
/// from <see cref="Start"/> to <see cref="End"/>, where it is edited like any
/// text; the others wait here. Looks are numbered as the cases are, with
/// otherwise last. Only <see cref="OverlayTextDocument"/> changes one.
/// </summary>
internal sealed class OverlayTextBlock
{
  internal readonly List<OverlayCondition> Conditions = new();

  // One per case, then otherwise. The shown look's list is left empty: its
  // items are the document's.
  internal readonly List<List<OverlayTextItem>> Looks = new() { new List<OverlayTextItem>() };

  /// <summary>Where the shown look starts in the document.</summary>
  public int Start { get; internal set; }

  /// <summary>Where the shown look ends; the same as <see cref="Start"/> for a look that shows nothing.</summary>
  public int End { get; internal set; }

  /// <summary>The look in the document now: a case's number, or <see cref="Cases"/> for otherwise.</summary>
  public int Shown { get; internal set; }

  public int Cases => Conditions.Count;

  /// <summary>The number of the otherwise look, after every case's.</summary>
  public int Otherwise => Conditions.Count;

  /// <summary>When case <paramref name="index"/> shows; not to be changed in place.</summary>
  public OverlayCondition When(int index) => Conditions[index];

  internal OverlayTextBlock Clone()
  {
    OverlayTextBlock copy = new()
    {
      Start = Start,
      End = End,
      Shown = Shown,
    };
    foreach (OverlayCondition condition in Conditions)
      copy.Conditions.Add(condition.Clone());
    copy.Looks.Clear();
    foreach (List<OverlayTextItem> look in Looks)
      copy.Looks.Add(new List<OverlayTextItem>(look));
    return copy;
  }
}
