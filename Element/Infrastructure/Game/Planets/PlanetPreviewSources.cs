using UnityEngine;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// Finds the planet a preview copies: the live player planet, or outside a level
/// the color screen's prefab planet in the player's saved color. Never runs a
/// planet's gameplay callbacks.
/// </summary>
internal sealed class PlanetPreviewSources
{
  private readonly PlanetAppearanceController _appearance;
  private PlanetRenderer _fallback;
  private float _nextFallbackLookup;

  /// <param name="appearance">The planets' colors before planet appearance; null outside the game.</param>
  public PlanetPreviewSources(PlanetAppearanceController appearance) => _appearance = appearance;

  public PlanetPreviewSource Resolve(int index)
  {
    scrController controller = scrController.instance;
    PlanetRenderer renderer = index switch
    {
      0 => controller?.planetRed?.planetRenderer,
      1 => controller?.planetBlue?.planetRenderer,
      _ => controller?.planetGreen?.planetRenderer,
    };
    bool isLive = renderer != null;
    if (!isLive)
    {
      if (_fallback == null && Time.unscaledTime >= _nextFallbackLookup)
      {
        _nextFallbackLookup = Time.unscaledTime + 1;
        GameObject prefab = ADOBase.gc?.prefab_colorsScreen;
        if (prefab != null)
          foreach (PlanetRenderer candidate in prefab.GetComponentsInChildren<PlanetRenderer>(true))
            if (candidate.GetComponent<SpriteRenderer>()?.sprite != null)
            {
              _fallback = candidate;
              break;
            }
      }
      renderer = _fallback;
    }
    if (renderer == null)
      return default;
    PlanetSprite sprite = renderer.sprite != null ? renderer.sprite : renderer.GetComponent<PlanetSprite>();
    SpriteRenderer body =
      sprite != null ? sprite.GetComponent<SpriteRenderer>() : renderer.GetComponent<SpriteRenderer>();
    if (body == null)
      return default;
    var state = _appearance?.GetPreviewState(renderer);
    var source = new PlanetPreviewSource
    {
      Renderer = renderer,
      Body = body,
      Glow = renderer.glow,
      Ring = renderer.ring,
      Tail = renderer.tailParticles,
      Core = renderer.coreParticles,
      BodyTexture = state?.SourceTexture ?? ReadPlanetTexture(body),
      AlphaMask = ReadTexture(body, "_AlphaMask"),
      Shader = body.sharedMaterial != null ? body.sharedMaterial.shader : null,
      BodyColor = state?.BodySource ?? body.color,
      GlowColor = state?.Glow ?? (renderer.glow != null ? renderer.glow.color : Color.clear),
      RingColor =
        state != null ? state.RingSource.WithAlpha(state.RingSource.a * 0.4f)
        : renderer.ring != null ? renderer.ring.startColor
        : Color.clear,
      TailColor = state?.TailSource ?? ReadParticleColor(renderer.tailParticles),
      CoreColor = state?.CoreSource ?? ReadParticleColor(renderer.coreParticles),
    };
    if (!isLive)
      ResolveMenuColors(index, ref source);
    return source;
  }

  private static void ResolveMenuColors(int index, ref PlanetPreviewSource source)
  {
    // Prefabs have not run LoadPlanetColor. Resolve the persisted player preset
    // directly, without running the prefab's gameplay callbacks.
    PlanetColor color = index == 2 ? new PlanetColor(Color.green) : Persistence.GetPlayerColor(index == 0);
    Color tint = color.ToRealColor();
    if (color.preset == PlanetColorPreset.Gold || color.preset == PlanetColorPreset.Overseer)
    {
      bool gold = color.preset == PlanetColorPreset.Gold;
      GameObject special = gold ? source.Renderer.planetGold : source.Renderer.planetOverseer;
      string suffix = gold ? "Gold" : "Ov";
      if (special != null)
      {
        Transform root = special.transform;
        SpriteRenderer body = root.Find("Planet" + suffix)?.GetComponent<SpriteRenderer>();
        if (body != null)
        {
          source.Body = body;
          source.BodyTexture = ReadPlanetTexture(body);
          source.AlphaMask = ReadTexture(body, "_AlphaMask");
          source.Shader = body.sharedMaterial != null ? body.sharedMaterial.shader : null;
          source.BodyColor = gold ? body.color : new Color(9f / 85f, 0.64705884f, 40f / 51f, 1);
          source.Glow = root.Find("Glow" + suffix)?.GetComponent<SpriteRenderer>() ?? source.Glow;
          source.Ring = root.Find("Ring" + suffix)?.GetComponent<LineRenderer>() ?? source.Ring;
          source.Tail = root.Find("Tail" + suffix)?.GetComponent<ParticleSystem>() ?? source.Tail;
          source.Core = root.Find("Core" + suffix)?.GetComponent<ParticleSystem>() ?? source.Core;
          source.GlowColor = source.Glow != null ? source.Glow.color : Color.clear;
          source.RingColor = tint.WithAlpha(0.4f);
          source.TailColor = ReadParticleColor(source.Tail);
          source.CoreColor = ReadParticleColor(source.Core);
          return;
        }
      }
    }
    if (color.preset == PlanetColorPreset.Rainbow)
      tint = Color.HSVToRGB(0.08f, 1, 1);
    bool red = color.preset == PlanetColorPreset.DefaultRed;
    bool blue = color.preset == PlanetColorPreset.DefaultBlue;
    source.BodyTexture =
      red ? ADOBase.gc.tex_planetRed
      : blue ? ADOBase.gc.tex_planetBlue
      : ADOBase.gc.tex_planetWhite;
    source.BodyColor = red || blue ? Color.white : tint;
    source.CoreColor = tint;
    source.TailColor = tint;
    source.RingColor = tint.WithAlpha(0.4f);
    source.GlowColor = tint.WithAlpha(source.GlowColor.a);
    if (
      color.preset == PlanetColorPreset.TransBlue
      || color.preset == PlanetColorPreset.TransPink
      || color.preset == PlanetColorPreset.NBYellow
    )
      source.TailColor = Color.white;
    else if (color.preset == PlanetColorPreset.NBPurple)
      source.TailColor = Color.black;
  }

  private static Texture ReadPlanetTexture(SpriteRenderer renderer) =>
    renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_PlanetTex")
      ? renderer.sharedMaterial.GetTexture("_PlanetTex")
      : renderer.sprite?.texture;

  private static Texture ReadTexture(SpriteRenderer renderer, string property) =>
    renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(property)
      ? renderer.sharedMaterial.GetTexture(property)
      : null;

  private static Color ReadParticleColor(ParticleSystem system)
  {
    if (system == null)
      return Color.white;
    var lifetime = system.colorOverLifetime.color;
    Gradient gradient =
      lifetime.mode == ParticleSystemGradientMode.TwoGradients ? lifetime.gradientMin : lifetime.gradient;
    Color result = gradient != null ? gradient.Evaluate(0) : system.main.startColor.color;
    result.a = system.main.startColor.color.a;
    return result;
  }
}
