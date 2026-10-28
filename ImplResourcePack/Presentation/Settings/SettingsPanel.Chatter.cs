using System;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private const int ChatterPage = 8;
  private readonly Func<long> _blockedChatter;
  private Button _chatterSwitch;
  private RectTransform _chatterRow;
  private Slider _chatterWindow;
  private TextMeshProUGUI _chatterWindowValue,
    _chatterHint,
    _chatterBlocked,
    _chatterConflict;
  private long _shownBlocked = -1;

  private void CreateChatterPage()
  {
    ChatterBlockerOptions chatter = _settings.Gameplay.Chatter;
    Transform page = _pages[ChatterPage].transform;
    _chatterSwitch = _ui.Switch(
      L("채터 블로커", "Chatter blocker"),
      L("튄 키 입력을 한 번으로 셉니다.", "Counts a bouncing key press only once."),
      page,
      0,
      () => chatter.Enabled,
      value =>
      {
        chatter.Enabled = value;
        Changed();
      }
    );
    _chatterRow = _ui.Panel("Chatter window", page, 0, 100, 936, 88, SettingsUiFactory.Surface).rectTransform;
    _ui.Text(L("무시할 간격", "Ignore window"), _chatterRow, 16, 10, 300, 24, 14, SettingsUiFactory.Muted);
    _chatterWindowValue = _ui.Text("", _chatterRow, 800, 10, 120, 24, 14);
    _chatterWindowValue.alignment = TextAlignmentOptions.Right;
    _chatterWindow = _ui.Slider(
      "Chatter window slider",
      _chatterRow,
      8,
      42,
      920,
      0,
      value =>
      {
        int milliseconds = ChatterFilter.ClampWindow(Mathf.RoundToInt(value));
        if (milliseconds == chatter.WindowMs)
          return;
        chatter.WindowMs = milliseconds;
        _chatterWindowValue.text = milliseconds + " ms";
        Changed();
      },
      1
    );
    _chatterWindow.minValue = ChatterFilter.MinimumWindowMs;
    _chatterWindow.maxValue = ChatterFilter.MaximumWindowMs;
    _chatterWindow.wholeNumbers = true;
    _chatterWindow.SetValueWithoutNotify(chatter.WindowMs);
    _chatterHint = _ui.Text(
      L(
        "한 번 눌렀는데 두 번 입력될 때만 켜세요. 간격이 길면 같은 키를 빠르게 연타할 때 입력이 빠질 수 있어요.",
        "Turn this on only when one press sometimes counts twice. A long window can drop fast repeated presses on the same key."
      ),
      page,
      0,
      204,
      936,
      44,
      13,
      SettingsUiFactory.Muted
    );
    _chatterHint.textWrappingMode = TextWrappingModes.Normal;
    _chatterBlocked = _ui.Text("", page, 0, 256, 936, 24, 14, SettingsUiFactory.Muted);
    _chatterConflict = _ui.Text(
      L(
        "KeyboardChatterBlocker도 켜져 있어요. 입력이 두 번 걸러지지 않도록 한쪽만 사용하세요.",
        "KeyboardChatterBlocker is also running. Use only one of them so input is not filtered twice."
      ),
      page,
      0,
      286,
      936,
      44,
      13,
      new Color(1, .66f, .53f)
    );
    _chatterConflict.textWrappingMode = TextWrappingModes.Normal;
    _chatterConflict.gameObject.SetActive(IsAssemblyLoaded("KeyboardChatterBlocker"));
    RefreshChatterCount();
    _ui.Bind(() =>
    {
      _chatterWindow.interactable = chatter.Enabled;
      _chatterWindowValue.text = chatter.WindowMs + " ms";
      _chatterWindowValue.color = chatter.Enabled ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted;
    });
  }

  private void LayoutChatterPage(float width)
  {
    float switchHeight = width < 520 ? 100 : 88;
    SettingsUiFactory.Place((RectTransform)_chatterSwitch.transform, 0, 0, width, switchHeight);
    float top = switchHeight + 12;
    SettingsUiFactory.Place(_chatterRow, 0, top, width, 88);
    SettingsUiFactory.Place(_chatterWindowValue.rectTransform, width - 136, 10, 120, 24);
    SettingsUiFactory.Place((RectTransform)_chatterWindow.transform, 8, 42, width - 16, 36);
    top += 100;
    float hintHeight = width < 600 ? 64 : 44;
    SettingsUiFactory.Place(_chatterHint.rectTransform, 0, top, width, hintHeight);
    top += hintHeight + 8;
    SettingsUiFactory.Place(_chatterBlocked.rectTransform, 0, top, width, 24);
    top += 32;
    if (_chatterConflict.gameObject.activeSelf)
    {
      SettingsUiFactory.Place(_chatterConflict.rectTransform, 0, top, width, hintHeight);
      top += hintHeight;
    }
    ((RectTransform)_pages[ChatterPage].transform).sizeDelta = new Vector2(width, top);
  }

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
