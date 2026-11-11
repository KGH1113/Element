using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Profiles, right after an import: the fonts the profile uses that this PC
/// lacks, each with a font list to pick one to use instead. Until then those
/// texts draw in the default font.
/// </summary>
internal sealed class ProfileFontReplacements : IDisposable
{
  private const float FontRowHeight = 56;

  private readonly ISettingsWindow _window;
  private readonly Func<OverlayFonts> _overlayFonts;
  private readonly Action _resized;
  private readonly List<MissingFont> _missingFonts = new();
  private readonly RectTransform _fontCard;
  private readonly TextMeshProUGUI _fontCardDescription;
  private readonly Button _fontCardDone;

  // Fonts found in the imported profile while installed fonts are still listed.
  private List<OverlayFontUse> _checkingFonts;
  private SettingsUiFactory _fontRowsUi;
  private SettingsUiFactory _fontPopoverUi;
  private OverlayFontPopover _fontPopover;
  private RectTransform _fontPopoverAnchor;
  private int _fontPickerRow = -1;

  private sealed class MissingFont
  {
    public OverlayFontUse Use;

    // The font these texts use now: the missing one until another is picked.
    public string Current;
    public RectTransform Row;
    public TextMeshProUGUI Name;
    public TextMeshProUGUI State;
    public Button Pick;
  }

  /// <param name="overlayFonts">The overlay's fonts; null, or none yet, offers nothing.</param>
  /// <param name="resized">The card's height changed; the page lays out again.</param>
  public ProfileFontReplacements(
    ISettingsWindow window,
    Transform page,
    Func<OverlayFonts> overlayFonts,
    Action resized
  )
  {
    _window = window;
    _overlayFonts = overlayFonts;
    _resized = resized;
    SettingsUiFactory ui = window.Ui;
    _fontCard = ui.Panel("Missing fonts", page, 0, 0, 936, 160, SettingsUiFactory.Surface).rectTransform;
    TextMeshProUGUI title = ui.Text(
      L("이 PC에 없는 폰트가 있어요", "Some fonts aren't on this PC"),
      _fontCard,
      16,
      16,
      700,
      28,
      17
    );
    SettingsUiFactory.StretchWidth(title.rectTransform, 16, 16);
    _fontCardDescription = ui.Text(
      L(
        "가져온 프로필이 쓰는 폰트예요. 대신 쓸 폰트를 고르면 바로 바뀝니다. 그대로 두면 폰트를 설치하거나 추가할 때까지 기본 폰트로 보여요.",
        "The profile you imported uses these fonts. Pick one to use instead and it changes right away. Left as is, they show in the default font until you install or add them."
      ),
      _fontCard,
      16,
      48,
      904,
      24,
      14,
      SettingsUiFactory.Muted
    );
    _fontCardDescription.alignment = TextAlignmentOptions.TopLeft;
    _fontCardDone = ui.Button(L("완료", "Done"), _fontCard, 16, 0, 120, 40, Clear);
    _fontCard.gameObject.SetActive(false);
  }

  public bool Visible => _fontCard.gameObject.activeSelf;

  public bool PopoverVisible => _fontPopover?.Visible == true;

  /// <summary>After an import: looks for fonts the new profile uses that this PC lacks.</summary>
  public void Offer()
  {
    Clear();
    OverlayFonts fonts = _overlayFonts?.Invoke();
    if (fonts == null)
      return;
    List<OverlayFontUse> uses = OverlayFontUse.Find(_window.Settings);
    if (uses.Count == 0)
      return;
    _checkingFonts = uses;
    fonts.Library?.RequestInstalled();
    CheckImportedFonts();
  }

  // Every frame while installed fonts are still being listed; settles once.
  private void CheckImportedFonts()
  {
    OverlayFonts fonts = _overlayFonts?.Invoke();
    if (_checkingFonts == null || fonts == null)
      return;
    fonts.Tick();
    foreach (OverlayFontUse use in _checkingFonts)
      if (fonts.Status(use.Key) == OverlayFontStatus.Loading)
        return;
    foreach (OverlayFontUse use in _checkingFonts)
      if (fonts.Status(use.Key) == OverlayFontStatus.Missing)
        _missingFonts.Add(new MissingFont { Use = use, Current = use.Key });
    _checkingFonts = null;
    if (_missingFonts.Count > 0)
      BuildFontRows();
  }

