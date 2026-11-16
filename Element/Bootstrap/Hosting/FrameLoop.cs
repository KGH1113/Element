namespace Element.Bootstrap.Hosting;

/// <summary>
/// The mod's per-frame work, from UnityModManager's update in every scene, in a fixed
/// order. Each task has its own guard, so a failing one can't stop those after it.
/// Allocation free: the tasks and their delegates are made once.
/// </summary>
internal sealed class FrameLoop
{
  private readonly FailureGuard _guard;
  private readonly FrameTask[] _tasks;

  public FrameLoop(FailureGuard guard, params FrameTask[] tasks)
  {
    _guard = guard;
    _tasks = tasks;
  }

  public void Tick()
  {
    for (int i = 0; i < _tasks.Length; i++)
      _guard.Run(_tasks[i].Context, _tasks[i].Tick);
  }
}
