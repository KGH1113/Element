using UnityEngine;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// One player planet's colors as the game set them, before planet appearance: what
/// the overrides start from, what restoring puts back and what the settings preview
/// copies.
/// </summary>
internal sealed class PlanetOriginalAppearance
{
  internal readonly PlanetRenderer Renderer;
  internal int Depth;
  internal Color BodySource,
    TailSource,
    CoreSource,
    RingSource,
    FaceSource,
    Glow;
  internal bool CoreChanged,
    TailChanged;
  private Texture _texture;
  internal Texture SourceTexture => _texture;
  private PlanetSprite _sprite;
  private PlanetParticleColors _core,
    _tail,
    _explosion;

  internal PlanetOriginalAppearance(PlanetRenderer renderer)
  {
    Renderer = renderer;
    Capture();
  }

  internal void Capture()
  {
    _sprite = Renderer.sprite;
    _texture = _sprite != null ? _sprite.sprite : null;
    BodySource = _sprite != null ? _sprite.color : Color.white;
    TailSource = Renderer.tailParticles != null ? Renderer.GetTailColor() : BodySource;
    if (Renderer.tailParticles != null)
    {
      var lifetime = Renderer.tailParticles.colorOverLifetime.color;
      Gradient gradient =
        lifetime.mode == ParticleSystemGradientMode.TwoGradients ? lifetime.gradientMin : lifetime.gradient;
      if (gradient != null)
      {
        float alpha = TailSource.a;
        TailSource = gradient.Evaluate(0);
        TailSource.a = alpha;
      }
    }
    CoreSource = Renderer.coreParticles != null ? Renderer.coreParticles.main.startColor.color : BodySource;
    RingSource = Renderer.ringComp != null ? Renderer.ringComp.color : BodySource;
    RingSource.a /= 0.4f;
    FaceSource = Renderer.faceSprite != null ? Renderer.faceSprite.color : BodySource;
    Glow = Renderer.glow != null ? Renderer.glow.color : Color.white;
    _core = new(Renderer.coreParticles);
    _tail = new(Renderer.tailParticles);
    _explosion = new(Renderer.deathExplosion);
    CoreChanged = TailChanged = false;
  }

  internal void Restore()
  {
    if (_sprite != null)
    {
      _sprite.sprite = _texture;
      _sprite.color = BodySource;
    }
    if (Renderer.ringComp != null)
    {
      Color ring = RingSource;
      ring.a *= 0.4f;
      Renderer.ringComp.color = ring;
    }
    if (Renderer.faceSprite != null)
      Renderer.faceSprite.color = FaceSource;
    if (Renderer.glow != null)
      Renderer.glow.color = Glow;
    _core.Restore();
    _tail.Restore();
    _explosion.Restore();
  }
}
