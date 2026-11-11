using System;

namespace ElementKit;

/// <summary>
/// Puts an <see cref="OverlayElement"/> in Element's Add Element → More, under
/// your mod. Element finds it on its own when it is installed: there is nothing
/// to call, and your mod doesn't need Element as a requirement.
/// </summary>
/// <example>
/// <code>
/// [Element("bars", "예시 막대", "Sample bars")]
/// public sealed class BarsElement : OverlayElement&lt;BarsSettings&gt; { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ElementAttribute : Attribute
{
  /// <param name="key">What layouts save: lowercase letters, digits and dashes, such as "bars". Never change it once released.</param>
  /// <param name="koreanName">The name players see in Korean, such as "예시 막대".</param>
  /// <param name="englishName">The name players see in English, such as "Sample bars".</param>
  public ElementAttribute(string key, string koreanName, string englishName)
  {
    Key = key;
    KoreanName = koreanName;
    EnglishName = englishName;
  }

  /// <summary>What layouts save, without your mod's id.</summary>
  public string Key { get; }

  /// <summary>The name players see in Korean.</summary>
  public string KoreanName { get; }

  /// <summary>The name players see in English.</summary>
  public string EnglishName { get; }
}
