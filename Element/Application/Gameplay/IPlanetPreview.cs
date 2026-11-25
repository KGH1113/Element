using System;
using Element.Configuration.Gameplay;
using UnityEngine;

namespace Element.Application.Gameplay;

/// <summary>Draws a planet as the game would, for the settings window's Planets page.</summary>
internal interface IPlanetPreview : IDisposable
{
  /// <summary>Planet <paramref name="planetIndex"/> in <paramref name="profile"/>'s colors.</summary>
  Texture Render(int planetIndex, PlanetAppearanceProfile profile);
}
