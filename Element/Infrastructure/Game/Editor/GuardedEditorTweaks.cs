using System;
using Element.Configuration;
using UnityEngine.SceneManagement;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// The editor tweaks as the game patches reach them. An error stops the tweaks
/// for the rest of the session and gives the editor its own controls back.
/// </summary>
internal sealed class GuardedEditorTweaks : IDisposable
{
  private readonly EditorTweaksController _editor;
  private readonly Action<string, Exception> _logException;
  private bool _disposed,
    _failed;

  /// <param name="settings">Normalized when they load; the settings window writes only valid values.</param>
  public GuardedEditorTweaks(EditorTweaksOptions settings, Action<string, Exception> logException)
  {
    _logException = logException;
    _editor = new(settings);
    SceneManager.sceneUnloaded += SceneUnloaded;
  }

  private bool On => !_disposed && !_failed;

  /// <summary>The settings changed: the selection text follows at once.</summary>
  public void Apply()
  {
    if (_disposed || _failed)
      return;
    _editor.ApplySettings();
    try
    {
      _editor.Tick(ADOBase.editor);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void AfterEditorUpdate(scnEditor editor)
  {
    if (!On)
      return;
    try
    {
      _editor.Tick(editor);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void InvalidateEditor()
  {
    if (!_disposed)
      _editor.Invalidate();
  }

  public void OnFloorVisibilityChanged(scrFloor floor)
  {
    if (!_disposed)
      _editor.OnFloorVisibilityChanged(floor);
  }

  /// <summary>Before the editor flips or rotates <paramref name="floor"/>; null when nothing follows it.</summary>
  public EditorTweaksController.Mutation BeginEditorTransform(
    scnEditor editor,
    scrFloor floor,
    string method,
    object[] args
  )
  {
    if (!On)
      return null;
    try
    {
      bool direction = method == "RotateFloor180" || (bool)args[1];
      return _editor.BeginMutation(editor, floor, method, direction);
    }
    catch (Exception exception)
    {
      Fail(exception);
      return null;
    }
  }

  public void AfterEditorTransform(
    scnEditor editor,
    bool remakePath,
    bool ranOriginal,
    EditorTweaksController.Mutation mutation
  )
  {
    if (!On || !ranOriginal)
      return;
    try
    {
      _editor.FinishMutation(editor, mutation, remakePath);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  private void Fail(Exception exception)
  {
    if (_failed)
      return;
    _failed = true;
    _logException("Editor tweaks stopped after an error; original editor controls remain available", exception);
    try
    {
      _editor.Dispose();
    }
    catch (Exception cleanup)
    {
      _logException("Restore editor selection text", cleanup);
    }
  }

  private void SceneUnloaded(Scene scene) => _editor.Invalidate();

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    SceneManager.sceneUnloaded -= SceneUnloaded;
    _editor.Dispose();
  }
}
