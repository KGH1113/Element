using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Fonts: like size and color, a font goes to the selected characters, or to
/// the whole text when nothing is selected. The font field's input method is
/// the font list, which stands in the dock where the keypad does. Each pick
/// shows on the text at once; one undo takes back everything tried while the
/// list was open.
/// </summary>
internal sealed class TextFonts
{
  private const int FontLabelLength = 18;

  private readonly IEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextColor _color;
  private PropertyButtonViewModel _fontButton;
  private UiFontPopover _fontPopover;
  private bool _fontSnapshotTaken;

  public TextFonts(IEditorActions editor, ITextEditorContext context, TextDocumentEditing text, TextColor color)
  {
    _editor = editor;
    _context = context;
    _text = text;
    _color = color;
  }

  public bool Visible => _fontPopover?.Visible ?? false;

  /// <summary>The font list; made by <see cref="Build"/>.</summary>
  public IPopover Popover => _fontPopover;

  /// <summary>The font search has the keyboard.</summary>
  public bool IsTyping => _fontPopover.IsTyping;

  public bool Contains(Vector2 screen) => _fontPopover.Contains(screen);

  /// <summary>The bar's Font button; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _fontButton;

  public PropertyButtonViewModel BuildButton() =>
    _fontButton = new PropertyButtonViewModel(
      "Text font",
      L("폰트", "Font"),
      ToggleFont,
      opens: true,
      fieldGlyph: "Aa"
    );

  public void Build(EditorFramePanel screen, OverlayFonts fonts, bool reduceMotion)
  {
    // Whoever closes it redraws the bar; the next session is its own undo step.
    _fontPopover = new UiFontPopover(screen.Ui, screen.Frame, fonts, reduceMotion, PickFont, bare: true)
    {
      Hidden = () => _fontSnapshotTaken = false,
      // It stands in the input method's place, which is smaller than a popover.
      Compact = true,
    };
  }

  public void Tick() => _fontPopover.Tick();

  public void Hide() => _fontPopover?.Hide();

  private void ToggleFont()
  {
    bool open = _fontPopover.Visible;
    _editor.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    if (!open)
    {
      OverlayFonts fonts = _context.Element.Fonts;
      // The dock names the list; it needs no title of its own.
      _fontPopover.Show("", _text.RangeFont(), L("기본 폰트", "Default font"), DefaultFontName(fonts));
    }
    _editor.Render();
  }

  /// <summary>
  /// The open list goes on with a new selection, or with the whole text once
  /// nothing is selected: it marks that font, and its next pick is a new undo step.
  /// </summary>
  public void Retarget()
  {
    if (!Visible)
      return;
    _fontSnapshotTaken = false;
    _fontPopover.SetCurrent(_text.RangeFont());
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
    _editor.Render();
  }

  /// <summary>The bar names the range's font, lit while the list is open.</summary>
  public void ShowButton() =>
    _fontButton.ShowField(
      new FieldChip("font", FontLabel(), OverlayValueType.Any) { Glyph = "Aa" },
      _fontPopover.Visible
    );

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

  /// <summary>
  /// While the list is open, the dock shows the font field: on its line
  /// <paramref name="whose"/> font and the font it is, the list in the input
  /// method's place.
  /// </summary>
  public void ShowIn(InputDockViewModel dock, string whose) =>
    dock.ShowChoice(DockMethod.Fonts, whose, L("폰트", "Font"), "Aa", FontLabel());

  // Where the dock draws an input method: the list is the font field's.
  public void Place(InputDockPanel dock)
  {
    if (_fontPopover == null || !_fontPopover.Visible || dock == null)
      return;
    Rect place = dock.MethodArea;
    _fontPopover.Place(place.x, place.y, place.width, place.height);
    _fontPopover.Root.SetAsLastSibling();
  }
}
