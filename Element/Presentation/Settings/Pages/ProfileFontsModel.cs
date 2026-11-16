using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Profiles, right after an import: the fonts the profile uses that this PC
/// lacks, each with a font list to pick one to use instead. Until then those
/// texts draw in the default font.
/// </summary>
internal sealed class ProfileFontsModel : ViewModel
{
  private readonly ElementSettings _settings;
  private readonly Func<OverlayFonts> _overlayFonts;
  private readonly Action _changed;

  // Fonts found in the imported profile while installed fonts are still listed.
  private List<OverlayFontUse> _checking;

  /// <summary>A row per missing font; empty puts the card away.</summary>
  public Row[] Rows = Array.Empty<Row>();

  public Computed<bool> Visible { get; }

  /// <summary>Puts the card away.</summary>
  public Command Done { get; }

  /// <summary>The open font list's per-frame work; set by the list while it is open.</summary>
  public Action PickerTick;

  /// <param name="overlayFonts">The overlay's fonts; null, or none yet, offers nothing.</param>
  /// <param name="changed">A replacement was picked: apply and save the settings.</param>
  public ProfileFontsModel(ElementSettings settings, Func<OverlayFonts> overlayFonts, Action changed)
  {
    _settings = settings;
    _overlayFonts = overlayFonts;
    _changed = changed;
    Visible = Computed(() => Rows.Length > 0);
    Done = Command(Clear);
  }

  /// <summary>The overlay's fonts, for the font list; null when there are none.</summary>
  public OverlayFonts Fonts => _overlayFonts?.Invoke();

  /// <summary>After an import: looks for fonts the new profile uses that this PC lacks.</summary>
  public void Offer()
  {
    Clear();
    OverlayFonts fonts = Fonts;
    if (fonts == null)
      return;
    List<OverlayFontUse> uses = OverlayFontUse.Find(_settings);
    if (uses.Count == 0)
      return;
    _checking = uses;
    fonts.Library?.RequestInstalled();
    Check();
  }

  /// <summary>Whether installed fonts are still being listed for the last import.</summary>
  public bool Checking => _checking != null;

  /// <summary>Every frame while Profiles is shown: settles the check once fonts are listed, and runs the open list.</summary>
  public void Tick()
  {
    if (_checking != null)
      Check();
    PickerTick?.Invoke();
  }

  // Settles once no font the profile uses is still loading.
  private void Check()
  {
    OverlayFonts fonts = Fonts;
    if (_checking == null || fonts == null)
      return;
    fonts.Tick();
    foreach (OverlayFontUse use in _checking)
      if (fonts.Status(use.Key) == OverlayFontStatus.Loading)
        return;
    var rows = new List<Row>();
    foreach (OverlayFontUse use in _checking)
      if (fonts.Status(use.Key) == OverlayFontStatus.Missing)
        rows.Add(new Row(this, use));
    _checking = null;
    Rows = rows.ToArray();
    Runtime.Probe();
  }

  /// <summary>Puts the card away, with any rows and the font list.</summary>
  public void Clear()
  {
    _checking = null;
    foreach (Row row in Rows)
      row.Dispose();
    Rows = Array.Empty<Row>();
  }

  /// <summary>Closes an open font list; false when none was open.</summary>
  public bool DismissPicker()
  {
    foreach (Row row in Rows)
      if (row.PickerOpen)
      {
        row.PickerOpen = false;
        Runtime.Probe();
        return true;
      }
    return false;
  }

  // A pick shows at once; the list stays open to try another.
  private void Replace(Row row, string key)
  {
    key = OverlayFontKey.Normalize(key);
    row.Use.Set(_settings, key);
    row.Show(key);
    _changed();
    Runtime.Probe();
  }

  protected override void OnDisposed() => Clear();

  /// <summary>A missing font: the texts that use it, and the font they use now.</summary>
  internal sealed class Row : ViewModel
  {
    private readonly ProfileFontsModel _owner;
    public readonly OverlayFontUse Use;

    /// <summary>The font these texts use now: the missing one until another is picked.</summary>
    public string Current;
    public readonly string Name;
    public string State = "";
    public bool Replaced;

    /// <summary>This row's font list is open.</summary>
    public bool PickerOpen;

    public Command Choose { get; }

    public Row(ProfileFontsModel owner, OverlayFontUse use)
    {
      _owner = owner;
      Use = use;
      Name = OverlayFonts.DisplayName(use.Key);
      Show(use.Key);
      Choose = Command(() => PickerOpen = true);
    }

    /// <summary>The font list as it opens: the font these texts use now marked, a pick replacing it.</summary>
    public FontListRequest Picker() =>
      new()
      {
        Fonts = _owner.Fonts,
        Title = L("대신 쓸 폰트", "Font to use instead"),
        Current = Current,
        DefaultName = Use.IsDefault ? L("메이플스토리", "MapleStory") : L("기본 폰트", "Default font"),
        DefaultDetail = Use.IsDefault ? L("기본 제공", "Built-in") : L("오버레이 기본값", "Overlay default"),
        ReduceMotion = _owner._settings.ReduceUiMotion,
        Picked = Pick,
        Ticks = tick => _owner.PickerTick = tick,
      };

    /// <summary>A font picked in the list.</summary>
    public void Pick(string key) => _owner.Replace(this, key);

    public void Show(string current)
    {
      Current = current;
      Replaced = !OverlayFontKey.Same(current, Use.Key);
      State =
        !Replaced ? L("기본 폰트로 보이는 중", "Showing in the default font")
        : current.Length == 0 ? L("기본 폰트로 바꿨어요", "Changed to the default font")
        : L(OverlayFonts.DisplayName(current) + "로 바꿨어요", "Changed to " + OverlayFonts.DisplayName(current));
    }
  }
}
