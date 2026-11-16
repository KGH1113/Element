using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Tweaks;
using UnityEngine;
using UnityEngine.Rendering;
using Element.Configuration.Gameplay;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// One planet's preview: copies of its body, glow, ring and particles on the
/// preview layer, with their own materials, colored for a planet appearance.
/// Inactive except while it renders.
/// </summary>
internal sealed class PlanetPreviewVisual : IDisposable
{
  internal readonly GameObject Root;
  internal float HalfHeight = 1.7f;
  private readonly PlanetPreviewSource _source;
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

  internal PlanetPreviewVisual(Transform parent, PlanetPreviewSource source)
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
    Root = PlanetPreviewObjects.Child(parent, "Planet Visuals");
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

  internal bool SameAssets(PlanetPreviewSource source) =>
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

  internal void Apply(PlanetPreviewSource source, PlanetAppearanceProfile profile)
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

  private static Color ApplyColor(Color source, PlanetComponentAppearance component)
  {
    bool read = ColorUtility.TryParseHtmlString(component.ColorHex, out Color parsed);
    Color result = PlanetColorRule.UsesChosenColor(component.OverrideColor, read) ? parsed : source;
    result.a = PlanetColorRule.Alpha(source.a, component.Opacity);
    return result;
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
    SpriteRenderer copy = PlanetPreviewObjects.Child(Root.transform, name).AddComponent<SpriteRenderer>();
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
    LineRenderer copy = PlanetPreviewObjects.Child(Root.transform, "Ring").AddComponent<LineRenderer>();
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
    ParticleSystem copy = PlanetPreviewObjects.Child(Root.transform, name).AddComponent<ParticleSystem>();
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
    for (Transform current = source; current != null && current != _source.Renderer.transform; current = current.parent)
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
      PlanetPreviewObjects.Destroy(Root);
    }
    foreach (Material material in _materials)
      PlanetPreviewObjects.Destroy(material);
  }
}
