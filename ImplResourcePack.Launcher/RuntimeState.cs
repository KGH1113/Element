using System.Collections.Generic;

namespace ImplResourcePack.Launcher;

/// <summary>Runtime/state.json.</summary>
internal sealed class RuntimeState
{
  public const int CurrentSchema = 1;
  /// <summary>Failed versions are remembered this many at a time.</summary>
  public const int FailedLimit = 10;

  public int SchemaVersion { get; set; } = CurrentSchema;

  /// <summary>The version that loads when no update applies.</summary>
  public string Current { get; set; }

  /// <summary>The version before <see cref="Current"/>; the fallback if Current breaks.</summary>
  public string Previous { get; set; }

  /// <summary>Set while a new version loads for the first time. Left behind, it means that load crashed.</summary>
  public string Trial { get; set; }

  /// <summary>Versions that failed to start; the updater skips them.</summary>
  public List<string> Failed { get; set; } = new();
}
