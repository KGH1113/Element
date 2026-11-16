using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// The editing screen of your element, and the parts to build it from: the
/// same labels, buttons, switches and color picker Element's own screens use.
/// Element owns the screen around them: the dim, Cancel and Done, Esc to
/// finish, undo, Grow from, and a live preview of your element.
/// </summary>
/// <remarks>
/// <para>
/// Parts go one under another in the property column right of the preview,
/// where Element's text editor keeps its own, one row each: the label on the
/// left and the control on the right; a card is a heading over its parts.
/// Call <see cref="EditorControl.At"/> to put a part anywhere instead (left
/// of the column, around <see cref="PreviewArea"/>), or add your own UI
/// under <see cref="Root"/>.
/// </para>
/// <para>
/// Positions are in the screen's own units, top left (0, 0), y down: about
/// 1120 by 760 on any display.
/// </para>
/// </remarks>
public abstract class EditorPage
{
  // Only Element implements this: a mod uses the one Element hands it and
  // cannot subclass it, so new members never break mods.
  internal EditorPage() { }

  /// <summary>The whole screen, for parts you place yourself or UI of your own.</summary>
  public abstract RectTransform Root { get; }

  /// <summary>The screen's size in its own units.</summary>
  public abstract Vector2 Size { get; }

  /// <summary>Where the preview of your element sits: the room left of the property column.</summary>
  public abstract Rect PreviewArea { get; }

  /// <summary>Whether players see Element in Korean.</summary>
  public abstract bool Korean { get; }

  /// <summary><paramref name="korean"/> or <paramref name="english"/>, as players see Element.</summary>
  public string L(string korean, string english) => Korean ? korean : english;

  /// <summary>Words on their own, such as a heading or a hint.</summary>
  public abstract EditorControl Label(string text);

  /// <summary>A button. After <paramref name="clicked"/> the parts are made again, so they show what it changed.</summary>
  public abstract EditorControl Button(string text, Action clicked);

  /// <summary>On or off.</summary>
  public abstract EditorControl Switch(string label, bool on, Action<bool> changed);

  /// <summary>A number dragged between <paramref name="min"/> and <paramref name="max"/>, with its value beside it.</summary>
  /// <param name="label">What it is, left of the slider; empty for a slider as wide as the row.</param>
  /// <param name="value">The number to start at.</param>
  /// <param name="min">The smallest number.</param>
  /// <param name="max">The largest number.</param>
  /// <param name="changed">Runs as the slider moves.</param>
  /// <param name="step">What the number moves by, such as 1 for whole numbers; 0 moves freely.</param>
  public abstract EditorControl Slider(
    string label,
    float value,
    float min,
    float max,
    Action<float> changed,
    float step = 0
  );

  /// <summary>
  /// A number typed in. <paramref name="changed"/> runs as it is typed while the
  /// number is between <paramref name="min"/> and <paramref name="max"/>; when
  /// typing is done, a number outside them is brought to the nearest.
  /// </summary>
  public abstract EditorControl Number(string label, float value, float min, float max, Action<float> changed);

  /// <summary>Words typed in; <paramref name="changed"/> runs with each letter.</summary>
  public abstract EditorControl TextField(string label, string value, Action<string> changed);

  /// <summary>One option out of a few, side by side.</summary>
  public abstract EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed);

  /// <summary>A color swatch that opens Element's color picker.</summary>
  /// <param name="label">What it is, left of the swatch.</param>
  /// <param name="value">The color to start with.</param>
  /// <param name="changed">Runs as players pick.</param>
  /// <param name="opacity">Whether players can pick the color's opacity too.</param>
  public abstract EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true);

  /// <summary>
  /// A color swatch that opens Element's color picker with all it offers: the
  /// player's highlight color, a color of their own with opacity, or a gradient.
  /// </summary>
  public abstract EditorControl ColorPicker(string label, Paint value, Action<Paint> changed);

  /// <summary>One option out of many, picked from a list that opens under it.</summary>
  public abstract EditorControl Dropdown(
    string label,
    IReadOnlyList<string> options,
    int selected,
    Action<int> changed
  );

  /// <summary>A box to tick, with <paramref name="text"/> beside it.</summary>
  public abstract EditorControl Checkbox(string text, bool on, Action<bool> changed);

  /// <summary>Several options, each on or off, as chips that wrap onto more lines.</summary>
  /// <param name="label">What they are, left of the chips.</param>
  /// <param name="options">The options' names.</param>
  /// <param name="on">Which options start on, by index; missing ones start off.</param>
  /// <param name="changed">Runs with the option's index and whether it is now on.</param>
  public abstract EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  );

  /// <summary>A whole number with − and + beside it.</summary>
  public abstract EditorControl Stepper(string label, int value, int min, int max, Action<int> changed, int step = 1);

  /// <summary>Two numbers on one slider, the lower always at most the higher.</summary>
  /// <param name="label">What they are, left of the slider.</param>
  /// <param name="low">The lower number to start at.</param>
  /// <param name="high">The higher number to start at.</param>
  /// <param name="min">The smallest number.</param>
  /// <param name="max">The largest number.</param>
  /// <param name="changed">Runs with the lower and the higher as either handle moves.</param>
  /// <param name="step">What the numbers move by; 0 moves freely.</param>
  public abstract EditorControl RangeSlider(
    string label,
    float low,
    float high,
    float min,
    float max,
    Action<float, float> changed,
    float step = 0
  );

  /// <summary>
  /// A shortcut, set the way Element's own are: click, then press a key with at
  /// most one modifier; Esc cancels and the × clears it.
  /// </summary>
  public abstract EditorControl Shortcut(string label, Shortcut value, Action<Shortcut> changed);

  /// <summary>
  /// A font from the ones on this PC or added to Element; empty for the overlay's
  /// default font. Draw with it through <see cref="Drawing.SetFont"/>.
  /// </summary>
  public abstract EditorControl FontPicker(string label, string font, Action<string> changed);

  /// <summary>One of nine points of a box, picked on a grid like Element's Grow from.</summary>
  public abstract EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed);

  /// <summary>A small button showing only <paramref name="icon"/>; <paramref name="hint"/> says what it does on hover.</summary>
  public abstract EditorControl IconButton(EditorIcon icon, string hint, Action clicked);

  /// <summary>
  /// Words players can add to, remove from, reorder and type in. The parts
  /// change <paramref name="items"/> themselves; <paramref name="changed"/> runs after.
  /// </summary>
  public abstract EditorControl List(string label, List<string> items, Action changed = null, string newItem = "");

  /// <summary>A heading over the parts that follow.</summary>
  public abstract EditorControl Section(string title);

  /// <summary>A thin line across the column.</summary>
  public abstract EditorControl Divider();

  /// <summary>
  /// A card that holds the parts made until it is disposed, with an optional
  /// title: <c>using (page.Card("Colors")) { page.ColorPicker(...); }</c>
  /// </summary>
  public abstract IDisposable Card(string title = null);

  /// <summary>
  /// Tabs with these names; parts made inside <c>using (tabs.Page(i))</c> show
  /// only while that tab is picked. The picked tab stays across undo.
  /// </summary>
  public abstract EditorTabs Tabs(IReadOnlyList<string> names);

  /// <summary>Empty space in the column before the next part.</summary>
  public abstract void Gap(float height = 16);
}
