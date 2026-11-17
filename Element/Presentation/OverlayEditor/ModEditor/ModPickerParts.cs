using System;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Declarative.Controls;
using ElementKit;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>The page's pickers: colors, paints, fonts and shortcuts, as the column's rows opening Element's own tools (<see cref="ModParts"/>).</summary>
internal sealed class ModPickerParts
{
  private readonly ModEditorPage _page;

  public ModPickerParts(ModEditorPage page) => _page = page;

  public EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true)
  {
    if (!opacity)
      value.a = 1;
    ModPart part = _page.Part();
    ModValueViewModel<Color> current = _page.Value(part, value, changed);
    Binding<Color> color = Bind(() => current.Value);
    part.Control = () => ModParts.ColorPicker(label, color, opacity, _page.Context);
    return _page.Add(part);
  }

  public EditorControl ColorPicker(string label, Paint value, Action<Paint> changed)
  {
    ModPart part = _page.Part();
    // The mod gets a copy each time, so what it keeps isn't changed under it.
    ModValueViewModel<Paint> current = _page.Value<Paint>(
      part,
      value?.Clone() ?? Paint.Solid(Color.white),
      paint => changed?.Invoke(paint?.Clone() ?? Paint.Solid(Color.white))
    );
    Binding<Paint> paint = Bind(() => current.Value);
    part.Control = () => ModParts.PaintPicker(label, paint, _page.Context);
    return _page.Add(part);
  }

  public EditorControl FontPicker(string label, string font, Action<string> changed)
  {
    ModPart part = _page.Part();
    ModValueViewModel<string> current = _page.Value(part, OverlayFontKey.Normalize(font), changed);
    Binding<string> binding = Bind(() => current.Value);
    part.Control = () => ModParts.FontPicker(label, binding, _page.Context);
    return _page.Add(part);
  }

  public EditorControl Shortcut(string label, ElementKit.Shortcut value, Action<ElementKit.Shortcut> changed)
  {
    ModPart part = _page.Part();
    ModValueViewModel<ElementKit.Shortcut> current = _page.Value(part, value, changed);
    ShortcutFieldViewModel model = ModParts.ShortcutModel(Bind(() => current.Value), _page.Context);
    part.Control = () => ModParts.ShortcutField(label, model, _page.Context);
    return _page.Add(part);
  }
}
