using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ElementKit.Elements;

/// <summary>
/// A kind of overlay element your mod adds. Players add it from Add Element →
/// More, place it like any other element, and change its properties on an
/// editing screen built from Element's own parts. You pick which parts the
/// screen has (<see cref="Edit"/>) and draw the element (<see cref="Draw"/>).
/// </summary>
/// <remarks>
/// Keep the properties in <c>static readonly</c> fields of your type; <see cref="Kit.Attach"/>
/// finds the type and its properties there. It uses the instance your type
/// keeps in a static field of its own type, such as
/// <c>public static readonly HandBars Shared = new();</c>, so you can call
/// <see cref="Redraw"/> on it; without one it makes one with the constructor
/// that takes no arguments.
/// </remarks>
/// <example>
/// <code>
/// public sealed class HandBars : ElementType
/// {
///     public static readonly ColorProperty Left = new("left", "왼손 색", "Left hand", "#FF9F43");
///     public static readonly NumberProperty Width = new("width", "너비", "Width", 480, min: 40, max: 1920);
///
///     public HandBars() : base("hand-bars", "손 막대", "Hand bars") { }
///
///     protected override void Edit(EditorLayout editor)
///     {
///         editor.Bar(Left);
///         editor.Below(Width);
///     }
///
///     protected override Vector2 Measure(Props props) => new((float)props[Width], 64);
///
///     protected override void Draw(Surface surface, Props props) =>
///         surface.Box(new Rect(Vector2.zero, surface.Size), props[Left], rounding: 6);
/// }
/// </code>
/// </example>
public abstract class ElementType
{
  private int _revision;

  /// <param name="key">What saved layouts keep: lowercase letters, digits and dashes, such as "hand-bars". Never change it once released.</param>
  /// <param name="koreanName">A short name in Korean, such as "손 막대".</param>
  /// <param name="englishName">A short name in English, such as "Hand bars".</param>
  protected ElementType(string key, string koreanName, string englishName)
  {
    Key = key;
    KoreanName = koreanName;
    EnglishName = englishName;
  }

  /// <summary>The key saved layouts keep, without your mod's id.</summary>
  public string Key { get; }

  /// <summary>The name players see in Korean.</summary>
  public string KoreanName { get; }

  /// <summary>The name players see in English.</summary>
  public string EnglishName { get; }

  /// <summary>The id of the mod that attached this type; null before <see cref="Kit.Attach"/>.</summary>
  public string ModId { get; internal set; }

  /// <summary>The key as layouts save it, <c>ModId/key</c>; null before <see cref="Kit.Attach"/>.</summary>
  public string FullKey => ModId == null ? null : ModId + "/" + Key;

  /// <summary>The properties found in this type's static fields, in the order they are declared.</summary>
  public IReadOnlyList<Property> Properties { get; internal set; } = Array.Empty<Property>();

  /// <summary>
  /// True to be drawn every frame while shown, for something that moves on its
  /// own. Most types leave it off and call <see cref="Redraw"/> when their data changes.
  /// </summary>
  public virtual bool DrawsEveryFrame => false;

  // What Edit put on the screen, checked when the mod attached.
  internal EditorLayout Layout { get; set; }

  // False when Attach found a problem with the type; players can't add it.
  internal bool Accepted { get; set; } = true;

  /// <summary>Advances on every <see cref="Redraw"/>; reading it needs no lock.</summary>
  internal int Revision => Volatile.Read(ref _revision);

  /// <summary>
  /// Draws every element of this type again on Element's next frame, for data
  /// that isn't a property, such as what your mod read from the level. Any thread.
  /// </summary>
  public void Redraw() => Interlocked.Increment(ref _revision);

  /// <summary>
  /// Puts the properties on the editing screen. Without an override every
  /// property goes in the bar, in the order they are declared.
  /// </summary>
  protected virtual void Edit(EditorLayout editor)
  {
    foreach (Property property in Properties)
      editor.Bar(property);
  }

  /// <summary>The element's size in pixels at 1080p. Element places it and keeps the point it grows from still.</summary>
  protected abstract Vector2 Measure(Props props);

  /// <summary>
  /// Draws the element into <paramref name="surface"/>, whose top left is (0, 0)
  /// and whose size is what <see cref="Measure"/> gave. Called when a property,
  /// a value one of them uses or the highlight color changed, and after <see cref="Redraw"/>.
  /// </summary>
  protected abstract void Draw(Surface surface, Props props);

  // Element calls the overrides through these.
  internal void RunEdit(EditorLayout editor) => Edit(editor);

  internal Vector2 RunMeasure(Props props) => Measure(props);

  internal void RunDraw(Surface surface, Props props) => Draw(surface, props);
}
