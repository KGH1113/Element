using System;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.Ui.Declarative;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// A mod's settings in the editor's property column, where the text editor
/// keeps its own: right of the preview, the rows one under another,
/// scrolling when they are taller than the screen. A row that opens a tool
/// (a color, a font, a list of choices) opens it in the column under a back
/// row naming it, as the text editor's do; the back row (or Esc) brings the
/// rows back. A screen the mod wrote with ElementUI is drawn in the column's
/// <see cref="ModColumnStyle"/>; Element's own parts draw themselves as rows.
/// </summary>
internal sealed class ModEditorColumn : IDisposable
{
  /// <summary>As wide as the text editor's column, which stands above its keypad.</summary>
  public const float Width = KeypadView.Width;

  private readonly RectTransform _frame;
  private readonly Func<IBackend> _backend;
  private readonly Action _edited;
  private readonly Action<bool> _editing;
  private readonly ScreenPanel _rows;
  private readonly ModColumnModel _model;
  private ScreenPanel _tool;
  private float _toolWidth;
  private bool _closing;

  /// <param name="frame">The editor's controls, in frame units.</param>
  /// <param name="area">Where the column stands, in frame units.</param>
  /// <param name="backend">Draws a screen in the editor's look.</param>
  /// <param name="edited">A control changed what it is bound to; null when the parts report their own changes.</param>
  /// <param name="editing">A drag started (true) or ended.</param>
  public ModEditorColumn(RectTransform frame, Rect area, Func<IBackend> backend, Action edited, Action<bool> editing)
  {
    _frame = frame;
    Area = area;
    _backend = backend;
    _edited = edited;
    _editing = editing;
    // The back row's click ends; the tool goes the frame after.
    _model = new ModColumnModel(() => _closing = true);
    _rows = new ScreenPanel("Mod editor column", frame, Color.clear, keyboard: false);
    Place(_rows.Root, area.x, area.y);
  }

  /// <summary>Where the column stands, in frame units.</summary>
  public Rect Area { get; }

  /// <summary>The rows' panel.</summary>
  public RectTransform Root => _rows.Root;

  /// <summary>The open tool's panel, back row included; null while the rows show.</summary>
  public RectTransform ToolRoot => _tool?.Root;

  /// <summary>What the open tool is named on its back row; null while the rows show.</summary>
  public string OpenTool => _tool != null ? _model.Title.Value : null;

  /// <summary>Where the column and its open tool start on the left, in frame units: the preview keeps left of it.</summary>
  public float Left => _tool != null ? Mathf.Min(Area.x, Area.xMax - _toolWidth) : Area.x;

  /// <summary>The commands of rows that switch, step or open: they go with the column.</summary>
  public ModCommandsViewModel Commands { get; } = new();

  /// <summary>
  /// Shows <paramref name="rows"/> in the column, scrolling when taller
  /// than it; <paramref name="style"/> draws the ElementUI controls inside.
  /// <paramref name="model"/> goes with the rows; null when its owner keeps it.
  /// </summary>
  public void Mount(View rows, ControlStyle style, IDisposable model)
  {
    CloseTool();
    _rows.Mount(
      _backend(),
      ScrollView(
          rows.Frame(width: Area.width, alignment: Alignment.TopLeading)
            .Padding(new Insets(0, 0, InspectorRowStyle.Gap * 2, 0))
            .ControlStyle(style)
        )
        .Frame(width: Area.width, height: Area.height),
      model,
      Area.width
    );
    Listen(_rows);
  }

  /// <summary>
  /// Opens a tool in the column, in place of the rows, under a back row
  /// naming it <paramref name="title"/>. <paramref name="tool"/> makes it,
  /// with commands that go with it. A tool wider than the column
  /// (<paramref name="width"/>) reaches further left. A list of rows scrolls
  /// when it is taller than the column; a tool that fills the column (the
  /// font list) is as tall as it; any other is as tall as it wants.
  /// </summary>
  public void Open(
    string title,
    Func<ModCommandsViewModel, View> tool,
    float width = float.NaN,
    ToolHeight height = ToolHeight.Own
  )
  {
    CloseTool();
    _toolWidth = float.IsNaN(width) ? Area.width : Mathf.Max(Area.width, width);
    _model.Title.Value = title ?? "";
    float room = Area.height - InspectorRowStyle.BackHeight - 4;
    ModCommandsViewModel commands = new();
    View content = tool(commands).Frame(width: _toolWidth, alignment: Alignment.TopLeading);
    _tool = new ScreenPanel("Mod editor tool", _frame, Color.clear, keyboard: false);
    _tool.Mount(
      _backend(),
      VStack(
          spacing: 4,
          InspectorRowStyle.Back(() => _model.Title.Value, () => _model.Back, "Tool back"),
          height switch
          {
            ToolHeight.Fill => content.Frame(height: room, alignment: Alignment.TopLeading),
            ToolHeight.Scroll => ScrollView(content)
              .Frame(width: _toolWidth, maxHeight: room, alignment: Alignment.Top),
            _ => content,
          }
        )
        .Frame(width: _toolWidth, alignment: Alignment.TopLeading),
      commands,
      _toolWidth
    );
    Place(_tool.Root, Area.xMax - _toolWidth, Area.y);
    Listen(_tool);
    _rows.Root.gameObject.SetActive(false);
  }

  /// <summary>The rows come back; false when no tool was open.</summary>
  public bool CloseTool()
  {
    _closing = false;
    if (_tool == null)
      return false;
    _tool.Dispose();
    _tool = null;
    _rows.Root.gameObject.SetActive(true);
    return true;
  }

  /// <summary>Whether a field in the column (or its tool) has the keyboard.</summary>
  public bool Typing =>
    ModKeyboard.FocusedField(_rows.Root) != null || (_tool != null && ModKeyboard.FocusedField(_tool.Root) != null);

  /// <summary>Whether something of the column's is open: a tool, or a list a control opened.</summary>
  public bool HasPopover =>
    _tool != null || (_rows.Host != null && _rows.Host.Root != null && _rows.Host.Root.HasOverlay);

  /// <summary>Closes what is on top: a list a control opened, then the tool. False when nothing was open.</summary>
  public bool CloseTopPopover()
  {
    if (_tool != null && _tool.Host != null && _tool.Host.HandleEscape())
      return true;
    if (CloseTool())
      return true;
    return _rows.Host != null && _rows.Host.HandleEscape();
  }

  /// <summary>Once a frame: controls shown for the first time get the column's cursor.</summary>
  public void Tick()
  {
    if (_closing)
      CloseTool();
    _rows.Sync();
    _tool?.Sync();
  }

  private void Listen(ScreenPanel panel)
  {
    if (panel.Host == null || panel.Host.Root == null)
      return;
    if (_edited != null)
      panel.Host.Root.Edited += _edited;
    if (_editing != null)
      panel.Host.Root.Editing += _editing;
  }

  private static void Place(RectTransform root, float x, float y) =>
    root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));

  public void Dispose()
  {
    _tool?.Dispose();
    _tool = null;
    _rows.Dispose();
    Commands.Dispose();
    _model.Dispose();
  }

  /// <summary>The back row's name and command.</summary>
  private sealed class ModColumnModel : ViewModel
  {
    public ModColumnModel(Action back) => Back = Command(back);

    public readonly State<string> Title = new("");

    public Command Back { get; }
  }
}
