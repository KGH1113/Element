using Element.Presentation.Ui.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>
/// How a <see cref="SettingsSegmentedControl"/> looks and answers clicks: the
/// toggle group of pickers and editors, or the section tabs of a settings page.
/// </summary>
internal sealed class SettingsSegmentStyle
{
  /// <summary>shadcn's toggle group: the chosen segment raised in the accent surface on a light track.</summary>
  public static readonly SettingsSegmentStyle Toggle = new(
    "Segments",
    track: new Color(.16f, .16f, .16f),
    trackTakesClicks: true,
    inset: 3,
    idle: new Color(.16f, .16f, .16f, 0),
    idleHover: new Color(1, 1, 1, .06f),
    hoverLift: 0,
    pressAccent: true,
    disabled: new Color(0, 0, 0, 0),
    labelInset: 6,
    labelWraps: false,
    clicksRepeat: false
  );

  /// <summary>A settings page's sections: tabs on a dark track. Clicking the open tab runs it again.</summary>
  public static readonly SettingsSegmentStyle Tabs = new(
    "Sections",
    track: new Color(.085f, .085f, .085f),
    trackTakesClicks: false,
    inset: 4,
    idle: new Color(.085f, .085f, .085f),
    idleHover: null,
    hoverLift: .1f,
    pressAccent: false,
    disabled: Color.Lerp(UiPalette.Surface, UiPalette.Background, .45f),
    labelInset: 12,
    labelWraps: true,
    clicksRepeat: true
  );

  private readonly Color _idle,
    _disabled;
  private readonly Color? _idleHover;
  private readonly float _hoverLift;
  private readonly bool _pressAccent;

  private SettingsSegmentStyle(
    string name,
    Color track,
    bool trackTakesClicks,
    float inset,
    Color idle,
    Color? idleHover,
    float hoverLift,
    bool pressAccent,
    Color disabled,
    float labelInset,
    bool labelWraps,
    bool clicksRepeat
  )
  {
    Name = name;
    Track = track;
    TrackTakesClicks = trackTakesClicks;
    Inset = inset;
    _idle = idle;
    _idleHover = idleHover;
    _hoverLift = hoverLift;
    _pressAccent = pressAccent;
    _disabled = disabled;
    LabelInset = labelInset;
    LabelWraps = labelWraps;
    ClicksRepeat = clicksRepeat;
  }

  public string Name { get; }
  public Color Track { get; }
  public bool TrackTakesClicks { get; }

  /// <summary>The gap around and between the segments.</summary>
  public float Inset { get; }
  public float LabelInset { get; }
  public bool LabelWraps { get; }

  /// <summary>A click on the chosen segment reports it again.</summary>
  public bool ClicksRepeat { get; }

  /// <summary>A segment's colors, chosen or not, in the current accent.</summary>
  public ColorBlock Colors(ColorBlock colors, bool selected, SettingsUiFactory ui)
  {
    Color fill = selected ? ui.AccentSurface : _idle;
    colors.normalColor = fill;
    colors.selectedColor = fill;
    colors.highlightedColor =
      selected || _idleHover == null ? Color.Lerp(fill, Color.white, _hoverLift) : _idleHover.Value;
    colors.pressedColor = Color.Lerp(_pressAccent ? ui.AccentSurface : UiPalette.Surface, Color.black, .2f);
    colors.disabledColor = _disabled;
    colors.fadeDuration = ui.ReduceMotion() ? 0 : .1f;
    return colors;
  }
}
