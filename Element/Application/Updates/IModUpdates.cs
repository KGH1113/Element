namespace Element.Application.Updates;

/// <summary>Automatic updates as Settings shows and changes them.</summary>
internal interface IModUpdates
{
  ModUpdateStatus Status { get; }

  /// <summary>Takes effect the next time the game starts.</summary>
  bool AutomaticUpdates { get; set; }
}
