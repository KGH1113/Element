using System;
using System.Collections.Generic;
using Element.Configuration;
using UnityEngine;

namespace Element.Infrastructure.Game.Tweaks;

/// <summary>Overrides only the three real player planets and retains their game-owned appearance.</summary>
internal sealed class PlanetAppearanceController : IDisposable
{
  private readonly PlanetAppearanceOptions _settings;
  private readonly Dictionary<PlanetRenderer, PlanetState> _states = new();
  private readonly List<PlanetRenderer> _removed = new();
  private readonly Profile[] _profiles = new Profile[3];
  private bool _applying;
  private bool _enabled;

  public PlanetAppearanceController(PlanetAppearanceOptions settings)
  {
    _settings = settings;
    ReadProfiles();
  }

  public void Apply()
  {
    bool changed = ReadProfiles();
    _removed.Clear();
    foreach (var pair in _states)
    {
      PlanetRenderer renderer = pair.Key;
      if (renderer == null)
      {
        _removed.Add(renderer);
        continue;
      }
      Profile profile = Resolve(renderer);
      if (profile == null)
      {
        Restore(pair.Value);
        _removed.Add(renderer);
      }
      else if (changed)
      {
        Restore(pair.Value);
        Render(pair.Value, profile);
      }
    }
    foreach (PlanetRenderer renderer in _removed)
      _states.Remove(renderer);
    scrController controller = scrController.instance;
    if (controller == null)
      return;
    Ensure(controller.planetRed?.planetRenderer);
    Ensure(controller.planetBlue?.planetRenderer);
    Ensure(controller.planetGreen?.planetRenderer);
  }

  private bool ReadProfiles()
  {
    bool changed = _enabled != _settings.Enabled;
    _enabled = _settings.Enabled;
    changed |= ReadProfile(0, _settings.Red);
    changed |= ReadProfile(1, _settings.Blue);
    changed |= ReadProfile(2, _settings.Green);
    return changed;
  }

  private bool ReadProfile(int index, PlanetAppearanceProfile source)
  {
    if (_profiles[index]?.Matches(source) == true)
      return false;
    _profiles[index] = new Profile(source);
    return true;
  }

  private Profile Resolve(PlanetRenderer renderer)
  {
    if (!_settings.Enabled || renderer == null || renderer.objectDecoration != null)
      return null;
    scrController controller = scrController.instance;
    if (controller == null)
      return null;
    int index =
      renderer == controller.planetRed?.planetRenderer ? 0
      : renderer == controller.planetBlue?.planetRenderer ? 1
      : renderer == controller.planetGreen?.planetRenderer ? 2
      : -1;
    return index >= 0 && _profiles[index].Enabled ? _profiles[index] : null;
  }

  private PlanetState GetState(PlanetRenderer renderer)
  {
    if (renderer == null || renderer.sprite == null || renderer.sprite.meshRenderer == null)
      return null;
    if (!_states.TryGetValue(renderer, out PlanetState state))
      _states.Add(renderer, state = new PlanetState(renderer));
    return state;
  }

  private void Ensure(PlanetRenderer renderer)
  {
    Profile profile = Resolve(renderer);
    if (profile == null || _states.ContainsKey(renderer))
      return;
    PlanetState state = GetState(renderer);
    if (state != null)
      Render(state, profile);
  }

  // A preset/load can replace the sprite and particle systems. Suspend individual
  // overrides until the outer operation finishes, then capture its complete result.
  public PlanetState BeginAppearanceChange(PlanetRenderer renderer)
  {
    if (_applying || Resolve(renderer) == null)
      return null;
    PlanetState state = GetState(renderer);
    if (state == null)
      return null;
    if (state.Depth++ == 0)
      Restore(state);
    return state;
  }

  public void EndAppearanceChange(PlanetState state)
  {
    if (state == null || --state.Depth != 0 || state.Renderer == null)
      return;
    state.Capture();
    Profile profile = Resolve(state.Renderer);
    if (profile != null)
      Render(state, profile);
  }

  public void BeforeColor(PlanetRenderer renderer, string method, ref Color color)
  {
    if (_applying)
      return;
    Profile profile = Resolve(renderer);
    if (profile == null)
      return;
    PlanetState state = GetState(renderer);
    if (state == null || state.Depth > 0)
      return;
    switch (method)
    {
      case "SetPlanetColor":
        state.BodySource = color;
        break; // Nested core/ring/face calls must receive the original alpha.
      case "SetTailColor":
        state.TailSource = color;
        state.TailChanged = true;
        color = profile.Tail.Apply(color);
        break;
      case "SetCoreColor":
        state.CoreSource = color;
        state.CoreChanged = true;
        if (renderer.glow != null)
        {
          // Track the unmodified glow independently: the game multiplies its
          // previous alpha by the new core alpha on every color update.
          renderer.glow.color = state.Glow;
          state.Glow = color.WithAlpha(state.Glow.a * color.a);
        }
        color = profile.Body.Apply(color);
        break;
      case "SetRingColor":
        state.RingSource = color;
        color = profile.Ring.Apply(color);
        break;
      case "SetFaceColor":
        state.FaceSource = color;
        color = profile.Body.Apply(color);
        break;
    }
  }

