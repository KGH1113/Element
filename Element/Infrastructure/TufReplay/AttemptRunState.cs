namespace Element.Infrastructure.TufReplay;

/// <summary>
/// Saved by TUFReplay with each run. Older runs are read back by member name and type, so
/// renaming a member or changing its type needs an <c>UpgradeRunState</c> method on the provider.
/// </summary>
internal sealed class AttemptRunState
{
  public int Attempt;
  public int FullAttempt;
}
