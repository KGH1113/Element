using System;
using System.Collections.Generic;
using ImplResourcePack.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Infrastructure.Game.Tweaks;

/// <summary>
/// Renders the game's planet assets without creating a scrPlanet, PlanetRenderer,
/// or any other gameplay behaviour. Preview objects are inactive between renders.
/// </summary>
internal sealed class PlanetPreviewRenderer : IDisposable
{
  private const int PreviewLayer = 31;
  private readonly PlanetAppearanceController _appearance;
  private readonly Preview[] _previews = new Preview[3];
  private GameObject _root;
  private Camera _camera;

  // The settings page shows one planet at a time, so all previews share one
  // target. The owner frees it when settings close.
  private RenderTexture _target;
  private int _targetIndex = -1;
  private int _targetRevision;
  private PlanetRenderer _fallback;
  private float _nextFallbackLookup;
  private bool _disposed;

  public PlanetPreviewRenderer(PlanetAppearanceController appearance) => _appearance = appearance;

  public Texture Render(int planetIndex, PlanetAppearanceProfile profile)
  {
    if (_disposed || profile == null || planetIndex < 0 || planetIndex >= _previews.Length)
      return null;
    Source source = ResolveSource(planetIndex);
    if (source.Body == null || source.Body.sprite == null || source.Body.sharedMaterial == null)
      return null;
    EnsureCamera();
    Preview preview = _previews[planetIndex];
    if (preview == null || !preview.SameAssets(source))
    {
      preview?.Dispose();
      _previews[planetIndex] = preview = new Preview(_root.transform, source);
      if (_targetIndex == planetIndex)
        _targetIndex = -1;
    }
    int revision = AppearanceHash(source, profile);
    RenderTexture target = EnsureTarget();
    if (_targetIndex == planetIndex && _targetRevision == revision && target.IsCreated())
      return target;

    // The camera is disabled and only this visual tree is temporarily active.
    // No scene camera can see these objects during its normal render loop.
    RenderTexture previous = RenderTexture.active;
    try
    {
      _root.SetActive(true);
      preview.Root.SetActive(true);
      preview.Apply(source, profile);
      if (!target.IsCreated())
        target.Create();
      _camera.orthographicSize = preview.HalfHeight;
      _camera.targetTexture = target;
      _camera.Render();
      _targetIndex = planetIndex;
      _targetRevision = revision;
    }
    finally
    {
      preview.Root.SetActive(false);
      _root.SetActive(false);
      _camera.targetTexture = null;
      RenderTexture.active = previous;
    }
    return target;
  }

  private RenderTexture EnsureTarget()
  {
    if (_target != null)
      return _target;
    // The planet body is premultiplied but its ring uses straight alpha. An RGB
    // target avoids multiplying either composited result again in RawImage.
    RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
      ? RenderTextureFormat.RGB111110Float
      : RenderTextureFormat.ARGB32;
    // Sprites, lines and particles are ordered by sorting order, not depth;
    // a 4x MSAA depth buffer would only add another 4 MB.
    _target = new RenderTexture(640, 400, 0, format)
    {
      name = "ImplResourcePack Planet Preview",
      hideFlags = HideFlags.HideAndDontSave,
      antiAliasing = 4,
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp,
    };
    _target.Create();
    _targetIndex = -1;
    return _target;
  }

  private void EnsureCamera()
  {
    if (_root != null)
      return;
    _root = new GameObject("ImplResourcePack Planet Preview") { hideFlags = HideFlags.HideAndDontSave };
    _root.SetActive(false);
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    // Keep even other objects on the private layer outside the preview frustum.
    _root.transform.position = new Vector3(10000, 10000, 10000);
    GameObject cameraObject = Child(_root.transform, "Preview Camera");
    _camera = cameraObject.AddComponent<Camera>();
    _camera.enabled = false;
    _camera.orthographic = true;
    _camera.orthographicSize = 1.7f;
    _camera.aspect = 1.6f;
    _camera.nearClipPlane = 0.1f;
    _camera.farClipPlane = 20;
    _camera.clearFlags = CameraClearFlags.SolidColor;
    // Composite opacity once, against the same neutral surface as the UI card.
    _camera.backgroundColor = new Color(0.115f, 0.115f, 0.115f, 1);
    _camera.cullingMask = 1 << PreviewLayer;
    _camera.allowHDR = false;
    _camera.allowMSAA = true;
    _camera.useOcclusionCulling = false;
    _camera.transform.localPosition = new Vector3(-0.35f, 0, -10);
  }

