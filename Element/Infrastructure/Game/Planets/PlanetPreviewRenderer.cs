using System;
using Element.Application.Gameplay;
using Element.Configuration;
using Element.Configuration.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// Renders the game's planet assets without creating a scrPlanet, PlanetRenderer,
/// or any other gameplay behaviour. Preview objects are inactive between renders.
/// </summary>
internal sealed class PlanetPreviewRenderer : IPlanetPreview
{
  private readonly PlanetPreviewSources _sources;
  private readonly PlanetPreviewVisual[] _previews = new PlanetPreviewVisual[3];
  private GameObject _root;
  private Camera _camera;

  // The settings page shows one planet at a time, so all previews share one
  // target. The owner frees it when settings close.
  private RenderTexture _target;
  private int _targetIndex = -1;
  private int _targetRevision;
  private bool _disposed;

  /// <param name="appearance">The planets' colors before planet appearance; null outside the game.</param>
  public PlanetPreviewRenderer(PlanetAppearanceController appearance) =>
    _sources = new PlanetPreviewSources(appearance);

  public Texture Render(int planetIndex, PlanetAppearanceProfile profile)
  {
    if (_disposed || profile == null || planetIndex < 0 || planetIndex >= _previews.Length)
      return null;
    PlanetPreviewSource source = _sources.Resolve(planetIndex);
    if (source.Body == null || source.Body.sprite == null || source.Body.sharedMaterial == null)
      return null;
    EnsureCamera();
    PlanetPreviewVisual preview = _previews[planetIndex];
    if (preview == null || !preview.SameAssets(source))
    {
      preview?.Dispose();
      _previews[planetIndex] = preview = new PlanetPreviewVisual(_root.transform, source);
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
      name = "Element Planet Preview",
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
    _root = new GameObject("Element Planet Preview") { hideFlags = HideFlags.HideAndDontSave };
    _root.SetActive(false);
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    // Keep even other objects on the private layer outside the preview frustum.
    _root.transform.position = new Vector3(10000, 10000, 10000);
    GameObject cameraObject = PlanetPreviewObjects.Child(_root.transform, "Preview Camera");
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
    _camera.cullingMask = 1 << PlanetPreviewObjects.Layer;
    _camera.allowHDR = false;
    _camera.allowMSAA = true;
    _camera.useOcclusionCulling = false;
    _camera.transform.localPosition = new Vector3(-0.35f, 0, -10);
  }

  private static int AppearanceHash(PlanetPreviewSource source, PlanetAppearanceProfile profile)
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

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    foreach (PlanetPreviewVisual preview in _previews)
      preview?.Dispose();
    if (_root != null)
      PlanetPreviewObjects.Destroy(_root);
    _root = null;
    _camera = null;
    if (_target != null)
    {
      _target.Release();
      PlanetPreviewObjects.Destroy(_target);
    }
    _target = null;
  }
}
