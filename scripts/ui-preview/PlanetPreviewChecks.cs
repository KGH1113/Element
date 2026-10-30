using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Element.Configuration;
using Element.Infrastructure.Game.Tweaks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  // These checks execute the production renderer with real Unity rendering
  // components and deliberately synthetic textures. They do not validate
  // ADOFAI's shipped assets, shaders, or live gameplay integration.
  internal static class PlanetPreviewChecks
  {
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int _checks;

    internal static int Run(string output)
    {
      _checks = 0;
      var previousController = scrController.instance;
      var previousConstants = ADOBase.gc;
      var previousFire = Persistence.Fire;
      var previousIce = Persistence.Ice;
      RenderTexture previousActive = RenderTexture.active;
      using var source = new SyntheticPlanet();
      var snapshot = new SourceSnapshot(source);
      var appearance = new PlanetAppearanceController();
      using var renderer = new PlanetPreviewRenderer(appearance);
      var activeSentinel = new RenderTexture(8, 8, 0);
      activeSentinel.Create();
      int renders = 0;
      Camera.CameraCallback rendered = camera => { if (camera.name == "Preview Camera") renders++; };
      Camera.onPostRender += rendered;
      try
      {
        ADOBase.gc = new RDConstants {
          prefab_colorsScreen = source.Root,
          tex_planetRed = source.Texture, tex_planetBlue = source.Texture, tex_planetWhite = source.Texture
        };
        scrController.instance = new scrController {
          planetRed = new scrPlanet { planetRenderer = source.Renderer },
          planetBlue = new scrPlanet { planetRenderer = source.Renderer },
          planetGreen = new scrPlanet { planetRenderer = source.Renderer }
        };
        var profile = new PlanetAppearanceProfile("#FF0000");
        profile.Tail.Opacity = profile.Ring.Opacity = 0;
        Assert(renderer.Render(-1, profile) == null && renderer.Render(3, profile) == null && renderer.Render(0, null) == null,
          "invalid preview requests do not allocate a camera");
        Assert(Field<GameObject>(renderer, "_root") == null, "renderer remains lazy until a valid source is requested");
        RenderTexture.active = activeSentinel;
        RenderTexture target = Render(renderer, 0, profile);
        Assert(target.width == 640 && target.height == 400 && target.IsCreated() && target.depth == 0,
          "preview owns a live render target without an unused depth buffer");
        Assert(RenderTexture.active == activeSentinel, "preview rendering restores the caller's active render target");
        Assert(renders > 0, "production preview executes a real Unity camera render");
        int firstRenders = renders;
        Assert(renderer.Render(0, profile) == target && renders == firstRenders,
          "unchanged appearance reuses its render target and skips Camera.Render");
        CheckIsolation(renderer);
        Color[] red = Read(target);
        int center = BodyCenter(renderer, target);
        Color background = red[0];
        Assert(red[center].r > red[center].g + .4f && red[center].r > red[center].b + .4f,
          "the production camera produces the requested red Body pixels");
        Save(target, output, "planet-renderer-synthetic-body");

        profile.Body.ColorHex = "#00FF00";
        Assert(Render(renderer, 0, profile) == target && renders > firstRenders,
          "RGB changes redraw into the existing target");
        Color[] green = Read(target);
        Assert(green[center].g > green[center].r + .4f && green[center].g > green[center].b + .4f,
          "editing Body RGB changes actual rendered pixels");
        profile.Body.Opacity = 50;
        Render(renderer, 0, profile);
        Color[] half = Read(target);
        Assert(half[center].g < green[center].g - .1f && half[center].g > background.g + .1f,
          "half opacity composites between the opaque Body and card surface");
        profile.Body.Opacity = 0;
        Render(renderer, 0, profile);
        Assert(Distance(Read(target)[center], background) < .04f,
          "zero Body opacity reveals the preview surface without a residual body");

        profile.Tail.ColorHex = "#00FF00"; profile.Tail.Opacity = 100;
        Render(renderer, 0, profile);
        int greenTail = CountDominant(Read(target), 1);
        Assert(greenTail > 10, "synthetic source particles produce a visible colored trail");
        ParticleSystem tail = PreviewComponent<ParticleSystem>(renderer, 0, "Tail");
        Assert(!tail.main.playOnAwake && !tail.emission.enabled,
          "preview particles cannot emit automatically between explicit snapshots");
        profile.Tail.ColorHex = "#0000FF";
        Render(renderer, 0, profile);
        Color[] blueTail = Read(target);
        Assert(CountDominant(blueTail, 2) > 10 && CountDominant(blueTail, 1) < greenTail / 4,
          "changing Tail color recolors particles already present in the preview");
        profile.Tail.Opacity = 0;
        Render(renderer, 0, profile);
        Assert(CountDominant(Read(target), 2) < 4, "zero Tail opacity removes its visible particles");

        profile.Ring.ColorHex = "#0000FF"; profile.Ring.Opacity = 100;
        Render(renderer, 0, profile);
        Assert(CountDominant(Read(target), 2) > 40,
          "the source ring asset remains visible even when its source transform is animated to zero scale");
        LineRenderer ring = PreviewComponent<LineRenderer>(renderer, 0, "Ring");
        Assert(ring.positionCount == source.Renderer.ring.positionCount && ring.loop == source.Renderer.ring.loop,
          "preview uses the source ring geometry and closure");
        profile.Ring.Opacity = 50;
        Render(renderer, 0, profile);
        Assert(Mathf.Abs(ring.startColor.a - source.Renderer.ring.startColor.a * .5f) < .001f,
          "Ring opacity multiplies the source alpha once");
        profile.Ring.Opacity = 0;
        Render(renderer, 0, profile);
        Assert(CountDominant(Read(target), 2) < 4, "zero Ring opacity removes its pixels independently");
        snapshot.Check(source);

        profile.Body.ColorHex = "#BA712B"; profile.Body.Opacity = 100;
        profile.Tail.ColorHex = "#2BAE66"; profile.Tail.Opacity = 100;
        profile.Ring.ColorHex = "#469FE0"; profile.Ring.Opacity = 100;
        Render(renderer, 0, profile);
        Save(target, output, "planet-renderer-synthetic-components");
        // Settings show one planet at a time, so all planets share one target.
        int beforeSwitch = renders;
        RenderTexture blueTarget = Render(renderer, 1, new PlanetAppearanceProfile("#3366FF"));
        Assert(blueTarget == target && blueTarget.IsCreated() && renders > beforeSwitch,
          "switching planets redraws the single shared render target");
        int beforeReturn = renders;
        Assert(Render(renderer, 0, profile) == target && renders > beforeReturn,
          "switching back to Fire redraws it instead of showing the previous planet");
        int beforeRepeat = renders;
        Assert(Render(renderer, 0, profile) == target && renders == beforeRepeat,
          "the shown planet is not redrawn while its appearance is unchanged");

        source.Body.sprite = source.AlternateSprite;
        int beforeSprite = renders;
        RenderTexture replacement = Render(renderer, 0, profile);
        Assert(replacement == target && renders > beforeSprite,
          "source sprite replacement rebuilds the preview and redraws the shared target");
        Assert(PreviewComponent<SpriteRenderer>(renderer, 0, "Body").sprite == source.AlternateSprite,
          "the rebuilt preview uses the new source sprite asset");
        source.Body.sprite = source.Sprite;
        target = Render(renderer, 0, profile);
        Material original = source.Body.sharedMaterial;
        source.Body.sharedMaterial = source.AlternateMaterial;
        int beforeMaterial = renders;
        replacement = Render(renderer, 0, profile);
        Assert(replacement == target && renders > beforeMaterial,
          "source material replacement invalidates the visual cache");
        source.Body.sharedMaterial = original;
        target = Render(renderer, 0, profile);
        snapshot.Check(source);

        appearance.States[source.Renderer] = new PlanetAppearanceController.PlanetState {
          SourceTexture = source.Texture, BodySource = new Color(.1f, .4f, .9f, .8f),
          Glow = Color.clear, RingSource = new Color(.2f, .3f, .9f, 1), TailSource = Color.white, CoreSource = Color.clear
        };
        profile.Body.OverrideColor = false; profile.Body.Opacity = 50;
        Render(renderer, 0, profile);
        Color resolved = PreviewComponent<SpriteRenderer>(renderer, 0, "Body").color;
        Assert(Mathf.Abs(resolved.a - .4f) < .001f && Mathf.Abs(resolved.b - .9f) < .001f,
          "map colors use the saved unmodified appearance and apply opacity once");
        snapshot.Check(source);
        CheckIsolation(renderer);

        // A menu fallback is also exercised without constructing game objects or
        // invoking their callbacks. Persisted colors cross only the stub boundary.
        scrController.instance = null;
        Persistence.Fire = new PlanetColor(new Color(.9f, .2f, .1f));
        using (var menu = new PlanetPreviewRenderer(null))
        {
          var menuProfile = new PlanetAppearanceProfile();
          menuProfile.Body.OverrideColor = false;
          menuProfile.Tail.Opacity = menuProfile.Ring.Opacity = 0;
          Render(menu, 0, menuProfile);
          Color menuColor = PreviewComponent<SpriteRenderer>(menu, 0, "Body").color;
          Assert(Mathf.Abs(menuColor.r - .9f) < .001f && Mathf.Abs(menuColor.g - .2f) < .001f,
            "menu fallback resolves persisted player color without running game behaviors");
          CheckIsolation(menu);
        }
        snapshot.Check(source);
        scrController.instance = null; ADOBase.gc.prefab_colorsScreen = null;
        using (var missing = new PlanetPreviewRenderer(null))
        {
          Assert(missing.Render(0, profile) == null && Field<GameObject>(missing, "_root") == null,
            "an absent game source stays unavailable without allocating preview resources");
        }

        GameObject ownedRoot = Field<GameObject>(renderer, "_root");
        var ownedMaterials = ownedRoot.GetComponentsInChildren<Renderer>(true)
          .SelectMany(component => component.sharedMaterials).Where(material => material != null).Distinct().ToArray();
        RenderTexture[] ownedTargets = { Field<RenderTexture>(renderer, "_target") };
        renderer.Dispose();
        Assert(ownedRoot == null && ownedMaterials.All(material => material == null)
          && ownedTargets.All(texture => texture == null || !texture.IsCreated()),
          "disposing preview releases every owned visual, material, and GPU render target");
        Assert(renderer.Render(0, profile) == null, "a disposed renderer cannot allocate new preview resources");
        renderer.Dispose();
        snapshot.Check(source);
      }
      finally
      {
        Camera.onPostRender -= rendered;
        RenderTexture.active = previousActive;
        activeSentinel.Release(); Object.DestroyImmediate(activeSentinel);
        scrController.instance = previousController; ADOBase.gc = previousConstants;
        Persistence.Fire = previousFire; Persistence.Ice = previousIce;
      }
      return _checks;
    }

    private static RenderTexture Render(PlanetPreviewRenderer renderer, int index, PlanetAppearanceProfile profile)
    {
      var texture = renderer.Render(index, profile) as RenderTexture;
      Assert(texture != null && texture.IsCreated(), "valid synthetic game assets produce a render target");
      CheckIsolation(renderer);
      return texture;
    }

    private static void CheckIsolation(PlanetPreviewRenderer renderer)
    {
      GameObject root = Field<GameObject>(renderer, "_root");
      Camera camera = Field<Camera>(renderer, "_camera");
      Assert(root != null && !root.activeSelf && !camera.enabled && camera.targetTexture == null,
        "preview root and camera are inactive with no target between synchronous renders");
      Assert(root.GetComponentsInChildren<PlanetRenderer>(true).Length == 0
        && root.GetComponentsInChildren<PlanetSprite>(true).Length == 0,
        "visual preview does not clone gameplay behaviors");
      foreach (Transform child in root.transform)
        if (child.name == "Planet Visuals") Assert(!child.gameObject.activeSelf, "every cached visual is individually inactive");
    }

    private static T PreviewComponent<T>(PlanetPreviewRenderer renderer, int index, string name) where T : Component
    {
      object preview = ((Array)Field<object>(renderer, "_previews")).GetValue(index);
      return Field<GameObject>(preview, "Root").transform.Find(name).GetComponent<T>();
    }

    private static int BodyCenter(PlanetPreviewRenderer renderer, RenderTexture texture)
    {
      Camera camera = Field<Camera>(renderer, "_camera");
      Vector3 point = camera.WorldToViewportPoint(PreviewComponent<SpriteRenderer>(renderer, 0, "Body").transform.position);
      int x = Mathf.Clamp(Mathf.RoundToInt(point.x * texture.width), 0, texture.width - 1);
      int y = Mathf.Clamp(Mathf.RoundToInt(point.y * texture.height), 0, texture.height - 1);
      return y * texture.width + x;
    }

    private static Color[] Read(RenderTexture texture)
    {
      RenderTexture previous = RenderTexture.active;
      var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
      try
      {
        RenderTexture.active = texture;
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
        return pixels.GetPixels();
      }
      finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
    }

    private static int CountDominant(Color[] pixels, int channel)
    {
      int count = 0;
      foreach (Color value in pixels)
      {
        float selected = channel == 1 ? value.g : value.b;
        if (selected > value.r + .015f && selected > (channel == 1 ? value.b : value.g) + .015f) count++;
      }
      return count;
    }

    private static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    private static void Save(RenderTexture source, string directory, string name)
    {
      var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
      pixels.SetPixels(Read(source)); pixels.Apply();
      File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
      Object.DestroyImmediate(pixels);
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);

    private static void Assert(bool condition, string message)
    {
      _checks++;
      if (!condition) throw new InvalidOperationException("Planet renderer assertion: " + message);
    }

    private sealed class SyntheticPlanet : IDisposable
    {
      internal readonly GameObject Root;
      internal readonly Texture2D Texture;
      internal readonly Sprite Sprite, AlternateSprite;
      internal readonly Material Material, AlternateMaterial;
      internal readonly PlanetRenderer Renderer;
      internal readonly SpriteRenderer Body;

      internal SyntheticPlanet()
      {
        Root = new GameObject("Synthetic planet source"); Root.SetActive(false);
        Root.transform.position = new Vector3(40, 60, 0);
        Shader shader = Shader.Find("Sprites/Default");
        Assert(shader != null, "Unity's stock sprite shader is available for synthetic renderer checks");
        Texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Texture.SetPixels(Enumerable.Repeat(Color.white, 32 * 32).ToArray()); Texture.Apply();
        Sprite = Sprite.Create(Texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32);
        AlternateSprite = Sprite.Create(Texture, new Rect(0, 0, 32, 16), Vector2.one * .5f, 32);
        Material = new Material(shader) { mainTexture = Texture };
        AlternateMaterial = new Material(Material);
        Body = Root.AddComponent<SpriteRenderer>(); Body.sprite = Sprite; Body.sharedMaterial = Material; Body.color = Color.white;
        var properties = new MaterialPropertyBlock(); properties.SetFloat("_Frame", 42); Body.SetPropertyBlock(properties);
        Renderer = Root.AddComponent<PlanetRenderer>(); Renderer.sprite = Root.AddComponent<PlanetSprite>();
        Renderer.glow = Child("Source glow").AddComponent<SpriteRenderer>();
        Renderer.glow.sprite = Sprite; Renderer.glow.sharedMaterial = Material; Renderer.glow.color = Color.clear;
        Renderer.glow.transform.localScale = Vector3.one * 1.6f;
        Renderer.ring = Child("Source ring").AddComponent<LineRenderer>();
        Renderer.ring.sharedMaterial = Material; Renderer.ring.useWorldSpace = false;
        Renderer.ring.positionCount = 32; Renderer.ring.loop = true; Renderer.ring.widthMultiplier = .07f;
        Renderer.ring.startColor = Renderer.ring.endColor = new Color(.2f, .3f, .9f, .4f);
        for (int i = 0; i < 32; i++)
        {
          float angle = i / 32f * Mathf.PI * 2;
          Renderer.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
        }
        Renderer.ring.transform.localScale = Vector3.zero;
        Renderer.tailParticles = Particles("Source tail", Color.white);
        Renderer.coreParticles = Particles("Source core", Color.clear);
      }

      private GameObject Child(string name)
      {
        var child = new GameObject(name); child.transform.SetParent(Root.transform, false); return child;
      }

      private ParticleSystem Particles(string name, Color color)
      {
        var system = Child(name).AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false; main.startLifetime = 1; main.startSpeed = 0; main.startSize = .12f; main.startColor = color;
        var shape = system.shape; shape.enabled = false;
        var emission = system.emission; emission.enabled = false;
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = Material;
        return system;
      }

      public void Dispose()
      {
        Object.DestroyImmediate(Root); Object.DestroyImmediate(Material); Object.DestroyImmediate(AlternateMaterial);
        Object.DestroyImmediate(Sprite); Object.DestroyImmediate(AlternateSprite); Object.DestroyImmediate(Texture);
      }
    }

    private sealed class SourceSnapshot
    {
      private readonly Color _body, _glow, _ring, _tail, _core;
      private readonly Vector3 _position;
      private readonly Vector3[] _ringPoints;
      internal SourceSnapshot(SyntheticPlanet source)
      {
        _body = source.Body.color; _glow = source.Renderer.glow.color; _ring = source.Renderer.ring.startColor;
        _tail = source.Renderer.tailParticles.main.startColor.color; _core = source.Renderer.coreParticles.main.startColor.color;
        _position = source.Root.transform.position;
        _ringPoints = new Vector3[source.Renderer.ring.positionCount]; source.Renderer.ring.GetPositions(_ringPoints);
      }
      internal void Check(SyntheticPlanet source)
      {
        var properties = new MaterialPropertyBlock(); source.Body.GetPropertyBlock(properties);
        Assert(source.Body.color == _body && source.Renderer.glow.color == _glow && source.Renderer.ring.startColor == _ring
          && source.Renderer.tailParticles.main.startColor.color == _tail && source.Renderer.coreParticles.main.startColor.color == _core,
          "preview edits leave all source component colors untouched");
        Assert(source.Body.sharedMaterial == source.Material && source.Body.sprite == source.Sprite
          && properties.GetFloat("_Frame") == 42 && source.Root.transform.position == _position && !source.Root.activeSelf,
          "preview retains source assets, property blocks, transforms and activation state");
        Assert(source.Renderer.tailParticles.particleCount == 0 && source.Renderer.coreParticles.particleCount == 0,
          "preview never emits or simulates the source particle systems");
        for (int i = 0; i < _ringPoints.Length; i++)
          Assert(source.Renderer.ring.GetPosition(i) == _ringPoints[i], "preview does not rewrite source ring positions");
      }
    }
  }
}
