using System;

namespace Element.Configuration;

/// <summary>
/// Marks a saved settings class. <see cref="SettingsCopy"/> fills a field of
/// such a class in place instead of replacing the object, so whoever holds it
/// sees the new values.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class CopiedInPlaceAttribute : Attribute { }
