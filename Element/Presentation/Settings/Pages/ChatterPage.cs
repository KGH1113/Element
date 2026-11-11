using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks → Chatter blocker: the switch, its threshold, keys it never blocks and what it has blocked.</summary>
internal sealed class ChatterPage : SettingsPage
{
  private readonly Func<long> _blockedChatter;
  private readonly Button _chatterSwitch;
  private readonly SettingsValueRow _chatterWindow;
  private readonly TextMeshProUGUI _chatterHint,
    _chatterBlocked,
    _chatterConflict;
  private TextMeshProUGUI _ignoredDescription,
    _ignoredKeys;
  private RectTransform _ignoredCard;
  private Button _ignoredChange,
    _ignoredClear;
  private long _shownBlocked = -1;

  // While on, each key pressed goes onto the ignored keys or comes off them.
  private bool _ignoredCapture;

  /// <param name="blockedChatter">The presses blocked since the game started; null shows 0.</param>
  public ChatterPage(ISettingsWindow window, Func<long> blockedChatter)
    : base(window, "Chatter blocker")
  {
    _blockedChatter = blockedChatter;
    ChatterBlockerOptions chatter = Settings.Gameplay.Chatter;
    _chatterSwitch = Ui.Switch(
      L("채터 블로커", "Chatter blocker"),
      L("튄 키 입력을 한 번으로 셉니다.", "Counts a bouncing key press only once."),
      Root,
      0,
      () => chatter.Enabled,
      value =>
      {
        chatter.Enabled = value;
        Window.Changed();
      }
    );
    _chatterWindow = new SettingsValueRow(
      Ui,
      Root,
      "Chatter threshold",
      L("채터 임계값", "Chatter threshold"),
      "ms",
      ChatterFilter.MinimumWindowMs,
      ChatterFilter.MaximumWindowMs,
      1,
      () => chatter.WindowMs,
      milliseconds =>
      {
        chatter.WindowMs = milliseconds;
        Window.Changed();
      }
    );
    _chatterHint = Ui.Text(
      L(
        "같은 키를 마지막으로 받은 입력에서 이 시간 안에 다시 누르면 무시해요. 이보다 빠르게 같은 키를 연타하면 입력이 빠지니, 그런 키는 아래에 넣어 주세요.",
        "A press of the same key within this time of its last accepted press is ignored. Faster repeats on one key are dropped too, so add such keys below."
      ),
      Root,
      0,
      204,
      936,
      44,
      13,
      SettingsUiFactory.Muted
    );
    _chatterHint.textWrappingMode = TextWrappingModes.Normal;
    CreateIgnoredKeysCard(chatter);
    _chatterBlocked = Ui.Text("", Root, 0, 256, 936, 24, 14, SettingsUiFactory.Muted);
    _chatterConflict = Ui.Text(
      L(
        "KeyboardChatterBlocker도 켜져 있어요. 입력이 두 번 걸러지지 않도록 한쪽만 사용하세요.",
        "KeyboardChatterBlocker is also running. Use only one of them so input is not filtered twice."
      ),
      Root,
      0,
      286,
      936,
      44,
      13,
      SettingsPalette.Error
    );
    _chatterConflict.textWrappingMode = TextWrappingModes.Normal;
    _chatterConflict.gameObject.SetActive(IsAssemblyLoaded("KeyboardChatterBlocker"));
    RefreshChatterCount();
    Ui.Bind(() =>
    {
      _chatterWindow.Sync(chatter.Enabled);
      _ignoredChange.interactable = chatter.Enabled;
      _ignoredClear.interactable = chatter.Enabled && !_ignoredCapture && chatter.IgnoredKeys.Count > 0;
    });
  }

  public override string Title => L("채터 블로커", "Chatter blocker");

  private void CreateIgnoredKeysCard(ChatterBlockerOptions chatter)
  {
    _ignoredCard = Ui.Panel("Ignored keys", Root, 0, 0, 936, 160, SettingsUiFactory.Surface).rectTransform;
    TextMeshProUGUI title = Ui.Text(L("막지 않을 키", "Keys never blocked"), _ignoredCard, 16, 16, 700, 28, 17);
    SettingsUiFactory.StretchWidth(title.rectTransform, 16, 16);
    _ignoredDescription = Ui.Text("", _ignoredCard, 16, 48, 904, 24, 14, SettingsUiFactory.Muted);
    _ignoredDescription.alignment = TextAlignmentOptions.TopLeft;
    _ignoredKeys = Ui.Text("", _ignoredCard, 16, 0, 904, 24, 15);
    _ignoredKeys.alignment = TextAlignmentOptions.TopLeft;
    _ignoredChange = Ui.Button("", _ignoredCard, 16, 0, 152, 40, ToggleIgnoredCapture, true);
    _ignoredClear = Ui.Button(
      L("비우기", "Clear"),
      _ignoredCard,
      176,
      0,
      120,
      40,
      () =>
      {
        chatter.IgnoredKeys.Clear();
        Window.Changed();
        RefreshIgnoredKeys();
      }
    );
    RefreshIgnoredKeys();
  }

