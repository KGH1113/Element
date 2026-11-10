namespace Element.Domain.Combo;

internal readonly struct ComboSnapshot
{
  public int Count { get; }

  public ComboSnapshot(int count)
  {
    Count = count;
  }
}