  private Source ResolveSource(int index)
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
    var source = new Source
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

  private static void ResolveMenuColors(int index, ref Source source)
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

  private static int AppearanceHash(Source source, PlanetAppearanceProfile profile)
  {
    unchecked
    {
      int hash = source.BodyTexture != null ? source.BodyTexture.GetInstanceID() : 0;
      hash = hash * 31 + source.BodyColor.GetHashCode();
      hash = hash * 31 + source.GlowColor.GetHashCode();
      hash = hash * 31 + source.RingColor.GetHashCode();
      hash = hash * 31 + source.TailColor.GetHashCode();
      hash = hash * 31 + source.CoreColor.GetHashCode();
      hash = hash * 31 + profile.Enabled.GetHashCode();
      hash = ComponentHash(hash, profile.Body);
      hash = ComponentHash(hash, profile.Tail);
      return ComponentHash(hash, profile.Ring);
    }
  }

  private static int ComponentHash(int hash, PlanetComponentAppearance component)
  {
    unchecked
    {
      hash = hash * 31 + component.OverrideColor.GetHashCode();
      hash = hash * 31 + (component.ColorHex?.GetHashCode() ?? 0);
      return hash * 31 + component.Opacity.GetHashCode();
    }
  }

  private static Color ApplyColor(Color source, PlanetComponentAppearance component)
  {
    Color result =
      component.OverrideColor && ColorUtility.TryParseHtmlString(component.ColorHex, out Color parsed)
        ? parsed
        : source;
    result.a = source.a * Mathf.Clamp01(component.Opacity / 100);
    return result;
  }