  private void ToggleIgnoredCapture()
  {
    if (_ignoredCapture)
    {
      StopIgnoredCapture();
      return;
    }
    _ignoredCapture = true;
    // Space and Enter would press the focused button instead of being added.
    EventSystem.current?.SetSelectedGameObject(null);
    RefreshIgnoredKeys();
  }

  private void StopIgnoredCapture()
  {
    if (!_ignoredCapture)
      return;
    _ignoredCapture = false;
    RefreshIgnoredKeys();
  }

  public override void CancelCapture() => StopIgnoredCapture();

  // Every frame while capturing; Escape ends it.
  public override bool TickCapture()
  {
    if (!_ignoredCapture)
      return false;
    if (!UnityEngine.Input.anyKeyDown)
      return true;
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      StopIgnoredCapture();
      return true;
    }
    bool changed = false;
    int index = -1;
    while (KeyboardKeys.NextPressed(ref index, out KeyCode key))
      if (key != KeyCode.Escape && ChatterBlockerOptions.IsIgnorable(key))
      {
        Settings.Gameplay.Chatter.ToggleIgnored(key);
        changed = true;
      }
    if (!changed)
      return true;
    Window.Changed();
    RefreshIgnoredKeys();
    return true;
  }

  private void RefreshIgnoredKeys()
  {
    if (_ignoredCard == null)
      return;
    List<KeyCode> keys = Settings.Gameplay.Chatter.IgnoredKeys;
    _ignoredDescription.text = _ignoredCapture
      ? L(
        "누르는 키를 넣거나 뺍니다. 끝나면 완료를 누르거나 Esc를 누르세요.",
        "Each key you press is added, or removed if it's already here. Press Done or Esc when finished."
      )
      : L(
        "여기 넣은 키는 아무리 빠르게 연타해도 막지 않아요.",
        "Keys here are never blocked, however fast you repeat them."
      );
    var names = new List<string>(keys.Count);
    foreach (KeyCode key in keys)
      names.Add(ShortcutKeys.KeyLabel(key));
    _ignoredKeys.text = names.Count == 0 ? L("없음", "None") : string.Join(" · ", names);
    _ignoredKeys.color = names.Count == 0 ? SettingsUiFactory.Muted : SettingsUiFactory.Foreground;
    TextMeshProUGUI change = _ignoredChange.GetComponentInChildren<TextMeshProUGUI>(true);
    if (change != null)
      change.text = _ignoredCapture ? L("완료", "Done") : L("키 바꾸기", "Change keys");
    Ui.RefreshTheme();
    // The card is built before the rest of the page; the page lays it out then.
    if (_chatterConflict != null && Root.rect.width > 0)
      Layout(Root.rect.width, 0);
  }

  private float LayoutIgnoredKeys(float top, float width)
  {
    float inner = width - 32;
    float descriptionHeight = SettingsUiFactory.TextHeight(_ignoredDescription, inner, 20);
    SettingsUiFactory.Place(_ignoredDescription.rectTransform, 16, 48, inner, descriptionHeight);
    float y = 48 + descriptionHeight + 8;
    float keysHeight = SettingsUiFactory.TextHeight(_ignoredKeys, inner, 22);
    SettingsUiFactory.Place(_ignoredKeys.rectTransform, 16, y, inner, keysHeight);
    y += keysHeight + 12;
    SettingsUiFactory.Place((RectTransform)_ignoredChange.transform, 16, y, 152, 40);
    SettingsUiFactory.Place((RectTransform)_ignoredClear.transform, 176, y, 120, 40);
    float height = y + 40 + 16;
    SettingsUiFactory.Place(_ignoredCard, 0, top, width, height);
    return height;
  }

  public override void Layout(float width, float height)
  {
    float switchHeight = width < 520 ? 100 : 88;
    SettingsUiFactory.Place((RectTransform)_chatterSwitch.transform, 0, 0, width, switchHeight);
    float top = switchHeight + 12;
    _chatterWindow.Layout(0, top, width);
    top += SettingsValueRow.Height + 12;
    float hintHeight = width < 600 ? 64 : 44;
    SettingsUiFactory.Place(
      _chatterHint.rectTransform,
      0,
      top,
      width,
      SettingsUiFactory.TextHeight(_chatterHint, width, hintHeight)
    );
    top += SettingsUiFactory.TextHeight(_chatterHint, width, hintHeight) + 12;
    top += LayoutIgnoredKeys(top, width) + 16;
    SettingsUiFactory.Place(_chatterBlocked.rectTransform, 0, top, width, 24);
    top += 32;
    if (_chatterConflict.gameObject.activeSelf)
    {
      SettingsUiFactory.Place(_chatterConflict.rectTransform, 0, top, width, hintHeight);
      top += hintHeight;
    }
    Root.sizeDelta = new Vector2(width, top);
  }

  public override void Tick() => RefreshChatterCount();

  // Runs every frame while the page is open; text changes only when the count does.
  private void RefreshChatterCount()
  {
    long blocked = _blockedChatter?.Invoke() ?? 0;
    if (blocked == _shownBlocked)
      return;
    _shownBlocked = blocked;
    _chatterBlocked.text = L("이번 실행에서 막은 입력: ", "Blocked since ADOFAI started: ") + blocked;
  }

  private static bool IsAssemblyLoaded(string name)
  {
    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      if (string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
  }
}
