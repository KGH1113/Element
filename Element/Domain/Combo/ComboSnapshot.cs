namespace Element.Domain.Combo;

internal readonly struct ComboSnapshot
{
  public int Count { get; }
  public bool Bump { get; }

  public ComboSnapshot(int count, bool bump)
  {
    Count = count;
    Bump = bump;
  }
}
