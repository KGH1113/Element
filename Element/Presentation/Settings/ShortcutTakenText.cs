using Element.Configuration.Effects;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings;

/// <summary>Why a recorded shortcut can't be used: it opens settings or applies a preset already.</summary>
internal static class ShortcutTakenText
{
  /// <param name="owner">The preset that has the shortcut, or null when it opens settings.</param>
  public static string For(EffectPreset owner) =>
    owner == null
      ? L("설정 화면을 여는 단축키예요. 다른 키를 눌러주세요.", "This shortcut opens settings. Press another one.")
      : L(
        "'" + owner.Name + "' 프리셋이 이 단축키를 쓰고 있어요. 다른 키를 눌러주세요.",
        "'" + owner.Name + "' already uses this shortcut. Press another one."
      );
}
