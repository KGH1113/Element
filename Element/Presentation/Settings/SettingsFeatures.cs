using System;
using Element.Application.Profiles;
using Element.Application.Updates;
using Element.Configuration.Gameplay;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>
/// The parts of the settings window that need more than the saved settings.
/// Each one left null leaves its part out or shows it as unavailable.
/// </summary>
internal sealed class SettingsFeatures
{
  /// <summary>Draws planet <c>index</c> as the game would, for the Planets page; null says the preview is unavailable.</summary>
  public Func<int, PlanetAppearanceProfile, Texture> RenderPlanetPreview;

  /// <summary>The presses the chatter blocker dropped since the game started; null shows 0.</summary>
  public Func<long> BlockedChatter;

  /// <summary>Automatic updates, at the end of General; null leaves the row out.</summary>
  public IModUpdates Updates;

  /// <summary>True while Competitive Mode ties ADOFAI's Hide Judgment Text to the judgment page; null reads as off.</summary>
  public Func<bool> JudgementTextLinked;

  /// <summary>Profiles, linked at the top of General; null leaves them out.</summary>
  public IProfiles Profiles;

  /// <summary>The overlay fonts, to offer replacements after a profile import; null offers none.</summary>
  public Func<OverlayFonts> OverlayFonts;
}
