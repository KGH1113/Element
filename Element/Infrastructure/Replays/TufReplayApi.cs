using System;
using System.Reflection;

namespace Element.Infrastructure.Replays;

/// <summary>
/// TUFReplay's replay API, bound by reflection so Element runs without TUFReplay.
/// TUFReplay may load before or after Element. Unity main thread.
/// </summary>
/// <remarks>
/// TUFReplay calls a registered provider's public methods by name: it saves what
/// <c>CaptureRunState</c> returns with every run it records, and calls <c>OnReplayBegin</c>,
/// <c>OnReplayKey</c> and the others when it replays a run in the game or renders it to video.
/// See TUFReplay's docs/replay-api.md.
/// </remarks>
internal sealed class TufReplayApi : IDisposable
{
  private const string AssemblyName = "TUFReplay";
  private const string TypeName = "TUFReplay.ReplayApi";
  private const int RequiredApiVersion = 1;
  private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

  private MethodInfo _register;
  private MethodInfo _unregister;
  private MethodInfo _isRegistered;
  private Func<int> _renderWidth;
  private Func<int> _renderHeight;

  // Set on whatever thread loads an assembly; the next main-thread resolve reads it.
  private volatile bool _lookAgain = true;
  private bool _disposed;

  public TufReplayApi() => AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

  public bool Resolved => _register != null;

  /// <summary>The video's width while TUFReplay renders a run, otherwise 0.</summary>
  public int RenderWidth => Call(_renderWidth);

  /// <summary>The video's height while TUFReplay renders a run, otherwise 0.</summary>
  public int RenderHeight => Call(_renderHeight);

  /// <summary>Binds the API once TUFReplay is loaded; cheap until a new assembly loads.</summary>
  public bool TryResolve()
  {
    if (Resolved)
      return true;
    if (!_lookAgain || _disposed)
      return false;
    _lookAgain = false;
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      if (IsTufReplay(assembly) && Bind(assembly.GetType(TypeName, false)))
        return true;
    return false;
  }

  /// <summary>
  /// Registers <paramref name="provider"/>. False when TUFReplay refused it; TUFReplay then
  /// writes the reason to the mod log.
  /// </summary>
  public bool Register(string id, object provider) => Invoke(_register, id, provider) is true;

  public bool IsRegistered(string id) => Invoke(_isRegistered, id) is true;

  public void Unregister(string id) => Invoke(_unregister, id);

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
  }

  private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs loaded)
  {
    if (IsTufReplay(loaded.LoadedAssembly))
      _lookAgain = true;
  }

  private bool Bind(Type type)
  {
    if (type?.GetField("ApiVersion", Static)?.GetValue(null) is not int version || version < RequiredApiVersion)
      return false;
    MethodInfo register = type.GetMethod("Register", Static, null, new[] { typeof(string), typeof(object) }, null);
    MethodInfo unregister = type.GetMethod("Unregister", Static, null, new[] { typeof(string) }, null);
    MethodInfo isRegistered = type.GetMethod("IsRegistered", Static, null, new[] { typeof(string) }, null);
    if (register == null || unregister == null || isRegistered == null)
      return false;
    _unregister = unregister;
    _isRegistered = isRegistered;
    _renderWidth = Getter<int>(type, "RenderWidth");
    _renderHeight = Getter<int>(type, "RenderHeight");
    _register = register;
    return true;
  }

  private static bool IsTufReplay(Assembly assembly)
  {
    try
    {
      return assembly.GetName().Name == AssemblyName;
    }
    catch (Exception)
    {
      // Dynamic or partly loaded assemblies can refuse to describe themselves.
      return false;
    }
  }

  private static Func<T> Getter<T>(Type type, string name)
  {
    MethodInfo getter = type.GetProperty(name, Static)?.GetGetMethod();
    return getter?.ReturnType == typeof(T) ? (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), getter, false) : null;
  }

  private static int Call(Func<int> getter)
  {
    try
    {
      return getter?.Invoke() ?? 0;
    }
    catch (Exception)
    {
      return 0;
    }
  }

  private static object Invoke(MethodInfo method, params object[] arguments)
  {
    try
    {
      return method?.Invoke(null, arguments);
    }
    catch (Exception)
    {
      return null;
    }
  }
}
