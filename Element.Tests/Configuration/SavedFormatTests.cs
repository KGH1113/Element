using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Configuration.Profiles;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Configuration;

/// <summary>
/// A settings file and a profile file, each with every saved field filled, were
/// written by the build before the settings classes moved into feature folders
/// (Fixtures/). They must still read back whole and be written out byte for
/// byte the same, so a class's namespace is never part of a saved file.
/// </summary>
internal static class SavedFormatTests
{
  [Suite("settings and profile files written before still read and write the same")]
  public static void Run()
  {
    SettingsFileKeepsItsFormat();
    ProfileFileKeepsItsFormat();
  }

  private static void SettingsFileKeepsItsFormat()
  {
    string saved = Fixture("settings.xml");
    Check(WriteSettings(Sample<ElementSettings>()) == saved, "settings are written as before");
    using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved));
    ElementSettings read = SettingsXml.Read<ElementSettings>(stream);
    Check(read != null && WriteSettings(read) == saved, "a settings file written before reads back whole");
    Check(NamesNoClass(saved), "a settings file names no class namespace");
  }

  private static void ProfileFileKeepsItsFormat()
  {
    string saved = Fixture("profile.eprofile");
    Check(WriteProfile(SampleProfile()) == saved, "profiles are exported as before");
    using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved));
    ProfileFileResult read = ProfileFileCodec.Read(stream);
    Check(read.Problem == ProfileFileProblem.None, "a profile file written before imports: " + read.Problem);
    Check(WriteProfile(read.File) == saved, "a profile file written before reads back whole");
    Check(NamesNoClass(saved), "a profile file names no class namespace");
  }

  // Unity Mod Manager saves settings with an XmlSerializer of the settings class.
  private static string WriteSettings(ElementSettings settings)
  {
    using MemoryStream stream = new();
    using (StreamWriter writer = new(stream, new UTF8Encoding(false), 1024, true))
      new XmlSerializer(typeof(ElementSettings)).Serialize(writer, settings);
    return Lines(Encoding.UTF8.GetString(stream.ToArray()));
  }

  private static string WriteProfile(ProfileFile file)
  {
    using MemoryStream stream = new();
    ProfileFileCodec.Write(stream, file);
    return Lines(Encoding.UTF8.GetString(stream.ToArray()));
  }

  private static ProfileFile SampleProfile()
  {
    ProfileContent content = Sample<ProfileContent>();
    content.SettingsVersion = ElementSettings.CurrentSettingsVersion;
    return new ProfileFile
    {
      Format = ProfileFile.CurrentFormat,
      ModVersion = "1.2.3",
      Name = "Sample",
      Content = content,
      KeyViewers = "KeyViewers",
    };
  }

  private static bool NamesNoClass(string file) =>
    !file.Contains("Element.", StringComparison.Ordinal) && !file.Contains("xsi:type", StringComparison.Ordinal);

  private static string Fixture(string name)
  {
    using Stream stream = typeof(SavedFormatTests).Assembly.GetManifestResourceStream("Fixtures/" + name);
    if (stream == null)
      throw new InvalidOperationException("Fixtures/" + name + " is not built into the tests");
    using StreamReader reader = new(stream, Encoding.UTF8);
    return Lines(reader.ReadToEnd());
  }

  private static string Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');

  private static readonly System.Collections.Generic.HashSet<Type> Filling = new();

  // Every saved field gets a value, so a field of every saved class is in the file.
  private static T Sample<T>()
    where T : class, new()
  {
    T value = new();
    Fill(value, 0);
    return value;
  }

  private static bool HoldsNothing(object value)
  {
    foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
      if (!field.IsInitOnly && field.GetValue(value) != null)
        return false;
    return true;
  }

  private static void Fill(object target, int depth)
  {
    foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
      if (!field.IsInitOnly)
        field.SetValue(target, Value(field.FieldType, field.Name, depth));
  }

  private static object Value(Type type, string name, int depth)
  {
    if (type == typeof(string))
      return name;
    if (type == typeof(bool))
      return true;
    if (type == typeof(int))
      return 1;
    if (type == typeof(long))
      return 2L;
    if (type == typeof(float))
      return 0.5f;
    if (type == typeof(double))
      return 0.25;
    if (type.IsEnum)
    {
      Array values = Enum.GetValues(type);
      return values.GetValue(values.Length - 1);
    }
    if (depth > 8)
      return null;
    if (type.IsArray)
    {
      Array items = Array.CreateInstance(type.GetElementType(), 1);
      items.SetValue(Value(type.GetElementType(), name, depth + 1), 0);
      return items;
    }
    if (type.IsGenericType && typeof(IList).IsAssignableFrom(type))
    {
      IList items = (IList)Activator.CreateInstance(type);
      items.Add(Value(type.GetGenericArguments()[0], name, depth + 1));
      return items;
    }
    if (type.IsClass)
    {
      // A class that holds itself further down (a text run's If holds runs) is filled once.
      if (!Filling.Add(type))
        return null;
      object value = Activator.CreateInstance(type);
      Fill(value, depth + 1);
      Filling.Remove(type);
      // Cut off with nothing in it (only lists, all past the depth), it would
      // read back with its empty lists made again: leave it out instead.
      return HoldsNothing(value) ? null : value;
    }
    throw new InvalidOperationException("No sample value for " + type.Name + " " + name);
  }
}
