using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Element.Configuration.Profiles;

/// <summary>Reads and writes profile files. Reading never trusts the file: no DTDs, no outside references.</summary>
internal static class ProfileFileCodec
{
  public static void Write(Stream stream, ProfileFile file)
  {
    using XmlWriter writer = XmlWriter.Create(
      stream,
      new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }
    );
    new XmlSerializer(typeof(ProfileFile)).Serialize(writer, file);
  }

  public static ProfileFileResult Read(Stream stream)
  {
    ProfileFile file;
    try
    {
      using XmlReader reader = XmlReader.Create(
        stream,
        new XmlReaderSettings
        {
          // Overlay texts may be only spaces; see SettingsXml.
          IgnoreWhitespace = false,
          DtdProcessing = DtdProcessing.Prohibit,
          XmlResolver = null,
        }
      );
      XmlSerializer serializer = new(typeof(ProfileFile));
      if (!serializer.CanDeserialize(reader))
        return ProfileFileResult.Fail(ProfileFileProblem.NotProfile);
      file = serializer.Deserialize(reader) as ProfileFile;
    }
    catch (XmlException exception)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.NotProfile, exception);
    }
    catch (InvalidOperationException exception)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.Damaged, exception);
    }
    if (file == null)
      return ProfileFileResult.Fail(ProfileFileProblem.NotProfile);
    if (
      file.Format > ProfileFile.CurrentFormat
      || file.Content?.SettingsVersion > ElementSettings.CurrentSettingsVersion
    )
      return ProfileFileResult.Fail(ProfileFileProblem.NewerVersion, file: file);
    if (file.Content == null)
      return ProfileFileResult.Fail(ProfileFileProblem.Damaged, file: file);
    return new ProfileFileResult(file, ProfileFileProblem.None);
  }
}
