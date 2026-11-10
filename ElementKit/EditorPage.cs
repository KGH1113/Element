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
/// Parts go in a column under the preview, one row each, a label on the left
/// and the control on the right; leave the label empty for a control as wide
/// as the row. Call <see cref="EditorControl.At"/> to put a part anywhere
/// instead, or add your own UI under <see cref="Root"/>.
/// </para>
/// <para>
/// Positions are in the screen's own units, top left (0, 0), y down: about
/// 1120 by 760 on any display.
/// </para>
/// </remarks>
public abstract class EditorPage
{
  /// <summary>The whole screen, for parts you place yourself or UI of your own.</summary>
  public abstract RectTransform Root { get; }

  /// <summary>The screen's size in its own units.</summary>
  public abstract Vector2 Size { get; }

  /// <summary>Where the preview of your element sits.</summary>
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

  /// <summary>A number typed in, kept between <paramref name="min"/> and <paramref name="max"/>.</summary>
  public abstract EditorControl Number(string label, float value, float min, float max, Action<float> changed);

  /// <summary>Words typed in; <paramref name="changed"/> runs when typing is done.</summary>
  public abstract EditorControl TextField(string label, string value, Action<string> changed);

  /// <summary>One option out of a few, side by side.</summary>
  public abstract EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed);

  /// <summary>A color swatch that opens Element's color picker.</summary>
  /// <param name="label">What it is, left of the swatch.</param>
  /// <param name="value">The color to start with.</param>
  /// <param name="changed">Runs as players pick.</param>
  /// <param name="opacity">Whether players can pick the color's opacity too.</param>
  public abstract EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true);

  /// <summary>Empty space in the column before the next part.</summary>
  public abstract void Gap(float height = 16);
}

/// <summary>A part on the editing screen.</summary>
public abstract class EditorControl
{
  /// <summary>The part's UI object; its pivot is its top left.</summary>
  public abstract RectTransform Rect { get; }

  /// <summary>Takes the part out of the column and puts its top left at (<paramref name="x"/>, <paramref name="y"/>).</summary>
  public abstract EditorControl At(float x, float y);

  /// <summary>Makes the part <paramref name="width"/> wide.</summary>
  public abstract EditorControl Width(float width);

  /// <summary>Whether the part shows. A hidden part keeps its place in the column.</summary>
  public abstract bool Visible { get; set; }
}