  private void BuildFontRows()
  {
    _fontRowsUi?.Dispose();
    _fontRowsUi = new SettingsUiFactory(_window.Font) { ReduceMotion = () => _window.Settings.ReduceUiMotion };
    for (int i = 0; i < _missingFonts.Count; i++)
    {
      int index = i;
      MissingFont font = _missingFonts[i];
      font.Row = _fontRowsUi.Rect("Missing font", _fontCard, 16, 0, 904, FontRowHeight);
      font.Name = _fontRowsUi.Text("", font.Row, 0, 4, 600, 24, 16);
      font.Name.textWrappingMode = TextWrappingModes.NoWrap;
      font.Name.overflowMode = TextOverflowModes.Ellipsis;
      font.State = _fontRowsUi.Text("", font.Row, 0, 28, 600, 20, 13, SettingsUiFactory.Muted);
      font.State.textWrappingMode = TextWrappingModes.NoWrap;
      font.State.overflowMode = TextOverflowModes.Ellipsis;
      font.Pick = _fontRowsUi.Button(
        L("폰트 고르기", "Choose font"),
        font.Row,
        0,
        8,
        140,
        40,
        () => OpenFontReplacement(index),
        true
      );
    }
    _fontCard.gameObject.SetActive(true);
    RefreshFontRows();
  }

  private void RefreshFontRows()
  {
    foreach (MissingFont font in _missingFonts)
    {
      bool replaced = !OverlayFontKey.Same(font.Current, font.Use.Key);
      font.Name.text = OverlayFonts.DisplayName(font.Use.Key);
      font.State.text =
        !replaced ? L("기본 폰트로 보이는 중", "Showing in the default font")
        : font.Current.Length == 0 ? L("기본 폰트로 바꿨어요", "Changed to the default font")
        : L(
          OverlayFonts.DisplayName(font.Current) + "로 바꿨어요",
          "Changed to " + OverlayFonts.DisplayName(font.Current)
        );
      font.State.color = replaced ? _window.Ui.SoftAccent : SettingsUiFactory.Muted;
    }
    _window.Ui.RefreshTheme();
    _resized();
  }

  private void OpenFontReplacement(int index)
  {
    OverlayFonts fonts = _overlayFonts?.Invoke();
    if (fonts == null || index < 0 || index >= _missingFonts.Count)
      return;
    if (_fontPopover == null)
    {
      _fontPopoverUi = new SettingsUiFactory(_window.Font) { ReduceMotion = () => _window.Settings.ReduceUiMotion };
      _fontPopover = new OverlayFontPopover(
        _fontPopoverUi,
        _window.Frame,
        fonts,
        _window.Settings.ReduceUiMotion,
        PickReplacement
      );
      _fontPopover.Hidden = () =>
      {
        _fontPickerRow = -1;
        _window.Shade.Hide();
      };
    }
    MissingFont font = _missingFonts[index];
    _fontPickerRow = index;
    _fontPopoverAnchor = (RectTransform)font.Pick.transform;
    _window.Shade.Show();
    bool isDefault = font.Use.IsDefault;
    _fontPopover.Show(
      L("대신 쓸 폰트", "Font to use instead"),
      font.Current,
      isDefault ? L("메이플스토리", "MapleStory") : L("기본 폰트", "Default font"),
      isDefault ? L("기본 제공", "Built-in") : L("오버레이 기본값", "Overlay default")
    );
    LayoutFontPopover();
  }

  // A pick shows at once; the list stays open to try another.
  private void PickReplacement(string key)
  {
    if (_fontPickerRow < 0 || _fontPickerRow >= _missingFonts.Count)
      return;
    MissingFont font = _missingFonts[_fontPickerRow];
    key = OverlayFontKey.Normalize(key);
    font.Use.Set(_window.Settings, key);
    font.Current = key;
    _window.Changed();
    _window.Save();
    RefreshFontRows();
  }

