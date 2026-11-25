using Element.Configuration;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Presentation.OverlayEditor;

// The dock's variable: the keypad types into the picked number, values go
// after the picked piece or in its place, a condition's slots open in turn,
// and the field takes a change only while it fits.
internal static class VariableEditingTests
{
  [Suite("the dock edits a variable piece by piece, into slots, and keeps a field that doesn't fit")]
  public static void Run()
  {
    TheKeypadTypesNumbers();
    ValuesGoInPlace();
    ConditionsOpenSlotByslot();
    MadeValuesOpenLikeAnyVariable();
  }

  private static (FormulaEditing Editing, OverlayVariable[] Saved) Field(
    OverlayValueType type,
    string start,
    ICustomValueBook book = null
  )
  {
    OverlayVariable[] saved = { OverlayVariable.Parse(start) };
    FormulaEditing editing = new(book);
    editing.Activate(new FormulaSlot("field", type, () => saved[0], variable => saved[0] = variable, null));
    return (editing, saved);
  }

  private static void TheKeypadTypesNumbers()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    Check(editing.SelectedPiece?.Text == "40", "a field opens with its last piece picked");
    editing.Press('5');
    Check(saved[0].Serialize() == "405", "digits go into the picked number");
    editing.Press('*');
    editing.Press('2');
    Check(saved[0].Serialize() == "405*2", "an operator joins the next number");
    editing.Press('.');
    Check(!editing.CanPress('.') && editing.Press('5') && saved[0].Serialize() == "405*2.5", "one point per number");
    editing.Backspace();
    editing.Backspace();
    editing.Backspace();
    Check(
      saved[0].Serialize() == "405*2" || saved[0].Serialize() == "405",
      "backspace takes digits, then pieces: " + saved[0].Serialize()
    );
    Check(!editing.CanPress('≥'), "comparisons only go in a condition's operator");
  }

  private static void ValuesGoInPlace()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.Combo));
    Check(saved[0].Serialize() == "combo", "in a number field a value replaces the picked one");
    Check(!editing.CanDrop(OverlayValueCatalog.Get(OverlayValueId.SongTitle)), "words don't fit a number");
    Check(
      editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.SongTitle)) == FormulaDropResult.Refused,
      "and are refused"
    );
    (FormulaEditing text, OverlayVariable[] words) = Field(OverlayValueType.Any, "\"Acc \"");
    text.Drop(text.Slot, OverlayValueCatalog.Get(OverlayValueId.Accuracy));
    Check(words[0].Serialize() == "\"Acc \" {accuracy}", "in Any a value goes beside the picked one");
    text.Drop(text.Slot, OverlayValueCatalog.Get(OverlayValueId.MakeText));
    Check(text.SelectedPiece?.Kind == OverlayPieceKind.Text, "made words are picked, for the keyboard");
    text.SetText("%");
    Check(words[0].Serialize() == "\"Acc \" {accuracy} \"%\"", "the keyboard types into them");
  }

  private static void ConditionsOpenSlotByslot()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.MakeCondition));
    Check(
      saved[0].Serialize() == "40" && editing.Problem?.Slot == OverlayPiece.Left,
      "an unfinished condition isn't taken: the field keeps 40, and the dock says which slot is empty"
    );
    editing.Enter(OverlayPiece.Left);
    Check(editing.Path.Count == 2 && editing.Kind == OverlayValueType.Number, "a slot opens as a number variable");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.Combo));
    editing.Back();
    Check(editing.SelectedPiece?.Kind == OverlayPieceKind.Condition, "back out, the condition is picked again");
    editing.Enter(OverlayPiece.Compare);
    Check(
      editing.Kind == OverlayValueType.Compare
        && editing.Working[0].Op == '≥'
        && editing.Press('>')
        && editing.Press('≥'),
      "the operator starts as ≥ and takes another comparison"
    );
    editing.Back();
    foreach (
      (int slot, string typed) in new[]
      {
        (OverlayPiece.Right, "100"),
        (OverlayPiece.Result, "60"),
        (OverlayPiece.Default, "40"),
      }
    )
    {
      editing.Enter(slot);
      foreach (char key in typed)
        editing.Press(key);
      editing.Back();
    }
    Check(
      editing.Problem == null && saved[0].Serialize() == "if({combo};>=;100;60;40)",
      "once every slot fits, the field takes it: " + saved[0].Serialize()
    );
  }

  private static OverlayValueDefinition Mine(string key) =>
    OverlayValueCatalog.TryGet("@" + key, out OverlayValueDefinition value) ? value : null;

  private static void MadeValuesOpenLikeAnyVariable()
  {
    ElementSettings settings = new();
    CustomValueBook book = new(settings);
    try
    {
      (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40", book);
      Check(
        editing.CanDrop(OverlayValueCatalog.Get(OverlayValueId.MakeCustom)),
        "a value can be made where numbers go"
      );
      editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.MakeCustom));
      string key = settings.OverlayCustomValues[0].Key;
      Check(settings.OverlayCustomValues[0].Type == "number", "it gives what the field needs");
      Check(
        editing.Path.Count == 2 && editing.Path[1].Custom == key && editing.Kind == OverlayValueType.Number,
        "and opens at once"
      );
      Check(
        saved[0].Serialize() == "40" && editing.Problem?.Kind == OverlayTypeProblemKind.Empty,
        "an empty value isn't taken yet"
      );
      Check(!editing.CanDrop(OverlayValueCatalog.Get(OverlayValueId.SongTitle)), "words don't go in a number value");
      Check(!editing.CanDrop(Mine(key)), "nor the value itself");
      editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.Combo));
      editing.Press('*');
      editing.Press('2');
      Check(
        settings.OverlayCustomValues[0].Variable == "{combo}*2",
        "what fits is kept with the settings: " + settings.OverlayCustomValues[0].Variable
      );
      Check(editing.Problem == null && saved[0].Serialize() == "{@" + key + "}", "and the field takes the value");
      editing.Back();
      editing.Rename("콤보 두 배");
      Check(
        OverlayCustomValues.NameOf(key) == "콤보 두 배" && settings.OverlayCustomValues[0].Name == "콤보 두 배",
        "renamed for every element"
      );
      (FormulaEditing color, _) = Field(OverlayValueType.Color, "#FFFFFF", book);
      Check(!color.CanDrop(Mine(key)), "a number value doesn't fit a color");
      System.Collections.Generic.List<Element.Configuration.Overlay.OverlayCustomValueSettings> before = book.Copy();
      editing.Enter(0);
      editing.Clear();
      Check(settings.OverlayCustomValues[0].Variable == "{combo}*2", "an empty value keeps what it had");
      editing.Press('7');
      book.Restore(before);
      Check(settings.OverlayCustomValues[0].Variable == "{combo}*2", "Cancel puts the made values back");
    }
    finally
    {
      OverlayCustomValues.Load(null);
    }
  }
}
