using System;
using Element.Presentation.Settings;
using TMPro;

namespace Element.Presentation.Layout;

/// <summary>The settings UI's factory as the layout editor and its editors use it.</summary>
internal static class EditorUiFactory
{
  public static SettingsUiFactory Create(TMP_FontAsset font, Func<bool> reduceMotion) =>
    new(font) { ReduceMotion = reduceMotion };
}
