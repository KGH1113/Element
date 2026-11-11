using Element.Configuration;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Settings.Pages;

/// <summary>What a settings page can ask of the window it is shown in.</summary>
internal interface ISettingsWindow
{
  ElementSettings Settings { get; }

  /// <summary>Builds the pages' controls and keeps them in the current theme.</summary>
  UiFactory Ui { get; }
  TMP_FontAsset Font { get; }

  /// <summary>The window itself: popovers are placed in it, over the pages.</summary>
  RectTransform Frame { get; }

  /// <summary>Where the pages sit; the window scrolls the one shown.</summary>
  RectTransform Viewport { get; }

  /// <summary>The dimmed layer under an open popover.</summary>
  UiPopoverShade Shade { get; }

  /// <summary>The window is open on this page.</summary>
  bool IsShowing(SettingsPage page);

  /// <summary>Shows a page, as after saving on it.</summary>
  void ShowPage(SettingsPage page);

  /// <summary>Opens a page from a link on another; focus moves to Back.</summary>
  void OpenPage(SettingsPage page);

  /// <summary>Saves, hides the window and opens the overlay editor.</summary>
  void EditLayout();

  /// <summary>A setting changed: applies it and shows it everywhere.</summary>
  void Changed();

  /// <summary>Lays the window out again, as a page's height changed.</summary>
  void FitWindow();

  /// <summary>Saves the settings file; on failure the window says so and false returns.</summary>
  bool Save();
  void ShowSaveError();

  /// <summary>Keeps an open popover's edit; false while it can't be kept.</summary>
  bool CommitPopovers();
  void HidePopovers();

  /// <summary>Focuses a field now and places its caret again next frame.</summary>
  void Focus(TMP_InputField field);

  /// <summary>Another profile's values replaced the settings in place; every page shows them.</summary>
  void SettingsReplaced();
}
