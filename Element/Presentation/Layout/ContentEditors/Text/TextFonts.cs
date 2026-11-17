using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Layout.ContentEditors.Shell.Dock;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// Fonts: like size and color, a font goes to the selected characters, or to
/// the whole text when nothing is selected. Each pick shows on the text at
/// once; one undo takes back everything tried while the list was open.
/// </summary>
internal sealed class TextFonts
{
  private const int FontLabelLength = 18;

  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextColor _color;
  private OverlayBarButtonModel _fontButton;
  private UiFontPopover _fontPopover;
  private bool _fontSnapshotTaken;

  public TextFonts(ITextEditorHost host, TextDocumentEditing text, TextColor color)
  {
    _host = host;
    _text = text;
    _color = color;
  }

  public bool Visible => _fontPopover?.Visible ?? false;

  /// <summary>The font list; made by <see cref="Build"/>.</summary>
  public IPopover Popover => _fontPopover;

  /// <summary>What the open list picks for, as the column's back row names it.</summary>
  public string Title { get; private set; } = "";

  /// <summary>The font search has the keyboard.</summary>
  public bool IsTyping => _fontPopover.IsTyping;

  public bool Contains(Vector2 screen) => _fontPopover.Contains(screen);

  /// <summary>The bar's Font button; made by <see cref="BuildButton"/>.</summary>
  public OverlayBarButtonModel Button => _fontButton;

  public OverlayBarButtonModel BuildButton() =>
    _fontButton = new OverlayBarButtonModel(
      "Text font",
      L("폰트", "Font"),
      ToggleFont,
      value: true,
      icon: UiIconKind.Font,
      opens: true
    );

  public void Build(OverlayEditorFrame screen, OverlayFonts fonts, bool reduceMotion)
  {
    // Whoever closes it redraws the bar; the next session is its own undo step.
    _fontPopover = new UiFontPopover(screen.Ui, screen.Frame, fonts, reduceMotion, PickFont)
    {
      Hidden = () => _fontSnapshotTaken = false,
    };
  }

  public void Tick() => _fontPopover.Tick();

  public void Hide() => _fontPopover?.Hide();

  private void ToggleFont()
  {
    bool open = _fontPopover.Visible;
    _host.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    if (!open)
    {
      OverlayFonts fonts = _host.Element.Fonts;
      string current = _text.RangeFont();
      Title = _text.HasSelection ? L("선택한 글자 폰트", "Selected text font") : L("글자 폰트", "Text font");
      // The column's back row names the list; it needs no title of its own.
      _fontPopover.Show("", current, L("기본 폰트", "Default font"), DefaultFontName(fonts));
    }
    _host.Render();
  }

  private void PickFont(string key)
  {
    if (!_fontSnapshotTaken)
    {
      _text.Remember(false);
      _fontSnapshotTaken = true;
    }
    _text.Range(out int start, out int end);
    if (start < end)
      _text.Document.SetFont(start, end, key);
    else
      _text.PendingFont = OverlayFontKey.Normalize(key);
    _host.Render();
  }

  /// <summary>The bar names the range's font, lit while the list is open.</summary>
  public void ShowButton() => _fontButton.Set(FontLabel(), false, _fontPopover.Visible);

  private string FontLabel()
  {
    string font = _text.RangeFont();
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

  // In the property column from top (under its back row) like the color
  // picker, above the keypad, a little wider for long names.
  public void Place(InputDockPanel dock, float top)
  {
    if (_fontPopover == null || !_fontPopover.Visible || dock == null)
      return;
    Rect keypad = dock.KeypadArea;
    float width = Mathf.Max(keypad.width, UiFontPopover.PreferredWidth);
    float height = Mathf.Min(UiFontPopover.PreferredHeight, dock.Top - 12 - top);
    _fontPopover.Place(keypad.xMax - width, top, width, height);
    _fontPopover.Root.SetAsLastSibling();
  }
}
