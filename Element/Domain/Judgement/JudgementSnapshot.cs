namespace Element.Domain.Judgement;

internal readonly struct JudgementSnapshot
{
  public int FailOverload { get; }
  public int TooEarly { get; }
  public int VeryEarly { get; }
  public int EarlyPerfect { get; }
  public int PerfectMinus { get; }
  public int XPerfectAndAuto { get; }
  public int PerfectPlus { get; }
  public int LatePerfect { get; }
  public int VeryLate { get; }
  public int TooLate { get; }
  public int FailMiss { get; }

  public JudgementSnapshot(
    int failOverload,
    int tooEarly,
    int veryEarly,
    int earlyPerfect,
    int perfectMinus,
    int xPerfectAndAuto,
    int perfectPlus,
    int latePerfect,
    int veryLate,
    int tooLate,
    int failMiss
  )
  {
    FailOverload = failOverload;
    TooEarly = tooEarly;
    VeryEarly = veryEarly;
    EarlyPerfect = earlyPerfect;
    PerfectMinus = perfectMinus;
    XPerfectAndAuto = xPerfectAndAuto;
    PerfectPlus = perfectPlus;
    LatePerfect = latePerfect;
    VeryLate = veryLate;
    TooLate = tooLate;
    FailMiss = failMiss;
  }
}
