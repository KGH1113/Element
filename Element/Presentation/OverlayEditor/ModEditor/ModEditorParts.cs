using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// Element's pickers for a screen a mod writes with ElementUI
/// (<see cref="EditorParts"/>), bound to the mod's own settings.
/// </summary>
internal sealed class ModEditorParts : IEditorParts
{
  private readonly ModPickerContext _context;

  public ModEditorParts(ModPickerContext context) => _context = context;

  public View ColorPicker(string label, Binding<Color> color, bool opacity) =>
    ModParts.ColorPicker(label, color, opacity, _context);

  public View PaintPicker(string label, Binding<Paint> paint) => ModParts.PaintPicker(label, paint, _context);

  public View FontPicker(string label, Binding<string> font) => ModParts.FontPicker(label, font, _context);

  public View AnchorPicker(string label, Binding<Anchor> anchor) => ModParts.AnchorPicker(label, anchor, _context);

  public View ShortcutField(string label, Binding<Shortcut> shortcut) =>
    ModParts.ShortcutField(label, shortcut, _context);
}
