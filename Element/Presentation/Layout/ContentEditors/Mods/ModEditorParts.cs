using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Element's pickers for a screen a mod writes with ElementUI
/// (<see cref="EditorParts"/>): the same views <c>EditorPage</c>'s parts
/// show, bound to the mod's own settings.
/// </summary>
internal sealed class ModEditorParts : IEditorParts
{
  private readonly ModEditorContext _context;

  public ModEditorParts(ModEditorContext context) => _context = context;

  public View ColorPicker(string label, Binding<Color> color, bool opacity) =>
    ModParts.ColorPicker(label, color, opacity);

  public View PaintPicker(string label, Binding<Paint> paint) => ModParts.PaintPicker(label, paint);

  public View FontPicker(string label, Binding<string> font) => ModParts.FontPicker(label, font, _context);

  public View AnchorPicker(string label, Binding<Anchor> anchor) => ModParts.AnchorPicker(label, anchor, _context);

  public View ShortcutField(string label, Binding<Shortcut> shortcut) =>
    ModParts.ShortcutField(label, shortcut, _context);
}
