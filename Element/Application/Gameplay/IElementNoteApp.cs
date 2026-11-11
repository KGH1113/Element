namespace Element.Application.Gameplay;

/// <summary>The ElementNote app bundled with the mod.</summary>
internal interface IElementNoteApp
{
  /// <summary>Installs or updates it unless it is running.</summary>
  void InstallIfClosed();

  /// <summary>Installs it like <see cref="InstallIfClosed"/>, then opens it.</summary>
  void OpenIfClosed();
}
