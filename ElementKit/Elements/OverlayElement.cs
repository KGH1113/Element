using ElementUI;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// An overlay element your mod draws and Element places. Players add it from
/// Add Element → More, move and align it like Element's own elements, and
/// change it on an editing screen you build from Element's parts.
/// </summary>
/// <remarks>
/// <para>
/// Write it like any part of your mod: read the game, use your own Harmony
/// patches and state. Element makes one instance per placed element (and one
/// more for the editing screen's preview) with the constructor that takes
/// nothing, then calls, in order: <see cref="LoadSettings"/>,
/// <see cref="OnCreate"/>, <see cref="OnSettingsChanged"/>, then
/// <see cref="OnUpdate"/> every frame while it is shown.
/// </para>
/// <para>
/// Element doesn't have to be installed. Without it nothing makes your
/// element, so keep the rest of your mod from touching this class: let the
/// element read your mod's state, not the other way round.
/// </para>
/// <para>Mark the class with <see cref="ElementAttribute"/>. Most elements
/// derive from <see cref="OverlayElement{TSettings}"/>, which saves settings for you.</para>
/// </remarks>
public abstract class OverlayElement
{
  private RectTransform _root;

  /// <summary>
  /// Where you draw: a UI object Element places on the overlay. Its pivot is
  /// its top left; set its size with <see cref="Size"/>. Add your UGUI under it.
  /// </summary>
  public RectTransform Root => _root;

  /// <summary>Element's drawing helpers: text in the overlay's font and rounded boxes.</summary>
  public Drawing Drawing { get; private set; }

  /// <summary>
  /// True while an Element editor shows this element: the layout editor or the
  /// editing screen's preview. Show sample data when you have nothing to show,
  /// so players can see, place and style the element outside a level.
  /// </summary>
  public bool IsPreview { get; private set; }

