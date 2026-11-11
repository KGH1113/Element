using System;
using System.IO;

namespace Element.Infrastructure.ElementNote.App;

/// <summary>The ElementNote copy this mod ships for the current operating system.</summary>
internal interface IDmNoteApp
{
  /// <summary>False when this mod build carries no ElementNote for this OS.</summary>
  bool IsBundled { get; }

  bool IsRunning();

  /// <summary>Copies the bundled app out of the mod folder when it changed.</summary>
  void Install();

  /// <summary>Makes the installed app show up where the OS lists apps, so it opens without the game.</summary>
  void EnsureListed();

  /// <summary>Opens the installed app in the background, owned by the game.</summary>
  void Launch();
}
