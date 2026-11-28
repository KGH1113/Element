using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A shortcut as keycaps in a field: a click records the next key pressed
/// with at most one modifier, Esc cancels, and one icon button beside it
/// removes or resets it. The hint under it says how to record, or why the
/// key pressed can't be used. When the field and its button don't fit on one
/// line, the button goes under the field.
/// </summary>
internal sealed class ShortcutField : View<ShortcutFieldViewModel>
{
  private const float FieldHeight = 44,
    Radius = 10;

  private readonly UiIconKind _actIcon;
  private readonly string _actHint;
  private readonly string _name;

  /// <param name="actIcon">The button beside the field: Close to remove, Reset to restore the default.</param>
  /// <param name="actHint">What that button does, shown while the pointer is over it; null shows nothing.</param>
  /// <param name="name">Names the field's button, so a screen with several finds each; null keeps the default.</param>
  public ShortcutField(ShortcutFieldViewModel vm, UiIconKind actIcon, string actHint = null, string name = null)
    : base(vm)
  {
    _actIcon = actIcon;
    _actHint = actHint;
    _name = name;
  }

  public override View Body =>
    VStack(
      spacing: 0,
      Flow(
        8,
        Button(
            If(
                () => VM.ShowsKeys.Value,
                Keycaps,
                () =>
                  Text(() => VM.Prompt.Value)
                    .Foreground(() => VM.Recording ? Colors.Foreground : Colors.Muted)
                    .Font(Font.Body.Sized(15))
              )
              .Padding(0, 12)
              .Frame(maxHeight: float.PositiveInfinity),
            () => VM.Record,
            ButtonStyle.Plain
          )
          .ButtonStyle(SurfaceHover.Of(Radius))
          .Configure<Selectable, bool>(() => VM.Recording, KeepFocus())
          .Configure<GameObject>(button => button.name = _name ?? button.name)
          .Frame(height: FieldHeight)
          .Background(Colors.Background, Radius, Colors.Line),
        ActButton()
      ),
      If(
        () => VM.Recording,
        () => Text(() => VM.Hint).Font(Font.Caption).Foreground(Colors.Muted).Padding(new Insets(10, 0, 0, 0))
      )
    );

  private View ActButton()
  {
    View button = Button(
        Image(ElementIcons.Of(_actIcon), 24).Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        () => VM.Act,
        ButtonStyle.Plain
      )
      .ButtonStyle(SurfaceHover.Of(Radius))
      .Frame(width: FieldHeight, height: FieldHeight)
      .Background(Colors.Background, Radius, Colors.Line);
    return string.IsNullOrEmpty(_actHint) ? button : button.Hint(_actHint);
  }

  // The modifier (as its macOS symbol with its side, or in words), a plus, and the key.
  private View Keycaps() =>
    HStack(
      spacing: 0,
      If(
        () => VM.HasModifier.Value,
        () =>
          HStack(
              spacing: 8,
              Keycap(
                If(
                  () => VM.ModifierSymbol.Value,
                  () =>
                    HStack(
                      spacing: 2,
                      Text(() => VM.ModifierSide.Value).Font(Font.Caption.Sized(11)).Foreground(Colors.Muted),
                      new KeySymbol(() => VM.Modifier.Value)
                    ),
                  () => Text(() => VM.ModifierText.Value).Font(Font.Body.Sized(15))
                )
              ),
              Text("+").Font(Font.Caption).Foreground(Colors.Muted)
            )
            .Padding(new Insets(0, 0, 0, 8))
      ),
      Keycap(Text(() => VM.KeyText.Value).Font(Font.Body.Sized(15)))
    );

  private static View Keycap(View content) =>
    content.Padding(4, 10).Frame(minWidth: 44, minHeight: 30).Background(Colors.Line, 8);

  // Recording takes the keyboard away from the field, so Enter or Space can't
  // press it again; when recording ends the field takes the keyboard back.
  private static Action<Selectable, bool> KeepFocus()
  {
    bool wasRecording = false;
    return (field, recording) =>
    {
      EventSystem events = EventSystem.current;
      if (recording)
        events?.SetSelectedGameObject(null);
      else if (wasRecording && events != null && field.gameObject.activeInHierarchy)
        events.SetSelectedGameObject(field.gameObject);
      wasRecording = recording;
    };
  }

  /// <summary>A macOS modifier symbol, drawn as vectors: the overlay font has no ⌘ ⌥ glyphs.</summary>
  private sealed class KeySymbol : NativeView
  {
    private const float Side = 22;
    private readonly Expression<Func<KeyCode>> _key;
    private UiKeySymbol _symbol;

    public KeySymbol(Expression<Func<KeyCode>> key) => _key = key;

    protected override void Build(RectTransform rect)
    {
      _symbol = rect.gameObject.AddComponent<UiKeySymbol>();
      _symbol.raycastTarget = false;
      _symbol.color = UguiTheme.Color(ColorToken.Foreground);
      Watch(_key, key => _symbol.SetKey(key));
    }

    protected override Size PreferredSize(Proposal proposal) => new(Side, Side);
  }
}
