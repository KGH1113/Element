using Element.Application.Updates;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Notices;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>General: the profiles link, the settings shortcut, reduced motion, ElementNote and automatic updates.</summary>
internal sealed class GeneralPage : SettingsPage
{
  private readonly IModUpdates _updates;
  private readonly Button _profileLink;
  private readonly RectTransform _shortcutSection;
  private readonly SettingsShortcutField _settingsShortcut;
  private readonly Button _motionRow,
    _dmNoteRow,
    _updateRow;
  private readonly TextMeshProUGUI _updateDescription;

  /// <param name="profiles">Linked at the top; null leaves the link out.</param>
  /// <param name="updates">Automatic updates, at the end; null leaves the row out.</param>
  public GeneralPage(ISettingsWindow window, ProfilesPage profiles, IModUpdates updates)
    : base(window, "General")
  {
    _updates = updates;
    if (profiles != null)
    {
      _profileLink = profiles.CreateLink(Root);
      Adopt(profiles, _profileLink);
    }
    _shortcutSection = Ui.Panel("Settings shortcut", Root, 0, 0, 936, 124, UiPalette.Surface).rectTransform;
    TextMeshProUGUI shortcutTitle = Ui.Text(
      L("설정 단축키", "Settings shortcut"),
      _shortcutSection,
      16,
      16,
      700,
      28,
      17
    );
    UiFactory.StretchWidth(shortcutTitle.rectTransform, 16, 16);
    ElementSettings settings = Settings;
    _settingsShortcut = new SettingsShortcutField(
      Ui,
      _shortcutSection,
      () => settings.SettingsShortcutKey,
      () => settings.SettingsShortcutModifier,
      AssignSettingsShortcut,
      "Reset shortcut",
      UiIconKind.Reset,
      () =>
      {
        settings.SettingsShortcutKey = ElementSettings.DefaultSettingsShortcutKey;
        settings.SettingsShortcutModifier = ElementSettings.DefaultSettingsShortcutModifier;
        _settingsShortcut.Cancel();
        Window.Changed();
        UpdateShortcutLabel();
      },
      L("왼쪽 Ctrl + F9", "Left Ctrl + F9"),
      Window.FitWindow
    );
    _motionRow = Ui.Switch(
      L("움직임 줄이기", "Reduce motion"),
      L("설정 전환과 스위치 움직임을 즉시 적용합니다.", "Use immediate menu and switch transitions."),
      Root,
      136,
      () => settings.ReduceUiMotion,
      v =>
      {
        settings.ReduceUiMotion = v;
        Window.Changed();
      }
    );
    _dmNoteRow = Ui.Switch(
      L("ElementNote 자동 열기/닫기", "Auto open/close ElementNote"),
      L(
        "게임을 켜면 ElementNote를 열고, 게임을 끄면 함께 닫습니다.",
        "Open ElementNote with the game and close it when the game quits."
      ),
      Root,
      236,
      () => settings.AutoOpenCloseDmNote,
      v =>
      {
        settings.AutoOpenCloseDmNote = v;
        Window.Changed();
      }
    );
    if (updates != null)
      _updateRow = Ui.Switch(
        L("자동 업데이트", "Automatic updates"),
        UpdateStatusText.For(updates),
        Root,
        336,
        () => updates.AutomaticUpdates,
        v =>
        {
          updates.AutomaticUpdates = v;
          RefreshUpdateRow();
        },
        out _updateDescription
      );
  }

  public override string Title => L("일반", "General");

  public override void Shown() => RefreshUpdateRow();

  public override bool TickCapture()
  {
    if (!_settingsShortcut.Capturing)
      return false;
    _settingsShortcut.Tick();
    return true;
  }

  public override void CancelCapture() => _settingsShortcut.Cancel();

  private string AssignSettingsShortcut(KeyCode key, KeyCode modifier)
  {
    if (Settings.ShortcutTaken(key, modifier, null, out EffectPreset owner))
      return ShortcutTakenText.For(owner);
    Settings.SettingsShortcutKey = key;
    Settings.SettingsShortcutModifier = modifier;
    Window.Changed();
    return null;
  }

  private void RefreshUpdateRow()
  {
    if (_updateDescription != null)
      _updateDescription.text = UpdateStatusText.For(_updates);
  }

  private void UpdateShortcutLabel()
  {
    _settingsShortcut.Refresh();
    LayoutShortcutControls();
  }

  public override void Layout(float width, float height)
  {
    UiFactory.Place(_shortcutSection, 0, 0, width, 124);
    UpdateShortcutLabel();
  }

  private void LayoutShortcutControls()
  {
    float contentWidth = _shortcutSection.rect.width;
    float top = 0;
    // The profiles link leads the page.
    if (_profileLink != null)
    {
      UiFactory.Place((RectTransform)_profileLink.transform, 0, 0, contentWidth, 80);
      top = 92;
    }
    float fieldBottom = _settingsShortcut.Layout(60, contentWidth);
    float shortcutHeight = fieldBottom + (_settingsShortcut.Capturing ? 16 : 20);
    UiFactory.Place(_shortcutSection, 0, top, contentWidth, shortcutHeight);
    float rowHeight = contentWidth < 520 ? 116 : 88;
    float motionTop = top + shortcutHeight + 12;
    UiFactory.Place((RectTransform)_motionRow.transform, 0, motionTop, contentWidth, rowHeight);
    float dmNoteTop = motionTop + rowHeight + 12;
    UiFactory.Place((RectTransform)_dmNoteRow.transform, 0, dmNoteTop, contentWidth, rowHeight);
    float bottom = dmNoteTop + rowHeight;
    if (_updateRow != null)
    {
      UiFactory.Place((RectTransform)_updateRow.transform, 0, bottom + 12, contentWidth, rowHeight);
      bottom += 12 + rowHeight;
    }
    Root.sizeDelta = new Vector2(contentWidth, bottom);
  }
}
