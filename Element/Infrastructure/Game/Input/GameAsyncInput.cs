namespace Element.Infrastructure.Game.Input;

/// <summary>The keys ADOFAI's async input has queued for the game.</summary>
internal static class GameAsyncInput
{
  public static void Clear()
  {
    // This runs every frame while the settings own the keyboard. Clearing an empty
    // ConcurrentQueue still allocates a segment and takes the lock the hook thread
    // enqueues under, so skip the game's clear when there is nothing to drop.
    if (
      !AsyncInputManager.keyQueue.IsEmpty
      || AsyncInputManager.keyMask.Count != 0
      || AsyncInputManager.keyDownMask.Count != 0
      || AsyncInputManager.keyUpMask.Count != 0
    )
      AsyncInputManager.ClearKeys();
    AsyncInputManager.frameDependentKeyMask.Clear();
    AsyncInputManager.frameDependentKeyDownMask.Clear();
    AsyncInputManager.frameDependentKeyUpMask.Clear();
  }
}
