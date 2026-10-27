using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Presentation.Settings;
using UnityEngine;

namespace ImplResourcePack.Presentation.Layout;

/// <summary>
/// What the editor's offline key viewer sample tells the player. Each message
/// says what to do next, in player terms (no IPC, WebKit or protocol words).
/// </summary>
internal static class NativeViewerStatusText
{
  public static string Get(NativeViewerStatus status) =>
    status switch
    {
      NativeViewerStatus.WaitingForApp => SettingsLocale.L(
        "ImplDmNote를 실행하면 키뷰어가 여기에 표시돼요.",
        "Open ImplDmNote to show the key viewer here."
      ),
      NativeViewerStatus.Connecting => SettingsLocale.L(
        "ImplDmNote에서 키뷰어를 불러오고 있어요.",
        "Loading the key viewer from ImplDmNote…"
      ),
      NativeViewerStatus.ObsModeOff => SettingsLocale.L(
        "ImplDmNote에서 OBS 모드를 켜면 키뷰어가 표시돼요.",
        "Turn on OBS mode in ImplDmNote to show the key viewer."
      ),
      NativeViewerStatus.HiddenInApp => SettingsLocale.L(
        "ImplDmNote에서 '얼불춤 안에 표시'를 켜면 키뷰어가 표시돼요.",
        "Turn on Show inside ADOFAI in ImplDmNote to show the key viewer."
      ),
      NativeViewerStatus.NoViewerTab => SettingsLocale.L(
        "ImplDmNote에서 이 키뷰어의 탭을 선택하면 여기에 표시돼요.",
        "Select a tab for this key viewer in ImplDmNote to show it here."
      ),
      NativeViewerStatus.Preparing => SettingsLocale.L(
        "키뷰어를 준비하고 있어요. 준비되면 자동으로 표시돼요.",
        "Preparing the key viewer. It appears automatically when ready."
      ),
      NativeViewerStatus.ReplayRendering => SettingsLocale.L(
        "ImplDmNote가 리플레이를 렌더링하고 있어요. 끝나면 키뷰어가 다시 표시돼요.",
        "ImplDmNote is rendering a replay. The key viewer returns when it finishes."
      ),
      NativeViewerStatus.Reconnecting => SettingsLocale.L(
        "키뷰어를 다시 연결하고 있어요. 계속 보이지 않으면 ImplDmNote를 다시 열어 주세요.",
        "Reconnecting the key viewer. If it stays hidden, reopen ImplDmNote."
      ),
      NativeViewerStatus.Incompatible => SettingsLocale.L(
        "ImplDmNote와 모드를 최신 버전으로 업데이트하면 키뷰어를 쓸 수 있어요.",
        "Update ImplDmNote and the mod to the latest version to use the key viewer."
      ),
      NativeViewerStatus.PageUnavailable => SettingsLocale.L(
        "ImplDmNote에서 OBS 모드를 껐다 켜면 키뷰어를 다시 불러올 수 있어요.",
        "Turn OBS mode off and on in ImplDmNote to load the key viewer again."
      ),
      NativeViewerStatus.WindowUnavailable => SettingsLocale.L(
        "게임 창으로 돌아오거나 창 모드로 바꾸면 키뷰어를 다시 붙일 수 있어요.",
        "Return to the game window or switch to windowed mode to attach the key viewer."
      ),
      NativeViewerStatus.ComponentMissing => SettingsLocale.L(
        "모드를 다시 설치하고 게임을 재시작하면 키뷰어를 쓸 수 있어요.",
        "Reinstall the mod and restart ADOFAI to use the key viewer."
      ),
      NativeViewerStatus.WebViewRuntimeMissing => SettingsLocale.L(
        "Microsoft Edge WebView2 런타임을 설치하거나 업데이트한 뒤 게임을 다시 시작하면 키뷰어를 쓸 수 있어요.",
        "Install or update the Microsoft Edge WebView2 Runtime, then restart ADOFAI to use the key viewer."
      ),
      NativeViewerStatus.UnsupportedSystem => UnityEngine.Application.platform == RuntimePlatform.WindowsPlayer
        ? SettingsLocale.L(
          "게임 내 키뷰어는 Windows 10 이상에서 쓸 수 있어요.",
          "In-game key viewers need Windows 10 or later."
        )
        : SettingsLocale.L(
          "게임 내 키뷰어는 macOS 11 이상에서 쓸 수 있어요.",
          "In-game key viewers need macOS 11 or later."
        ),
      NativeViewerStatus.UnsupportedPlatform => SettingsLocale.L(
        "게임 내 키뷰어는 지금 macOS와 Windows에서만 쓸 수 있어요. ImplDmNote 창이나 OBS로 키뷰어를 볼 수 있어요.",
        "In-game key viewers currently run on macOS and Windows only. Use the ImplDmNote window or OBS instead."
      ),
      _ => string.Empty,
    };
}
