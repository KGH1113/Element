namespace Element.Presentation.Settings.Window;

/// <summary>
/// What the settings window asks of the screen that shows it, and the only
/// way out of the window: its pages change the settings, then the window asks
/// for them to be applied or saved, for the layout editor, or to close.
/// </summary>
internal interface ISettingsWindowActions
{
  /// <summary>A setting changed: the game takes it at once.</summary>
  void Apply();

  /// <summary>Writes the settings; false when they couldn't be written.</summary>
  bool Save();

  /// <summary>Hides the window and opens the layout editor.</summary>
  void EditLayout();

  void Close();
}
