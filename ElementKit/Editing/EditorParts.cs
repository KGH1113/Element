using System;
using ElementUI;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// Element's own pickers as ElementUI views, for an editing screen written
/// with ElementUI (<see cref="OverlayElement{TSettings}.Editor"/>): the color
/// picker, the font list, the nine-point anchor and the shortcut recorder.
/// Bring them in with <c>using static ElementKit.EditorParts;</c> beside
/// <c>using static ElementUI.UI;</c>.
/// </summary>
/// <remarks>
/// Each takes a label and a binding, like ElementUI's own controls:
/// <c>ColorPicker(VM.L("색", "Color"), Bind(() =&gt; VM.Settings.Fill))</c>.
/// They work only on Element's editing screen, where Element draws them.
/// </remarks>
public static class EditorParts
{
  /// <summary>A color swatch that opens Element's color picker.</summary>
  /// <param name="label">What it is, left of the swatch.</param>
  /// <param name="color">The color it shows and the picker changes.</param>
  /// <param name="opacity">Whether players can pick the color's opacity too.</param>
  public static View ColorPicker(string label, Binding<Color> color, bool opacity = true) =>
    Parts.ColorPicker(label, color, opacity);

  /// <summary>
  /// A color swatch that opens Element's color picker with all it offers: the
  /// player's highlight color, a color of their own with opacity, or a gradient.
  /// </summary>
  public static View ColorPicker(string label, Binding<Paint> paint) => Parts.PaintPicker(label, paint);

  /// <summary>
  /// A font from the ones on this PC or added to Element; empty for the
  /// overlay's default font. Draw with it through <see cref="Drawing.SetFont"/>.
  /// </summary>
  public static View FontPicker(string label, Binding<string> font) => Parts.FontPicker(label, font);

  /// <summary>One of nine points of a box, picked on a grid like Element's Grow from.</summary>
  public static View AnchorPicker(string label, Binding<Anchor> anchor) => Parts.AnchorPicker(label, anchor);

  /// <summary>
  /// A shortcut, set the way Element's own are: click, then press a key with at
  /// most one modifier; Esc cancels and the × clears it.
  /// </summary>
  public static View ShortcutField(string label, Binding<Shortcut> shortcut) => Parts.ShortcutField(label, shortcut);

  // Element draws the parts; it sets this when it starts.
  internal static IEditorParts Host { get; set; }

  private static IEditorParts Parts =>
    Host
    ?? throw new InvalidOperationException(
      "Element's editing parts can be used only on Element's editing screen, inside OverlayElement<T>.Editor."
    );
}

/// <summary>Draws <see cref="EditorParts"/>; only Element implements it.</summary>
internal interface IEditorParts
{
  View ColorPicker(string label, Binding<Color> color, bool opacity);

  View PaintPicker(string label, Binding<Paint> paint);

  View FontPicker(string label, Binding<string> font);

  View AnchorPicker(string label, Binding<Anchor> anchor);

  View ShortcutField(string label, Binding<Shortcut> shortcut);
}
