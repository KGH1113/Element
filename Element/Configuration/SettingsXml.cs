using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Element.Configuration;

/// <summary>
/// Reads a settings file keeping text that is only whitespace. XmlSerializer
/// reading a plain stream drops it, which lost the space between two values
/// in an overlay text on every restart.
/// </summary>
internal static class SettingsXml
{
  public static T Read<T>(Stream stream)
    where T : class
  {
    using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = false });
    return new XmlSerializer(typeof(T)).Deserialize(reader) as T;
  }
}
