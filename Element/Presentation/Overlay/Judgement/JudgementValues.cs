using System.Text;
using Element.Application;
using Element.Domain.Elements;
using Element.Domain.Judgement;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.Judgement;

// The colored judgment row and each count on its own.
internal sealed class JudgementValues : IOverlayView<JudgementSnapshot>
{
  private static readonly OverlayValueId[] Counts =
  {
    OverlayValueId.Overload,
    OverlayValueId.TooEarly,
    OverlayValueId.VeryEarly,
    OverlayValueId.EarlyPerfect,
    OverlayValueId.PerfectEarly,
    OverlayValueId.PurePerfect,
    OverlayValueId.PerfectLate,
    OverlayValueId.LatePerfect,
    OverlayValueId.VeryLate,
    OverlayValueId.TooLate,
    OverlayValueId.Miss,
  };

  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(256);

  public JudgementValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    _values.Clear(OverlayValueId.Judgements);
    foreach (OverlayValueId id in Counts)
      _values.Clear(id);
  }

  // Runs on every hit: numbers and colors are appended without their own strings.
  public void Render(JudgementSnapshot snapshot)
  {
    JudgementColors.AppendRow(
      _builder.Clear(),
      snapshot.FailOverload,
      snapshot.TooEarly,
      snapshot.VeryEarly,
      snapshot.EarlyPerfect,
      snapshot.PerfectMinus,
      snapshot.XPerfectAndAuto,
      snapshot.PerfectPlus,
      snapshot.LatePerfect,
      snapshot.VeryLate,
      snapshot.TooLate,
      snapshot.FailMiss
    );
    _values.Set(OverlayValueId.Judgements, _builder);
    Set(OverlayValueId.Overload, snapshot.FailOverload);
    Set(OverlayValueId.TooEarly, snapshot.TooEarly);
    Set(OverlayValueId.VeryEarly, snapshot.VeryEarly);
    Set(OverlayValueId.EarlyPerfect, snapshot.EarlyPerfect);
    Set(OverlayValueId.PerfectEarly, snapshot.PerfectMinus);
    Set(OverlayValueId.PurePerfect, snapshot.XPerfectAndAuto);
    Set(OverlayValueId.PerfectLate, snapshot.PerfectPlus);
    Set(OverlayValueId.LatePerfect, snapshot.LatePerfect);
    Set(OverlayValueId.VeryLate, snapshot.VeryLate);
    Set(OverlayValueId.TooLate, snapshot.TooLate);
    Set(OverlayValueId.Miss, snapshot.FailMiss);
  }

  public void Dispose() { }

  private void Set(OverlayValueId id, int count) => _values.Set(id, _builder.Clear().AppendInt(count));
}
