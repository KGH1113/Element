// Only game/environment boundaries are stubbed; UI and renderer sources are copied verbatim.
using System;
using System.Collections.Generic;
using UnityEngine;
public static class RDString { public static SystemLanguage language = SystemLanguage.Korean; }
namespace Element.Presentation.Settings { internal static class UIInteractionState { public static void RestoreCursor() { } public static void SetTextEditing(bool value) { } } }
public static class ADOBase
{
  public class Controller { public bool paused; }
  public class Conductor { public bool isGameWorld; }
  public static Controller controller;
  public static Conductor conductor;
  public static RDConstants gc;
}
public sealed class RDConstants
{
  public GameObject prefab_colorsScreen;
  public Texture tex_planetRed, tex_planetBlue, tex_planetWhite;
}
public sealed class scrController
{
  public static scrController instance;
  public scrPlanet planetRed, planetBlue, planetGreen;
}
public sealed class scrPlanet { public PlanetRenderer planetRenderer; }
public sealed class PlanetRenderer : MonoBehaviour
{
  public PlanetSprite sprite;
  public SpriteRenderer glow;
  public LineRenderer ring;
  public ParticleSystem tailParticles, coreParticles;
  public GameObject planetGold, planetOverseer;
}
public sealed class PlanetSprite : MonoBehaviour { }
public enum PlanetColorPreset { Custom, DefaultRed, DefaultBlue, Gold, Overseer, Rainbow, TransBlue, TransPink, NBYellow, NBPurple }
public struct PlanetColor
{
  public PlanetColorPreset preset;
  private Color _color;
  public PlanetColor(Color color) { preset = PlanetColorPreset.Custom; _color = color; }
  public Color ToRealColor() => _color;
}
public static class Persistence
{
  public static PlanetColor Fire = new PlanetColor(Color.red), Ice = new PlanetColor(Color.blue);
  public static PlanetColor GetPlayerColor(bool fire) => fire ? Fire : Ice;
}
public static class FixtureColorExtensions
{
  public static Color WithAlpha(this Color color, float alpha) { color.a = alpha; return color; }
}
namespace Element.Infrastructure.Game.Tweaks
{
  // Snapshot injection models the game-owned appearance boundary, not the
  // production appearance controller's mutation/restoration implementation.
  internal sealed class PlanetAppearanceController
  {
    internal readonly Dictionary<PlanetRenderer, PlanetState> States = new Dictionary<PlanetRenderer, PlanetState>();
    internal PlanetState GetPreviewState(PlanetRenderer renderer) => States.TryGetValue(renderer, out PlanetState state) ? state : null;
    internal sealed class PlanetState
    {
      internal Texture SourceTexture;
      internal Color BodySource, Glow, RingSource, TailSource, CoreSource;
    }
  }
}
namespace Element.Infrastructure.Game.DmNote
{
  // Only the native transport is replaced. The production managed target owns
  // anchor selection, scaling, visibility fitting, staging and persistence.
  internal sealed class GameWebView : INativeViewerLayoutHost
  {
    public bool Connected = true;
    public Vector2 HostSizePoints;
    public Vector2 ReferenceSize;
    public Rect LegacyBoundsPoints;
    public float LegacyEffectiveScale = 1;
    public Rect ContentBoundsPoints;
    public float LastEffectiveScale;
    public int LegacyCalls;
    public int BoundsCalls;
    public bool LastCustomized;
    public Vector2 LastLegacyCenter;
    public float LastLegacyScale;
    public ulong GeometryRevision { get; private set; }

    public void ChangeHost(Vector2 points)
    {
      HostSizePoints = points;
      GeometryRevision++;
    }

    public void ChangeReference(Vector2 size)
    {
      ReferenceSize = size;
      GeometryRevision++;
    }

    public bool HasSurface(string kind) => Connected;
    public void Select(string kind, bool selected) { }

    public void Layout(string kind, bool customized, float centerX, float centerY, float scale)
    {
      if (!Connected) return;
      LegacyCalls++;
      LastCustomized = customized;
      LastLegacyCenter = new Vector2(centerX, centerY);
      LastLegacyScale = scale;
      // Treat this as an observation supplied by AppKit, independent of the
      // requested legacy values. This fake does not reimplement its old fit.
      ContentBoundsPoints = LegacyBoundsPoints;
      LastEffectiveScale = LegacyEffectiveScale;
    }

    public void LayoutBounds(string kind, Rect normalized, float effectiveScale)
    {
      if (!Connected) return;
      BoundsCalls++;
      ContentBoundsPoints = new Rect(normalized.x * HostSizePoints.x, normalized.y * HostSizePoints.y,
        normalized.width * HostSizePoints.x, normalized.height * HostSizePoints.y);
      LastEffectiveScale = effectiveScale;
    }

    public bool TryGetReferenceSize(string kind, out Vector2 size)
    {
      size = ReferenceSize;
      return Connected && size.x > 0 && size.y > 0;
    }

    // This fake has no initial fit, so its content and reference sizes match.
    public bool TryGetContentSize(string kind, out Vector2 size) => TryGetReferenceSize(kind, out size);

    public bool TryGetViewSize(out Vector2 size)
    {
      size = HostSizePoints;
      return size.x > 0 && size.y > 0;
    }

    public bool TryGetBounds(string kind, out Rect normalized, out float effectiveScale)
    {
      effectiveScale = LastEffectiveScale;
      normalized = default;
      if (!Connected || HostSizePoints.x <= 0 || HostSizePoints.y <= 0) return false;
      normalized = new Rect(ContentBoundsPoints.x / HostSizePoints.x, ContentBoundsPoints.y / HostSizePoints.y,
        ContentBoundsPoints.width / HostSizePoints.x, ContentBoundsPoints.height / HostSizePoints.y);
      return normalized.width > 0 && normalized.height > 0;
    }

    public bool TryGetSelectionBounds(string kind, out Rect normalized) => TryGetBounds(kind, out normalized, out _);
    public bool TryGetPointer(out Vector2 normalized) { normalized = default; return false; }
  }
}
public sealed class ColourSchemeHitMargin { public Color colourXPerfect = new Color(.376f, 1f, .306f); }
public static class RDC
{
  public static object data;
  public static ColourSchemeHitMargin hitMarginColoursBySettings;
}
namespace UnityModManagerNet
{
  public static class UnityModManager
  {
    public class ModEntry { }
    public class ModSettings
    {
      public virtual void Save(ModEntry entry) { }
      protected static void Save<T>(T settings, ModEntry entry) { }
    }
    public class UI { public static UI Instance; public void ToggleWindow(bool visible) { } }
  }
}
