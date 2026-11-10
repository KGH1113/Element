using System;
using System.Collections.Generic;

namespace ElementKit.Elements;

/// <summary>
/// Something players change on your element's editing screen. Element saves it
/// with the layout, so it survives restarts, profiles and undo; you read it
/// through <see cref="Props"/> when you draw.
/// </summary>
public abstract class Property
{
  private protected Property(string key, string koreanName, string englishName)
  {
    Key = key;
    KoreanName = koreanName;
    EnglishName = englishName;
  }

  /// <summary>What layouts save: lowercase letters, digits and dashes. Never change it once released.</summary>
  public string Key { get; }

  /// <summary>The name players see in Korean, such as "왼손 색".</summary>
  public string KoreanName { get; }

  /// <summary>The name players see in English, such as "Left hand".</summary>
  public string EnglishName { get; }
}

/// <summary>What a number is measured in; the editing screen shows it after the number.</summary>
public enum NumberUnit
{
  /// <summary>A count or a plain number.</summary>
  None,

  /// <summary>Pixels at 1080p, like a shape's width.</summary>
  Pixels,

  /// <summary>Percent.</summary>
  Percent,

  /// <summary>Degrees.</summary>
  Degrees,
}

/// <summary>
/// A number players type, or work out from values with the keypad, like a
/// shape's width. It stays between <see cref="Min"/> and <see cref="Max"/>.
/// </summary>
public sealed class NumberProperty : Property
{
  /// <param name="key">Lowercase letters, digits and dashes, such as "width".</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="value">The number a new element starts with.</param>
  /// <param name="min">The smallest number it takes.</param>
  /// <param name="max">The largest number it takes.</param>
  /// <param name="unit">What the number measures.</param>
  public NumberProperty(
    string key,
    string koreanName,
    string englishName,
    double value,
    double min,
    double max,
    NumberUnit unit = NumberUnit.Pixels
  )
    : base(key, koreanName, englishName)
  {
    Default = value;
    Min = min;
    Max = max;
    Unit = unit;
  }

  /// <summary>The number a new element starts with.</summary>
  public double Default { get; }

  /// <summary>The smallest number it takes.</summary>
  public double Min { get; }

  /// <summary>The largest number it takes.</summary>
  public double Max { get; }

  /// <summary>What the number measures.</summary>
  public NumberUnit Unit { get; }
}

/// <summary>
/// A color players pick: the highlight color or one of their own, with its
/// opacity, or a gradient. Draw it with <see cref="Surface.Box"/> to get all of
/// that; <see cref="ElementColor.Solid"/> gives one color for your own drawing.
/// </summary>
public sealed class ColorProperty : Property
{
  /// <summary>Pass as the color to start with the player's highlight color.</summary>
  public const string Highlight = "highlight";

  /// <param name="key">Lowercase letters, digits and dashes, such as "left".</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="color">The color a new element starts with: "#RRGGBB" or <see cref="Highlight"/>.</param>
  /// <param name="opacity">The opacity it starts with, from 0 to 1.</param>
  public ColorProperty(string key, string koreanName, string englishName, string color, float opacity = 1)
    : base(key, koreanName, englishName)
  {
    DefaultColor = color;
    DefaultOpacity = opacity;
  }

  /// <summary>"#RRGGBB" or <see cref="Highlight"/>.</summary>
  public string DefaultColor { get; }

  /// <summary>From 0 to 1.</summary>
  public float DefaultOpacity { get; }

  /// <summary>Whether a new element follows the highlight color.</summary>
  public bool StartsWithHighlight => string.Equals(DefaultColor, Highlight, StringComparison.Ordinal);
}

/// <summary>One of a <see cref="ChoiceProperty"/>'s options.</summary>
public sealed class ChoiceOption
{
  /// <param name="key">What layouts save: lowercase letters, digits and dashes.</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  public ChoiceOption(string key, string koreanName, string englishName)
  {
    Key = key;
    KoreanName = koreanName;
    EnglishName = englishName;
  }

  /// <summary>What layouts save.</summary>
  public string Key { get; }

  /// <summary>The name players see in Korean.</summary>
  public string KoreanName { get; }

  /// <summary>The name players see in English.</summary>
  public string EnglishName { get; }
}

/// <summary>One option out of a few, such as which way bars grow. A new element starts with the first.</summary>
public sealed class ChoiceProperty : Property
{
  private readonly ChoiceOption[] _options;

  /// <param name="key">Lowercase letters, digits and dashes, such as "align".</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="options">Two or more options; the first is where a new element starts.</param>
  public ChoiceProperty(string key, string koreanName, string englishName, params ChoiceOption[] options)
    : base(key, koreanName, englishName) => _options = options ?? Array.Empty<ChoiceOption>();

  /// <summary>The options in the order players see them.</summary>
  public IReadOnlyList<ChoiceOption> Options => _options;
}

/// <summary>On or off, such as whether numbers show.</summary>
public sealed class ToggleProperty : Property
{
  /// <param name="key">Lowercase letters, digits and dashes, such as "numbers".</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="on">Whether a new element starts on.</param>
  public ToggleProperty(string key, string koreanName, string englishName, bool on)
    : base(key, koreanName, englishName) => Default = on;

  /// <summary>Whether a new element starts on.</summary>
  public bool Default { get; }
}
