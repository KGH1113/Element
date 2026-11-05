using System;
using System.Reflection;
using Element.Application;

namespace Element.Infrastructure.Rendering;

/// <summary>
/// OrbitRender's overlay API, bound by reflection so Element runs without it.
/// OrbitRender may load before or after Element. Unity main thread.
/// </summary>
internal sealed class OrbitRenderApi : IRenderVideo, IDisposable
{
  private const string AssemblyName = "OrbitRender";
  private const string TypeName = "OrbitRender.OverlayApi";
  private const int RequiredApiVersion = 1;
  private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

  private MethodInfo _register;
  private MethodInfo _unregister;
  private MethodInfo _isRegistered;
  private Func<bool> _isRendering;
  private PropertyInfo _width;
  private PropertyInfo _height;
  private PropertyInfo _videoTicks;

  // Set on whatever thread loads an assembly; the next main-thread resolve reads it.
  private volatile bool _lookAgain = true;
  private bool _disposed;

  public OrbitRenderApi() => AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

  public bool Resolved => _register != null;

  // A compiled getter, not reflection: the overlay reads this every frame.
  /// <summary>False when OrbitRender is not loaded, so plays count as usual.</summary>
  public bool IsRendering => Call(_isRendering);

  public int Width => Read(_width) is int value ? value : 0;
  public int Height => Read(_height) is int value ? value : 0;

  /// <summary>The replay's video time in 100 ns units, or null outside a replay render.</summary>
  public long? VideoTicks => Read(_videoTicks) is long value ? value : null;

  /// <summary>Binds the API once OrbitRender is loaded; cheap until a new assembly loads.</summary>
  public bool TryResolve()
  {
    if (Resolved)
      return true;
    if (!_lookAgain || _disposed)
      return false;
    _lookAgain = false;
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      if (IsOrbitRender(assembly) && Bind(assembly.GetType(TypeName, false)))
        return true;
    return false;
  }

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
    if (IsOrbitRender(loaded.LoadedAssembly))
      _lookAgain = true;
  }

  private bool Bind(Type type)
  {
    if (type?.GetField("ApiVersion", Static)?.GetValue(null) is not int version || version < RequiredApiVersion)
      return false;
    MethodInfo register = type.GetMethod("Register", Static, null, new[] { typeof(string), typeof(object) }, null);
    if (register == null)
      return false;
    _unregister = type.GetMethod("Unregister", Static, null, new[] { typeof(string) }, null);
    _isRegistered = type.GetMethod("IsRegistered", Static, null, new[] { typeof(string) }, null);
    _isRendering = Getter<bool>(type, "IsRendering");
    _width = type.GetProperty("Width", Static);
    _height = type.GetProperty("Height", Static);
    _videoTicks = type.GetProperty("VideoTicks", Static);
    _register = register;
    return true;
  }

  private static bool IsOrbitRender(Assembly assembly)
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

  private static bool Call(Func<bool> getter)
  {
    try
    {
      return getter != null && getter();
    }
    catch (Exception)
    {
      return false;
    }
  }

  private static object Read(PropertyInfo property)
  {
    try
    {
      return property?.GetValue(null);
    }
    catch (Exception)
    {
      return null;
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
