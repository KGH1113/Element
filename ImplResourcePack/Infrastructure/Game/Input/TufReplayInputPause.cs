using System;
using System.Reflection;

namespace ImplResourcePack.Infrastructure.Game.Input;

/// <summary>
/// Asks TUFReplay to pause its native input while the settings take the keyboard. TUFReplay
/// reads keys from the OS rather than the game, so blocking game input alone would still put
/// settings keystrokes into its recordings and let a running replay type into the window.
/// Optional: with no TUFReplay, or a version without the API, this does nothing.
/// </summary>
internal sealed class TufReplayInputPause : IDisposable
{
  private const string Owner = "ImplResourcePack.Settings";
  private const string AssemblyName = "TUFReplay";
  private const string ApiTypeName = "TUFReplay.NativeInputPause";
  private const int SupportedApiVersion = 1;

  private readonly Action<string> _logWarning;
  private readonly Action<string, Exception> _logException;
  private Action<string> _request;
  private Action<string> _release;
  private bool _unsupported;
  private bool _wanted;
  private bool _held;

  public TufReplayInputPause(Action<string> logWarning, Action<string, Exception> logException)
  {
    _logWarning = logWarning;
    _logException = logException;
  }

  /// <summary>Cheap when unchanged, so it can follow the keyboard state every frame.</summary>
  public void Set(bool paused)
  {
    if (paused == _wanted)
      return;
    _wanted = paused;
    if (paused)
      Request();
    else
      Release();
  }

  public void Dispose()
  {
    _wanted = false;
    Release();
  }

  private void Request()
  {
    if (_held || !TryResolve())
      return;
    try
    {
      _request(Owner);
      _held = true;
    }
    catch (Exception exception)
    {
      Disable("Pause TUFReplay native input", exception);
    }
  }

  private void Release()
  {
    if (!_held)
      return;
    _held = false;
    try
    {
      _release(Owner);
    }
    catch (Exception exception)
    {
      Disable("Resume TUFReplay native input", exception);
    }
  }

  // Runs only when the settings take the keyboard, so scanning assemblies again
  // while TUFReplay is absent costs nothing during play.
  private bool TryResolve()
  {
    if (_request != null)
      return true;
    if (_unsupported)
      return false;

    bool tufReplayLoaded = false;
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (!string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.Ordinal))
        continue;
      tufReplayLoaded = true;
      Type api = assembly.GetType(ApiTypeName, throwOnError: false);
      if (api != null && TryBind(api))
        return true;
    }

    if (tufReplayLoaded)
    {
      _unsupported = true;
      _logWarning(
        "[Input] This TUFReplay version cannot pause its input for other mods, so keys pressed in Settings can appear in its recordings. Update TUFReplay to fix this."
      );
    }
    return false;
  }

  private bool TryBind(Type api)
  {
    FieldInfo version = api.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
    if (!(version?.GetValue(null) is int value) || value != SupportedApiVersion)
      return false;

    Action<string> request = Bind(api, "Request");
    Action<string> release = Bind(api, "Release");
    if (request == null || release == null)
      return false;

    _request = request;
    _release = release;
    return true;
  }

  private static Action<string> Bind(Type api, string name)
  {
    MethodInfo method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
    return method == null
      ? null
      : (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method, throwOnBindFailure: false);
  }

  private void Disable(string context, Exception exception)
  {
    _request = null;
    _release = null;
    _unsupported = true;
    _logException(context, exception);
  }
}
