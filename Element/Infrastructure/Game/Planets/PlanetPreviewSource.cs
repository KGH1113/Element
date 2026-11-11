using UnityEngine;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// What a planet preview copies: the game's planet components and the colors they
/// show without Element's planet appearance.
/// </summary>
internal struct PlanetPreviewSource
{
  internal PlanetRenderer Renderer;
  internal SpriteRenderer Body,
    Glow;
  internal LineRenderer Ring;
  internal ParticleSystem Tail,
    Core;
  internal Texture BodyTexture,
    AlphaMask;
  internal Shader Shader;
  internal Color BodyColor,
    GlowColor,
    RingColor,
    TailColor,
    CoreColor;
}
