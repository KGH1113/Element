namespace Element.Domain.Judgement;

internal readonly struct ComboSnapshot
{
  public int Count { get; }

  public ComboSnapshot(int count)
  {
    Count = count;
  }
}