  private static GameObject Child(Transform parent, string name)
  {
    var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = PreviewLayer };
    child.transform.SetParent(parent, false);
    return child;
  }

  private static void DestroyOwned(Object value)
  {
    if (value == null)
      return;
    if (UnityEngine.Application.isPlaying)
      Object.Destroy(value);
    else
      Object.DestroyImmediate(value);
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    foreach (Preview preview in _previews)
      preview?.Dispose();
    if (_root != null)
      DestroyOwned(_root);
    _root = null;
    _camera = null;
    if (_target != null)
    {
      _target.Release();
      DestroyOwned(_target);
    }
    _target = null;
  }

  private struct Source
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

  private sealed class Preview : IDisposable
  {
    internal readonly GameObject Root;
    internal float HalfHeight = 1.7f;
    private readonly Source _source;
    private readonly List<Material> _materials = new();
    private readonly SpriteRenderer _body,
      _glow;
    private readonly LineRenderer _ring;
    private readonly ParticleSystem _tail,
      _core;
    private readonly Material _bodyMaterial;
    private readonly Sprite _bodySprite;
    private readonly Material _sourceBodyMaterial;
    private readonly Material _sourceGlowMaterial,
      _sourceRingMaterial,
      _sourceTailMaterial,
      _sourceCoreMaterial;
    private readonly Sprite _sourceGlowSprite;
    private readonly Texture _sourceAlphaMask;
    private readonly Shader _sourceShader;
    private readonly MaterialPropertyBlock _bodyProperties = new();
    private readonly ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[256];
    private bool _particlesSeeded;

    internal Preview(Transform parent, Source source)
    {
      _source = source;
      _bodySprite = source.Body.sprite;
      _sourceBodyMaterial = source.Body.sharedMaterial;
      _sourceGlowMaterial = source.Glow != null ? source.Glow.sharedMaterial : null;
      _sourceGlowSprite = source.Glow != null ? source.Glow.sprite : null;
      _sourceRingMaterial = source.Ring != null ? source.Ring.sharedMaterial : null;
      _sourceTailMaterial = ParticleMaterial(source.Tail);
      _sourceCoreMaterial = ParticleMaterial(source.Core);
      _sourceAlphaMask = source.AlphaMask;
      _sourceShader = source.Shader;
      Root = Child(parent, "Planet Visuals");
      try
      {
        Root.SetActive(false);
        float size = Mathf.Max(0.01f, Mathf.Max(source.Body.sprite.bounds.size.x, source.Body.sprite.bounds.size.y));
        float scale = 1.15f / size;
        _body = CopySprite(source.Body, "Body", scale, 20);
        _body.transform.localPosition = Vector3.zero;
        _body.transform.localRotation = Quaternion.identity;
        _bodyMaterial = _body.sharedMaterial;
        if (_bodyMaterial.HasProperty("_Frame"))
          _bodyMaterial.SetFloat("_Frame", 3);
        source.Body.GetPropertyBlock(_bodyProperties);
        _glow = CopySprite(source.Glow, "Glow", scale, 10);
        _ring = CopyRing(source.Ring, scale);
        _tail = CopyParticles(source.Tail, "Tail", scale, 4);
        _core = CopyParticles(source.Core, "Core", scale, 15);
      }
      catch
      {
        Dispose();
        throw;
      }
    }

    internal bool SameAssets(Source source) =>
      Root != null
      && _source.Body == source.Body
      && _bodySprite == source.Body.sprite
      && _sourceBodyMaterial == source.Body.sharedMaterial
      && _sourceAlphaMask == source.AlphaMask
      && _sourceShader == source.Shader
      && _source.Glow == source.Glow
      && _sourceGlowMaterial == (source.Glow != null ? source.Glow.sharedMaterial : null)
      && _sourceGlowSprite == (source.Glow != null ? source.Glow.sprite : null)
      && _source.Ring == source.Ring
      && _sourceRingMaterial == (source.Ring != null ? source.Ring.sharedMaterial : null)
      && _source.Tail == source.Tail
      && _sourceTailMaterial == ParticleMaterial(source.Tail)
      && _source.Core == source.Core
      && _sourceCoreMaterial == ParticleMaterial(source.Core);

    private static Material ParticleMaterial(ParticleSystem source) =>
      source != null ? source.GetComponent<ParticleSystemRenderer>()?.sharedMaterial : null;

    internal void Apply(Source source, PlanetAppearanceProfile profile)
    {
      Texture bodyTexture = profile.Body.OverrideColor ? ADOBase.gc.tex_planetWhite : source.BodyTexture;
      if (_bodyMaterial.HasProperty("_PlanetTex"))
      {
        _bodyMaterial.SetTexture("_PlanetTex", bodyTexture);
        _bodyProperties.SetTexture("_PlanetTex", bodyTexture);
      }
      _bodyProperties.SetFloat("_Frame", 3);
      _body.SetPropertyBlock(_bodyProperties);
      _body.color = ApplyColor(source.BodyColor, profile.Body);
      if (_glow != null)
        _glow.color = ApplyColor(source.GlowColor, profile.Body);
      if (_ring != null)
        _ring.startColor = _ring.endColor = ApplyColor(source.RingColor, profile.Ring);
      SetParticleColor(_tail, ApplyColor(source.TailColor, profile.Tail), true);
      SetParticleColor(_core, ApplyColor(source.CoreColor, profile.Body), false);
      if (!_particlesSeeded || (_tail != null && _tail.particleCount == 0))
      {
        SeedParticles(_tail, true);
        SeedParticles(_core, false);
        _particlesSeeded = true;
      }
    }

    private Material CopyMaterial(Material source)
    {
      if (source == null)
        return null;
      var copy = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
      _materials.Add(copy);
      return copy;
    }

    private SpriteRenderer CopySprite(SpriteRenderer source, string name, float scale, int order)
    {
      if (source == null || source.sprite == null || source.sharedMaterial == null)
        return null;
      SpriteRenderer copy = Child(Root.transform, name).AddComponent<SpriteRenderer>();
      copy.sprite = source.sprite;
      copy.sharedMaterial = CopyMaterial(source.sharedMaterial);
      copy.flipX = source.flipX;
      copy.flipY = source.flipY;
      copy.drawMode = source.drawMode;
      copy.size = source.size;
      copy.sortingOrder = order;
      copy.maskInteraction = SpriteMaskInteraction.None;
      copy.shadowCastingMode = ShadowCastingMode.Off;
      copy.receiveShadows = false;
      var properties = new MaterialPropertyBlock();
      source.GetPropertyBlock(properties);
      copy.SetPropertyBlock(properties);
      CopyTransform(source.transform, copy.transform, scale);
      return copy;
    }

    private LineRenderer CopyRing(LineRenderer source, float scale)
    {
      if (source == null || source.sharedMaterial == null || source.positionCount < 2)
        return null;
      LineRenderer copy = Child(Root.transform, "Ring").AddComponent<LineRenderer>();
      copy.sharedMaterial = CopyMaterial(source.sharedMaterial);
      copy.positionCount = source.positionCount;
      var positions = new Vector3[source.positionCount];
      source.GetPositions(positions);
      float maximum = 0;
      for (int i = 0; i < positions.Length; i++)
      {
        if (source.useWorldSpace)
          positions[i] = source.transform.InverseTransformPoint(positions[i]);
        maximum = Mathf.Max(maximum, positions[i].magnitude);
      }
      // scrRing.Switch animates its transform to tileSize for the chosen planet,
      // and to zero otherwise. Preserve its authored one-tile geometry using the
      // same normalization as the body, without the transient zero scale.
      float ringScale = scale;
      HalfHeight = Mathf.Max(HalfHeight, maximum * ringScale * 1.12f);
      for (int i = 0; i < positions.Length; i++)
        positions[i] *= ringScale;
      copy.SetPositions(positions);
      copy.useWorldSpace = false;
      copy.loop = source.loop;
      copy.widthCurve = source.widthCurve;
      copy.widthMultiplier = source.widthMultiplier * ringScale;
      copy.numCapVertices = source.numCapVertices;
      copy.numCornerVertices = source.numCornerVertices;
      copy.textureMode = source.textureMode;
      copy.alignment = LineAlignment.View;
      copy.sortingOrder = 0;
      copy.shadowCastingMode = ShadowCastingMode.Off;
      copy.receiveShadows = false;
      var properties = new MaterialPropertyBlock();
      source.GetPropertyBlock(properties);
      copy.SetPropertyBlock(properties);
      return copy;
    }

    private ParticleSystem CopyParticles(ParticleSystem source, string name, float scale, int order)
    {
      ParticleSystemRenderer sourceRenderer = source != null ? source.GetComponent<ParticleSystemRenderer>() : null;
      if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
        return null;
      ParticleSystem copy = Child(Root.transform, name).AddComponent<ParticleSystem>();
      copy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      copy.useAutoRandomSeed = false;
      copy.randomSeed = 317;
      var main = copy.main;
      var original = source.main;
      main.playOnAwake = false;
      main.loop = false;
      main.maxParticles = 256;
      main.startLifetime = original.startLifetime;
      main.startSpeed = original.startSpeed;
      main.startSize3D = original.startSize3D;
      main.startSizeX = original.startSizeX;
      main.startSizeY = original.startSizeY;
      main.startSizeZ = original.startSizeZ;
      main.startRotation3D = original.startRotation3D;
      main.startRotationX = original.startRotationX;
      main.startRotationY = original.startRotationY;
      main.startRotationZ = original.startRotationZ;
      main.gravityModifier = original.gravityModifier;
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.scalingMode = ParticleSystemScalingMode.Hierarchy;
      var emission = copy.emission;
      emission.enabled = false;
      var shape = copy.shape;
      var originalShape = source.shape;
      shape.enabled = originalShape.enabled;
      shape.shapeType = originalShape.shapeType;
      shape.radius = originalShape.radius;
      shape.radiusThickness = originalShape.radiusThickness;
      shape.angle = originalShape.angle;
      shape.length = originalShape.length;
      shape.arc = originalShape.arc;
      shape.position = originalShape.position;
      shape.rotation = originalShape.rotation;
      shape.scale = originalShape.scale;
      shape.mesh = originalShape.mesh;
      var size = copy.sizeOverLifetime;
      var originalSize = source.sizeOverLifetime;
      size.enabled = originalSize.enabled;
      size.separateAxes = originalSize.separateAxes;
      size.x = originalSize.x;
      size.y = originalSize.y;
      size.z = originalSize.z;
      var rotation = copy.rotationOverLifetime;
      var originalRotation = source.rotationOverLifetime;
      rotation.enabled = originalRotation.enabled;
      rotation.separateAxes = originalRotation.separateAxes;
      rotation.x = originalRotation.x;
      rotation.y = originalRotation.y;
      rotation.z = originalRotation.z;
      var renderer = copy.GetComponent<ParticleSystemRenderer>();
      renderer.sharedMaterial = CopyMaterial(sourceRenderer.sharedMaterial);
      renderer.renderMode = sourceRenderer.renderMode;
      renderer.mesh = sourceRenderer.mesh;
      renderer.alignment = sourceRenderer.alignment;
      renderer.velocityScale = sourceRenderer.velocityScale;
      renderer.lengthScale = sourceRenderer.lengthScale;
      renderer.sortingOrder = order;
      renderer.shadowCastingMode = ShadowCastingMode.Off;
      renderer.receiveShadows = false;
      copy.transform.localScale = Vector3.one * scale;
      return copy;
    }

    private void CopyTransform(Transform source, Transform target, float scale)
    {
      Matrix4x4 matrix = Matrix4x4.identity;
      for (
        Transform current = source;
        current != null && current != _source.Renderer.transform;
        current = current.parent
      )
        matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
      target.localPosition = matrix.MultiplyPoint3x4(Vector3.zero) * scale;
      target.localRotation = matrix.rotation;
      target.localScale = matrix.lossyScale * scale;
    }

    private void SetParticleColor(ParticleSystem system, Color color, bool tail)
    {
      if (system == null)
        return;
      // Use the same color-over-lifetime curve as PlanetRenderer.SetTailColor /
      // SetCoreColor. The original particle material supplies the real texture.
      Color start = tail ? color * new Color(0.5f, 0.5f, 0.5f, 1) : color;
      var main = system.main;
      main.startColor = start;
      var lifetime = system.colorOverLifetime;
      lifetime.enabled = true;
      Gradient first = Gradient(color, 0.4f);
      Gradient second = Gradient(Color.Lerp(color, Color.black, 0.4f), 0.6f);
      lifetime.color = new ParticleSystem.MinMaxGradient(first, second);
      // Emitted particles retain their start color when settings change.
      int count = system.GetParticles(_particleBuffer);
      for (int i = 0; i < count; i++)
        _particleBuffer[i].startColor = start;
      if (count > 0)
        system.SetParticles(_particleBuffer, count);
    }

    private static Gradient Gradient(Color color, float alpha)
    {
      var gradient = new Gradient();
      gradient.SetKeys(
        new[] { new GradientColorKey(color, 0), new GradientColorKey(color * 0.6f, 1) },
        new[] { new GradientAlphaKey(alpha, 0), new GradientAlphaKey(0, 1) }
      );
      return gradient;
    }

    private static void SeedParticles(ParticleSystem system, bool tail)
    {
      if (system == null)
        return;
      const int steps = 30;
      float lifetime = Mathf.Clamp(system.main.startLifetime.constantMax, 0.2f, 1.2f);
      for (int i = 0; i < steps; i++)
      {
        float t = i / (float)(steps - 1);
        system.transform.localPosition = tail
          ? new Vector3(-1.8f * (1 - t), -0.45f * (1 - t) * (1 - t), 0)
          : Vector3.zero;
        system.Simulate(lifetime / steps, false, false, true);
        system.Emit(tail ? 3 : 1);
      }
      system.Pause(false);
    }

    public void Dispose()
    {
      if (Root != null)
      {
        Root.SetActive(false);
        DestroyOwned(Root);
      }
      foreach (Material material in _materials)
        DestroyOwned(material);
    }
  }
}
