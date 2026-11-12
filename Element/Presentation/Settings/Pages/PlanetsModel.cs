using System;
using Element.Configuration.Gameplay;
using Element.Presentation.Settings.Window;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Tweaks → Planets: each planet's body, trail and ring colors, with the
/// game's own drawing of the planet. The planet is drawn only while the page
/// shows: when it opens, when an appearance changes, and every tenth of a
/// second for the map's colors, which can change while it is open.
/// </summary>
internal sealed class PlanetsModel : SettingsPageModel
{
  private const float PollSeconds = .1f;

  private readonly Func<int, PlanetAppearanceProfile, Texture> _render;
  private Look _drawn;
  private float _nextPoll;
  private bool _open;

  /// <summary>The planet whose tab is picked: 0 fire, 1 ice, 2 the third planet.</summary>
  public int Planet;

  /// <summary>The page's width: the planet sits beside its parts, or above them when the window is narrow.</summary>
  public float Width = 936;

  public readonly PlanetModel Fire;
  public readonly PlanetModel Ice;
  public readonly PlanetModel Third;

  /// <summary>The planet whose tab is picked: what the page's switch and part rows edit.</summary>
  public Computed<PlanetModel> Picked { get; }

  /// <summary>The picked planet as the game draws it; null when the preview is unavailable.</summary>
  public Texture Preview;

  /// <summary>What the preview shows, under it.</summary>
  public Computed<string> PreviewCaption { get; }

  public PlanetsModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("행성", "Planets"))
  {
    _render = window.Features.RenderPlanetPreview;
    PlanetAppearanceOptions planets = Settings.Gameplay.Planets;
    Fire = Own(new PlanetModel(0, planets.Red));
    Ice = Own(new PlanetModel(1, planets.Blue));
    Third = Own(new PlanetModel(2, planets.Green));
    Picked = Computed(() =>
      Planet == 0 ? Fire
      : Planet == 1 ? Ice
      : Third
    );
    PreviewCaption = Computed(() =>
      Preview == null ? L("공 미리보기를 불러올 수 없습니다.", "Planet preview is unavailable.")
      : !Settings.Gameplay.Planets.Enabled || !Picked.Value.Profile.Enabled
        ? L("미리보기 · 게임에는 적용 안 됨", "Preview · not applied")
      : L("색상 미리보기", "Color preview")
    );
  }

  public override void Opened() => _open = true;

  public override void Shown() => RefreshPreview();

  /// <summary>Draws the planet again when its appearance changed, and every tenth of a second for the map's colors.</summary>
  public override void Tick()
  {
    if (_drawn.Equals(LookNow()) && Time.unscaledTime < _nextPoll)
      return;
    RefreshPreview();
  }

  /// <summary>
  /// Draws the picked planet now, while the page shows in an open window.
  /// The renderer draws again only when the planet's look changed.
  /// </summary>
  public void RefreshPreview()
  {
    if (!_open || !ReferenceEquals(Window.Current, this))
      return;
    _nextPoll = Time.unscaledTime + PollSeconds;
    _drawn = LookNow();
    PlanetModel planet = PlanetAt(Planet);
    Texture texture = _render?.Invoke(planet.Index, planet.Profile);
    if (ReferenceEquals(texture, Preview))
      return;
    Preview = texture;
    Runtime.Probe();
  }

  /// <summary>The window closed: the preview's texture goes with it, and the page draws again when it next shows.</summary>
  public override void Closed()
  {
    _open = false;
    _drawn = default;
    if (Preview is null)
      return;
    Preview = null;
    Runtime.Probe();
  }

  private PlanetModel PlanetAt(int index) =>
    index == 0 ? Fire
    : index == 1 ? Ice
    : Third;

  private Look LookNow() => new(Planet, Settings.Gameplay.Planets.Enabled, PlanetAt(Planet).Profile);

  /// <summary>What the preview depends on that the page can see: the picked planet and its colors.</summary>
  private readonly struct Look : IEquatable<Look>
  {
    private readonly int _planet;
    private readonly bool _on,
      _enabled;
    private readonly Part _body,
      _tail,
      _ring;

    public Look(int planet, bool on, PlanetAppearanceProfile profile)
    {
      _planet = planet + 1;
      _on = on;
      _enabled = profile.Enabled;
      _body = new Part(profile.Body);
      _tail = new Part(profile.Tail);
      _ring = new Part(profile.Ring);
    }

    public bool Equals(Look other) =>
      _planet == other._planet
      && _on == other._on
      && _enabled == other._enabled
      && _body.Equals(other._body)
      && _tail.Equals(other._tail)
      && _ring.Equals(other._ring);

    public override bool Equals(object obj) => obj is Look other && Equals(other);

    public override int GetHashCode() => _planet;
  }

  private readonly struct Part : IEquatable<Part>
  {
    private readonly bool _override;
    private readonly string _hex;
    private readonly float _opacity;

    public Part(PlanetComponentAppearance appearance)
    {
      _override = appearance.OverrideColor;
      _hex = appearance.ColorHex;
      _opacity = appearance.Opacity;
    }

    // The same string object, or equal text: either way the same color.
    public bool Equals(Part other) =>
      _override == other._override && _opacity == other._opacity && string.Equals(_hex, other._hex);

    public override bool Equals(object obj) => obj is Part other && Equals(other);

    public override int GetHashCode() => _opacity.GetHashCode();
  }
}