  /// <summary>
  /// The element's size in pixels at 1080p. Element keeps the point it grows
  /// from still when the size changes. Set it whenever your content's size changes.
  /// </summary>
  public Vector2 Size
  {
    get => _root != null ? _root.sizeDelta : Vector2.zero;
    set
    {
      if (_root != null)
        _root.sizeDelta = new Vector2(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
    }
  }

  /// <summary>Build your UI under <see cref="Root"/>. Settings are loaded already.</summary>
  protected virtual void OnCreate() { }

  /// <summary>Every frame while the element is shown: read your mod's state and update what you drew.</summary>
  protected virtual void OnUpdate() { }

  /// <summary>After the settings were loaded or a player changed them: show them.</summary>
  protected virtual void OnSettingsChanged() { }

  /// <summary>
  /// Builds the editing screen from <paramref name="page"/>'s parts. It is called
  /// on the preview's instance, so change its settings in the parts' callbacks;
  /// Element shows the change, records undo, and saves it when the player is done.
  /// It may be called again with a fresh page, such as after undo.
  /// </summary>
  /// <remarks>
  /// An element of <see cref="OverlayElement{TSettings}"/> may write its screen
  /// with ElementUI instead (<see cref="OverlayElement{TSettings}.Editor"/>);
  /// then this isn't called. Override one of the two.
  /// </remarks>
  protected virtual void OnEdit(EditorPage page) { }

  /// <summary>The element is going away: release what you made outside <see cref="Root"/>.</summary>
  protected virtual void OnDestroy() { }

  /// <summary>Your settings as text, kept by Element with the layout and profiles.</summary>
  protected virtual string SaveSettings() => string.Empty;

  /// <summary>Reads what <see cref="SaveSettings"/> wrote; empty for a new element.</summary>
  protected virtual void LoadSettings(string data) { }

  // Element drives the element through these.
  internal void Bind(RectTransform root, Drawing drawing, bool preview)
  {
    _root = root;
    Drawing = drawing;
    IsPreview = preview;
  }

  internal void SetPreview(bool preview) => IsPreview = preview;

  internal void RunCreate() => OnCreate();

  internal void RunUpdate() => OnUpdate();

  internal void RunSettingsChanged() => OnSettingsChanged();

  internal void RunEdit(EditorPage page) => OnEdit(page);

  /// <summary>
  /// The editing screen as ElementUI views and the view model they read, or
  /// null when the element builds it with <see cref="OnEdit"/>.
  /// </summary>
  internal virtual View RunEditor(bool korean, out ViewModel model)
  {
    model = null;
    return null;
  }

  internal void RunDestroy() => OnDestroy();

  internal string RunSave() => SaveSettings() ?? string.Empty;

  internal void RunLoad(string data) => LoadSettings(data ?? string.Empty);
}

/// <summary>
/// An overlay element whose settings are one class of yours. Element saves
/// them per element with the layout, so moving, undo, profiles and several
/// copies of the element just work.
/// </summary>
/// <typeparam name="TSettings">
/// A <c>[Serializable]</c> class with public fields, as Unity's <c>JsonUtility</c>
/// saves it: numbers, bools, strings, <c>Color</c>, <c>Vector2</c>, lists and
/// nested serializable classes. Field initializers are the defaults; a field
/// added in a later version starts at its default in saved elements.
/// </typeparam>
/// <example>
/// The editing screen built from <see cref="EditorPage"/>'s parts:
/// <code>
/// [Serializable]
/// public sealed class BarsSettings
/// {
///     public Color Left = new(1f, .62f, .26f);
///     public int Groups = 16;
/// }
///
/// [Element("bars", "진행 막대", "Progress bars")]
/// public sealed class BarsElement : OverlayElement&lt;BarsSettings&gt;
/// {
///     protected override void OnCreate() => Size = new Vector2(480, 96);
///
///     protected override void OnEdit(EditorPage page)
///     {
///         page.ColorPicker(page.L("왼손 색", "Left hand"), Settings.Left, color => Settings.Left = color);
///         page.Slider(page.L("묶음 수", "Groups"), Settings.Groups, 2, 64, value => Settings.Groups = (int)value, step: 1);
///     }
/// }
/// </code>
/// Or written with ElementUI, bound to the settings:
/// <code>
/// protected override View Editor(EditorViewModel&lt;BarsSettings&gt; vm) =>
///     VStack(
///         ColorPicker(vm.L("왼손 색", "Left hand"), Bind(() =&gt; vm.Settings.Left)),
///         Stepper(vm.L("묶음 수", "Groups"), Bind(() =&gt; vm.Settings.Groups), 2, 64));
/// </code>
/// </example>
public abstract class OverlayElement<TSettings> : OverlayElement
  where TSettings : class, new()
{
  /// <summary>This element's settings. Change them in <see cref="OverlayElement.OnEdit"/>'s callbacks.</summary>
  public TSettings Settings { get; private set; } = new();

  /// <summary><see cref="Settings"/> as JSON.</summary>
  protected sealed override string SaveSettings() => JsonUtility.ToJson(Settings);

  /// <summary>Fresh defaults, then what <paramref name="data"/> holds over them.</summary>
  protected sealed override void LoadSettings(string data)
  {
    TSettings settings = new();
    if (!string.IsNullOrEmpty(data))
      JsonUtility.FromJsonOverwrite(data, settings);
    Settings = settings;
  }

  /// <summary>
  /// The editing screen written with ElementUI: controls bound to
  /// <c>vm.Settings</c> (<c>Slider(vm.L("너비", "Width"), Bind(() =&gt; vm.Settings.Width), 80, 960)</c>)
  /// and Element's own pickers from <see cref="EditorParts"/>. Element shows it
  /// in a column under the preview, scrolling when it is taller than the room;
  /// it records each change for undo, shows it on the preview and saves it when
  /// the player is done. Return null, as the default does, to build the screen
  /// with <see cref="OverlayElement.OnEdit"/> instead; override one of the two.
  /// </summary>
  /// <param name="vm">The settings the screen edits; see <see cref="EditorViewModel{TSettings}"/>.</param>
  protected virtual View Editor(EditorViewModel<TSettings> vm) => null;

  internal sealed override View RunEditor(bool korean, out ViewModel model)
  {
    EditorViewModel<TSettings> vm = new(this, korean);
    View view = Editor(vm);
    if (view == null)
    {
      vm.Dispose();
      model = null;
      return null;
    }
    model = vm;
    return view;
  }
}
