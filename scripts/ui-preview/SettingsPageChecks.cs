using Element.Presentation.Settings;
using Element.Presentation.Settings.Window;
using System;
using System.Linq;
using System.Reflection;
using Element.Configuration;
using UnityEngine;

namespace Element.Editor
{
  /// <summary>
  /// Marks a check that runs while a settings page is shown in the preview:
  /// <c>[PageCheck("_tweaks")] static int Check(SettingsCheck check)</c>, returning
  /// how many assertions it made. Each page keeps its checks in a file of its own.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class PageCheckAttribute : Attribute
  {
    public PageCheckAttribute(string page) => Page = page;

    public string Page { get; }
  }

  /// <summary>What a page check works with: the open window, its view model, and a way to photograph it.</summary>
  internal sealed class SettingsCheck
  {
    public SettingsWindowScreen Panel;
    public SettingsWindowViewModel Model;
    public GameObject Root;
    public ElementSettings Settings;
    public int Width, Height;
    public string Name;
    public SettingsFeatures Features;
    public Action<string> Save;

    /// <summary>Runs ElementUI's pending updates and layout, as a frame would.</summary>
    public void Settle()
    {
      for (int i = 0; i < 4; i++)
      {
        ElementUI.Runtime.Flush(1);
        Canvas.ForceUpdateCanvases();
      }
    }

    public void Assert(bool condition, string message)
    {
      if (!condition)
        throw new Exception("UI preview check failed: " + message);
    }
  }

  internal static class SettingsPageChecks
  {
    public static int Run(string page, SettingsCheck check)
    {
      int count = 0;
      foreach (MethodInfo method in typeof(SettingsPageChecks).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(method => method.GetCustomAttribute<PageCheckAttribute>()?.Page == page))
        count += (int)method.Invoke(null, new object[] { check });
      return count;
    }
  }
}
