using Element.Configuration.Effects;

namespace Element.Presentation.Settings;

/// <summary>What the notice says after an effect preset's shortcut switched it.</summary>
internal static class EffectPresetShortcutNotice
{
  /// <param name="enabled">Effect removal is on after the switch.</param>
  /// <param name="saved">The settings were written.</param>
  public static (string Title, string Detail) For(EffectPreset preset, bool enabled, bool saved)
  {
    string detail = saved
      ? SettingsLocale.L("레벨을 시작하거나 재시작하면 반영됩니다.", "Takes effect when a level starts or restarts.")
      : SettingsLocale.L(
        "저장하지 못했어요. 모드 폴더의 쓰기 권한을 확인해주세요.",
        "Not saved. Check write permission for the mod folder."
      );
    string title = enabled
      ? SettingsLocale.L("이펙트 프리셋: ", "Effect preset: ") + preset.Name
      : SettingsLocale.L("이펙트 제거 꺼짐", "Effect removal off");
    return (title, detail);
  }
}
