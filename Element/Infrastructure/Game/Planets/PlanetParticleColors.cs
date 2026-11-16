using UnityEngine;

namespace Element.Infrastructure.Game.Planets;

/// <summary>A planet particle system's start and lifetime colors, to put back as they were.</summary>
internal readonly struct PlanetParticleColors
{
  private readonly ParticleSystem _system;
  private readonly ParticleSystem.MinMaxGradient _start,
    _lifetime;

  internal PlanetParticleColors(ParticleSystem system)
  {
    _system = system;
    _start = system != null ? system.main.startColor : default;
    _lifetime = system != null ? system.colorOverLifetime.color : default;
  }

  internal void Restore()
  {
    if (_system == null)
      return;
    var main = _system.main;
    main.startColor = _start;
    var lifetime = _system.colorOverLifetime;
    lifetime.color = _lifetime;
  }
}
