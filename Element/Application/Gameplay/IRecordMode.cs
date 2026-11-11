namespace Element.Application.Gameplay;

/// <summary>Record mode: the game's own UI and the overlay hidden for recording.</summary>
internal interface IRecordMode
{
  bool IsActive { get; }

  void Apply(bool active);

  /// <summary>Hides again what the current scene rebuilt.</summary>
  void ReapplyCurrentScene();
}
