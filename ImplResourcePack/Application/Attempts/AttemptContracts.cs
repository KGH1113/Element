using ImplResourcePack.Domain.Attempts;

namespace ImplResourcePack.Application.Attempts;

/// <summary>Reads the play the game just started.</summary>
internal interface IAttemptRunSource
{
  bool TryRead(int startFloor, out AttemptRun run);
}

/// <summary>Keeps attempt counts across game launches.</summary>
internal interface IAttemptStore
{
  AttemptBook Load();

  /// <summary>Saves the book as it is now; the write may finish later.</summary>
  void Save(AttemptBook book);

  /// <summary>Waits for saves that are still being written.</summary>
  void Flush();
}
