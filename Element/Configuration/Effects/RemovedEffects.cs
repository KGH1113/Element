using System;

namespace Element.Configuration.Effects;

[Flags]
public enum RemovedEffects : long
{
  None = 0,
  CameraMotion = 1L << 0,
  ScreenFlash = 1L << 1,
  ScreenShake = 1L << 2,
  Filters = 1L << 3,
  AdvancedFilters = 1L << 4,
  Backgrounds = 1L << 5,
  Decorations = 1L << 6,
  Particles = 1L << 7,
  TrackAnimations = 1L << 8,
  TrackMovement = 1L << 9,
  TrackPosition = 1L << 10,
  TrackColors = 1L << 11,
  TrackOpacity = 1L << 12,
  PlanetOrbit = 1L << 13,
  PlanetScale = 1L << 14,
  PlanetRadius = 1L << 15,
  HitSounds = 1L << 16,
  HoldSounds = 1L << 17,
  HideIcons = 1L << 18,
  RepeatEvents = 1L << 19,
  FrameRate = 1L << 20,
  Checkpoints = 1L << 21,
}
