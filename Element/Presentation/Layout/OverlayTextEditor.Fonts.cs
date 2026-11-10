using Element.Configuration;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Fonts: like size and color, a font goes to the selected characters, or to
// the whole text when nothing is selected. Each pick shows on the text at
// once; one undo takes back everything tried while the list was open.
internal sealed partial class OverlayTextEditor
{
  private const int FontLabelLength = 18;
  private OverlayBarButton _fontButton;
  private OverlayFontPopover _fontPopover;
  private bool _fontSnapshotTaken;

  private void BuildFontControls()
  {
    // Whoever closes it redraws the bar; the next session is its own undo step.
    _fontPopover = new OverlayFontPopover(_ui, _frame, _element.Fonts, _reduceMotion(), PickFont)
    {
      Hidden = () => _fontSnapshotTaken = false,
    };
  }

  private void ToggleFont()
  {
    bool open = _fontPopover.Visible;
    ClosePopovers();
    if (_picker.Visible)
      _picker.Hide();
    if (!open)
    {
      OverlayFonts fonts = _element.Fonts;
      string current = RangeFont();
      _fontPopover.Show(
        HasSelection ? L("선택한 글자 폰트", "Selected text font") : L("글자 폰트", "Text font"),
        current,
        L("기본 폰트", "Default font"),
        DefaultFontName(fonts)
      );
    }
    Render();
  }

  private void PickFont(string key)
  {
    if (!_fontSnapshotTaken)
    {
      Remember(false);
      _fontSnapshotTaken = true;
    }
    Range(out int start, out int end);
    if (start < end)
      _document.SetFont(start, end, key);
    else
      _pendingFont = OverlayFontKey.Normalize(key);
    Render();
  }

  // The range's font: empty follows the default, null mixes several.
  private string RangeFont()
  {
    Range(out int start, out int end);
    return start < end ? _document.RangeFont(start, end) : TypingFont(_caretIndex);
  }

  private string FontLabel()
  {
    string font = RangeFont();
    if (font == null)
      return L("여러 개", "Mixed");
    if (font.Length == 0)
      return L("기본", "Default");
    string name = OverlayFonts.DisplayName(font);
    return name.Length > FontLabelLength ? name.Substring(0, FontLabelLength - 1).TrimEnd() + "…" : name;
  }

  internal static string DefaultFontName(OverlayFonts fonts) =>
    fonts == null || fonts.DefaultKey.Length == 0
      ? L("메이플스토리", "MapleStory")
      : OverlayFonts.DisplayName(fonts.DefaultKey);

  // Right above the keypad like the color picker, a little wider for long names.
  private void PlaceFontPopover()
  {
    if (_fontPopover == null || !_fontPopover.Visible || _dock == null)
      return;
    Rect keypad = _dock.KeypadArea;
    float width = Mathf.Max(keypad.width, OverlayFontPopover.PreferredWidth);
    float top = OverlayEditorFrame.TopBar + 8;
    float height = Mathf.Min(OverlayFontPopover.PreferredHeight, _dock.Top - 12 - top);
    _fontPopover.Place(keypad.xMax - width, Mathf.Max(top, _dock.Top - 12 - height), width, height);
    _fontPopover.Root.SetAsLastSibling();
  }
}