  /// <summary>Puts the card away, with any rows and the font list.</summary>
  public void Clear()
  {
    _checkingFonts = null;
    _missingFonts.Clear();
    if (_fontPopover?.Visible == true)
      _fontPopover.Hide();
    _fontRowsUi?.Dispose();
    _fontRowsUi = null;
    for (int i = _fontCard.childCount - 1; i >= 0; i--)
    {
      GameObject child = _fontCard.GetChild(i).gameObject;
      if (child.name != "Missing font")
        continue;
      child.SetActive(false);
      SettingsUiFactory.Destroy(child);
    }
    _fontCard.gameObject.SetActive(false);
    _resized();
  }

  public bool DismissPopover()
  {
    if (_fontPopover?.Visible != true)
      return false;
    _fontPopover.Hide();
    return true;
  }

  public void HidePopover()
  {
    if (_fontPopover?.Visible == true)
      _fontPopover.Hide();
  }

  /// <summary>Every frame while Profiles is shown.</summary>
  public void Tick()
  {
    if (_checkingFonts != null)
      CheckImportedFonts();
    if (_fontPopover?.Visible == true)
      _fontPopover.Tick();
  }

  /// <summary>Places the card <paramref name="top"/> down the page; returns its height.</summary>
  public float Layout(float top, float width)
  {
    float inner = width - 32;
    float descriptionHeight = SettingsUiFactory.TextHeight(_fontCardDescription, inner, 20);
    SettingsUiFactory.Place(_fontCardDescription.rectTransform, 16, 48, inner, descriptionHeight);
    float y = 48 + descriptionHeight + 12;
    bool narrow = width < 480;
    foreach (MissingFont font in _missingFonts)
    {
      float rowHeight = narrow ? FontRowHeight + 48 : FontRowHeight;
      SettingsUiFactory.Place(font.Row, 16, y, inner, rowHeight);
      float textWidth = narrow ? inner : inner - 140 - 12;
      SettingsUiFactory.Place(font.Name.rectTransform, 0, 4, textWidth, 24);
      SettingsUiFactory.Place(font.State.rectTransform, 0, 28, textWidth, 20);
      SettingsUiFactory.Place((RectTransform)font.Pick.transform, narrow ? 0 : inner - 140, narrow ? 56 : 8, 140, 40);
      y += rowHeight;
    }
    y += 8;
    SettingsUiFactory.Place((RectTransform)_fontCardDone.transform, 16, y, 120, 40);
    float height = y + 40 + 16;
    SettingsUiFactory.Place(_fontCard, 0, top, width, height);
    if (_fontPopover?.Visible == true)
      LayoutFontPopover();
    return height;
  }

  // Below the row's button, kept inside the window.
  private void LayoutFontPopover()
  {
    if (_fontPopover?.Visible != true || _fontPopoverAnchor == null)
      return;
    _window.Shade.Layout();
    RectTransform window = _window.Frame;
    float width = Mathf.Min(OverlayFontPopover.PreferredWidth, window.rect.width - 20);
    float height = Mathf.Min(OverlayFontPopover.PreferredHeight, window.rect.height - 20);
    Vector3 local = window.InverseTransformPoint(_fontPopoverAnchor.position);
    float x = Mathf.Clamp(
      local.x - window.rect.xMin + _fontPopoverAnchor.rect.width - width,
      10,
      window.rect.width - width - 10
    );
    float y = Mathf.Clamp(
      window.rect.yMax - local.y + _fontPopoverAnchor.rect.height + 8,
      10,
      window.rect.height - height - 10
    );
    _fontPopover.Place(x, y, width, height);
    _fontPopover.Root.SetAsLastSibling();
  }

  public void Dispose()
  {
    _fontRowsUi?.Dispose();
    _fontPopover?.Dispose();
    _fontPopoverUi?.Dispose();
  }
}
