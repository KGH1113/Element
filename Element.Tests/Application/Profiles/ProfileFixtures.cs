using System.IO;
using System.Xml.Serialization;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Configuration.Profiles;

namespace Element.Tests.Application.Profiles;

/// <summary>Settings for the profile tests, and their saved values to compare.</summary>
internal static class ProfileFixtures
{
  internal static ElementSettings Fresh()
  {
    ElementSettings settings = new();
    SettingsMigrator.Migrate(settings);
    settings.Gameplay.Normalize();
    settings.Effects.Normalize();
    return settings;
  }

  internal static OverlayLayoutSettings SpacedText(string id)
  {
    OverlayLayoutSettings layout = new()
    {
      Id = id,
      TemplateId = OverlayElementKinds.Text,
      Customized = true,
      Text = new OverlayTextSettings(),
    };
    layout.Text.Runs.Add(OverlayTextRun.ValueRun("fps", OverlayColorSettings.HighlightColor()));
    layout.Text.Runs.Add(OverlayTextRun.Literal("  ", OverlayColorSettings.Custom("#FFFFFF")));
    layout.Text.Runs.Add(OverlayTextRun.ValueRun("bpm", OverlayColorSettings.HighlightColor()));
    return layout;
  }

  // Everything a profile carries, as the settings file would hold it.
  internal static string Values(ElementSettings settings) => Xml(ProfileContent.Capture(settings));

  internal static string Xml(object value)
  {
    using StringWriter writer = new();
    new XmlSerializer(value.GetType()).Serialize(writer, value);
    return writer.ToString();
  }
}