  public void AfterBodyColor(PlanetRenderer renderer)
  {
    if (_applying || !_states.TryGetValue(renderer, out PlanetState state) || state.Depth > 0)
      return;
    Profile profile = Resolve(renderer);
    if (profile != null)
      RenderBody(state, profile);
  }

  public void AfterFrame(PlanetRenderer renderer)
  {
    if (_applying || !renderer.rainbow || Resolve(renderer) is not Profile profile)
      return;
    PlanetState state = GetState(renderer);
    if (state == null || state.Depth > 0)
      return;
    // The game writes rainbow body RGB directly each frame; retain that source
    // while the normal color setters handle the other components without polling.
    state.BodySource = Color.HSVToRGB(PlanetRenderer.rainbowHue, 1, 1);
    RenderBody(state, profile);
  }

  private static void RenderBody(PlanetState state, Profile profile)
  {
    PlanetRenderer renderer = state.Renderer;
    if (renderer.sprite == null)
      return;
    if (profile.Body.Override && ADOBase.gc != null)
      renderer.sprite.sprite = ADOBase.gc.tex_planetWhite;
    renderer.sprite.color = profile.Body.Apply(state.BodySource);
  }

  private void Render(PlanetState state, Profile profile)
  {
    PlanetRenderer renderer = state.Renderer;
    if (renderer == null)
      return;
    _applying = true;
    try
    {
      RenderBody(state, profile);
      if (renderer.coreParticles != null)
      {
        if (renderer.glow != null)
          renderer.glow.color = state.Glow;
        renderer.SetCoreColor(profile.Body.Apply(state.CoreSource));
        if (renderer.glow != null)
          renderer.glow.color = profile.Body.Apply(state.Glow);
      }
      if (renderer.tailParticles != null)
        renderer.SetTailColor(profile.Tail.Apply(state.TailSource));
      if (renderer.ringComp != null)
      {
        Color ring = profile.Ring.Apply(state.RingSource);
        ring.a *= 0.4f;
        renderer.ringComp.color = ring;
      }
      if (renderer.faceSprite != null)
        renderer.faceSprite.color = profile.Body.Apply(state.FaceSource);
    }
    finally
    {
      _applying = false;
    }
  }

  private void Restore(PlanetState state)
  {
    PlanetRenderer renderer = state.Renderer;
    if (renderer == null)
      return;
    _applying = true;
    try
    {
      state.Restore();
      // Direct map/rainbow color updates may have arrived since the snapshot.
      if (state.CoreChanged && renderer.coreParticles != null)
      {
        renderer.SetCoreColor(state.CoreSource);
        if (renderer.glow != null)
          renderer.glow.color = state.Glow;
      }
      if (state.TailChanged && renderer.tailParticles != null)
        renderer.SetTailColor(state.TailSource);
    }
    finally
    {
      _applying = false;
    }
  }

  public void ForgetScene()
  {
    _removed.Clear();
    foreach (var pair in _states)
      if (pair.Key == null)
        _removed.Add(pair.Key);
    foreach (PlanetRenderer renderer in _removed)
      _states.Remove(renderer);
  }

  // Preview rendering must use the game's input colors/texture, not the already
  // overridden player visuals (which would apply opacity a second time).
  internal PlanetState GetPreviewState(PlanetRenderer renderer) =>
    renderer != null && _states.TryGetValue(renderer, out PlanetState state) ? state : null;

  public void Dispose()
  {
    foreach (PlanetState state in _states.Values)
      Restore(state);
    _states.Clear();
  }

  private sealed class Profile
  {
    internal readonly bool Enabled;
    internal readonly ComponentColor Body,
      Tail,
      Ring;

    internal Profile(PlanetAppearanceProfile source)
    {
      Enabled = source.Enabled;
      Body = new(source.Body);
      Tail = new(source.Tail);
      Ring = new(source.Ring);
    }

    internal bool Matches(PlanetAppearanceProfile source) =>
      Enabled == source.Enabled && Body.Matches(source.Body) && Tail.Matches(source.Tail) && Ring.Matches(source.Ring);
  }

  private readonly struct ComponentColor
  {
    internal readonly bool Override;
    private readonly Color _color;
    private readonly float _opacity;
    private readonly string _hex;

    internal ComponentColor(PlanetComponentAppearance source)
    {
      Override = source.OverrideColor;
      _hex = source.ColorHex;
      ColorUtility.TryParseHtmlString(source.ColorHex, out _color);
      _opacity = source.Opacity / 100;
    }

    internal bool Matches(PlanetComponentAppearance source) =>
      Override == source.OverrideColor && _hex == source.ColorHex && _opacity == source.Opacity / 100;

    internal Color Apply(Color original)
    {
      Color result = Override ? _color : original;
      result.a = original.a * _opacity;
      return result;
    }
  }

  internal sealed class PlanetState
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
    private ParticleColors _core,
      _tail,
      _explosion;

    internal PlanetState(PlanetRenderer renderer)
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

  private readonly struct ParticleColors
  {
    private readonly ParticleSystem _system;
    private readonly ParticleSystem.MinMaxGradient _start,
      _lifetime;

    internal ParticleColors(ParticleSystem system)
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
}
