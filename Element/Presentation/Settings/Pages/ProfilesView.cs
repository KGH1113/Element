using System;
using System.Linq.Expressions;
using Element.Configuration.Profiles;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Profiles: how many, Import and New profile; a card per profile in three,
/// two or one columns; the last action's message under the cards; and after
/// an import, the fonts this PC lacks.
/// </summary>
internal sealed class ProfilesView : View<ProfilesModel>
{
  internal const float CardHeight = 140;
  private const float ActionSize = 30;

  public ProfilesView(ProfilesModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      ViewThatFits(Heading(true).Frame(minWidth: 440, maxWidth: float.PositiveInfinity), Heading(false)),
      // Three columns where three fit; 226 keeps the widest window (936) at three.
      Grid(minColumnWidth: 192, spacing: 12, maxColumns: 3, ForEach(() => VM.Cards, card => card.Id, Card)),
      // Under the cards, so a message coming or going never moves them. Each part
      // brings its own gap, so one that isn't shown leaves none.
      VStack(
        spacing: 0,
        If(
          () => VM.HasMessage.Value,
          () =>
            Text(() => VM.Message)
              .Font(Font.Caption)
              .Foreground(() => VM.MessageIsProblem ? Colors.Danger : Colors.Accent)
              .Padding(new Insets(4, 0, 12, 0))
        ),
        If(() => VM.Fonts.Visible.Value, () => FontCard().Padding(new Insets(4, 0, 0, 0)))
      )
    );

  // ---- Heading: the count, Import and New profile; narrow windows put the buttons on a row of their own ----

  private View Heading(bool wide)
  {
    View count = Text(() => VM.Count).Font(Font.Heading);
    // Import needs the system's file window; without it the button is left out.
    View import = VM.FilesSupported
      ? Button(Text(L("가져오기", "Import")).Frame(maxWidth: float.PositiveInfinity), () => VM.Import).Frame(height: 44)
      : null;
    View create = Button(
        Text(L("새 프로필", "New profile")).Frame(maxWidth: float.PositiveInfinity),
        () => VM.NewProfile,
        ButtonStyle.Prominent
      )
      .Frame(height: 44)
      .Popover(Bind(() => VM.Creating.Open), NewProfilePanel);
    return wide
      ? HStack(spacing: 8, count, Spacer(), import?.Frame(width: 120), create.Frame(width: 156))
      : VStack(
        spacing: 8,
        count,
        HStack(
          spacing: 8,
          import?.Frame(maxWidth: float.PositiveInfinity),
          create.Frame(maxWidth: float.PositiveInfinity)
        )
      );
  }

  // The panel under New profile: it starts from the initial settings.
  private View NewProfilePanel() =>
    VStack(
        spacing: 0,
        Text(L("처음 설정으로 시작해요.", "Starts with the initial settings."))
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        HStack(
            spacing: 8,
            TextField(Bind(() => VM.Creating.Name), L("프로필 이름", "Profile name"))
              .CharacterLimit(ElementProfile.MaximumNameLength)
              .OnSubmit(_ => VM.Creating.Submit())
              .FocusOn(() => VM.Creating.FocusName)
              .Frame(maxWidth: float.PositiveInfinity),
            Button(
                Text(L("만들기", "Create")).Frame(maxWidth: float.PositiveInfinity),
                () => VM.Creating.Create,
                ButtonStyle.Prominent
              )
              .Frame(width: 100)
          )
          .Padding(new Insets(8, 0, 0, 0)),
        If(
          () => VM.Creating.Problem.Value.Length > 0,
          () =>
            Text(() => VM.Creating.Problem.Value)
              .Font(Font.Caption)
              .Foreground(Colors.Danger)
              .Padding(new Insets(8, 0, 0, 0))
        )
      )
      .Padding(8)
      // As wide as the old panel; a popover narrows it to fit a narrow window.
      .Frame(width: 340, alignment: Alignment.TopLeading);

  // ---- Cards ----

  private View Card(ItemSlot<ProfileCardModel> card) =>
    Button(
        VStack(
            spacing: 6,
            HStack(
              spacing: 8,
              VAlignment.Top,
              If(
                () => card.Value.Renaming.Value,
                () => RenameField(card),
                () =>
                  Text(() => card.Value.Name)
                    .Font(Font.Heading.Sized(20))
                    .Foreground(Colors.Foreground)
                    .LineLimit(2)
                    .Frame(maxWidth: float.PositiveInfinity, maxHeight: 54, alignment: Alignment.TopLeading)
              ),
              If(() => !card.Value.Renaming.Value, () => CardActions(card))
            ),
            Spacer(),
            Text(() => card.Value.Line.Value)
              .Foreground(() => card.Value.LineIsProblem.Value ? Colors.Danger : Colors.Muted),
            Text(() => card.Value.State.Value)
              .Foreground(() =>
                card.Value.Armed.Value ? Colors.Danger
                : card.Value.InUse ? Colors.Accent
                : Colors.Muted
              )
          )
          .Font(Font.Caption)
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity, alignment: Alignment.TopLeading),
        () => card.Value.Switch,
        ButtonStyle.Card
      )
      .Frame(height: CardHeight)
      .Overlay(
        RoundedRectangle(1.5f)
          .Fill(Colors.Accent)
          .Frame(width: 3, height: CardHeight - 28)
          .Hidden(() => !card.Value.InUse)
          .AllowsHitTesting(false),
        Alignment.Leading
      );

  private View CardActions(ItemSlot<ProfileCardModel> card) =>
    HStack(
      spacing: 2,
      CardAction(UiIconKind.Edit, () => card.Value.Rename, () => Colors.Muted),
      CardAction(UiIconKind.Duplicate, () => card.Value.Duplicate, () => Colors.Muted),
      VM.FilesSupported ? CardAction(UiIconKind.Export, () => card.Value.Export, () => Colors.Muted) : null,
      If(
        () => VM.CanDelete,
        () =>
          CardAction(
            UiIconKind.Delete,
            () => card.Value.Delete,
            () => card.Value.Armed.Value ? Colors.Danger : Colors.Muted
          )
      )
    );

  // A glyph in the card's top right: no surface of its own until the pointer is over it.
  private static View CardAction(UiIconKind icon, Expression<Func<Command>> command, Expression<Func<UiColor>> color) =>
    Button(Image(ElementIcons.Of(icon), 18).Foreground(color), command, ButtonStyle.Icon)
      .Frame(width: ActionSize, height: ActionSize);

  // The card's name as a field: the caret after the name; Enter or a click
  // elsewhere saves, Escape keeps the old name.
  private View RenameField(ItemSlot<ProfileCardModel> card) =>
    TextField(Bind(() => VM.RenameText))
      .CharacterLimit(ElementProfile.MaximumNameLength)
      .SelectAllOnFocus(false)
      .OnEditEnded((_, canceled) => VM.EndRename(canceled))
      .FocusOn(() => card.Value.Renaming.Value ? 1 : 0)
      .Frame(maxWidth: float.PositiveInfinity);

  // ---- After an import: the fonts this PC lacks ----

  private View FontCard() =>
    VStack(
        spacing: 12,
        Text(L("이 PC에 없는 폰트가 있어요", "Some fonts aren't on this PC")).Font(Font.Heading.Sized(17)),
        Text(
            L(
              "가져온 프로필이 쓰는 폰트예요. 대신 쓸 폰트를 고르면 바로 바뀝니다. 그대로 두면 폰트를 설치하거나 추가할 때까지 기본 폰트로 보여요.",
              "The profile you imported uses these fonts. Pick one to use instead and it changes right away. Left as is, they show in the default font until you install or add them."
            )
          )
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        // The rows touch; each brings its own space.
        VStack(spacing: 0, ForEach(() => VM.Fonts.Rows, row => row.Use.Key, FontRow)),
        Button(Text(L("완료", "Done")).Frame(maxWidth: float.PositiveInfinity), () => VM.Fonts.Done)
          .Frame(width: 120)
          .Padding(new Insets(4, 0, 0, 0))
      )
      .Padding(16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .Background(Colors.Surface, 12);

  // A missing font, what its texts show now, and the list to pick another; the button goes under on narrow windows.
  private View FontRow(ItemSlot<ProfileFontsModel.Row> slot) =>
    ViewThatFits(
        HStack(spacing: 12, FontWords(slot), ChooseFont(slot)).Frame(minWidth: 448, maxWidth: float.PositiveInfinity),
        VStack(spacing: 8, FontWords(slot), ChooseFont(slot))
      )
      .Padding(new Insets(4, 0, 8, 0));

  private static View FontWords(ItemSlot<ProfileFontsModel.Row> slot) =>
    VStack(
        spacing: 4,
        Text(() => slot.Value.Name).Font(Font.Body),
        Text(() => slot.Value.State)
          .Font(Font.Caption)
          .Foreground(() => slot.Value.Replaced ? Colors.Accent : Colors.Muted)
      )
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading);

  private View ChooseFont(ItemSlot<ProfileFontsModel.Row> slot) =>
    Button(
        Text(L("폰트 고르기", "Choose font")).Frame(maxWidth: float.PositiveInfinity),
        () => slot.Value.Choose,
        ButtonStyle.Prominent
      )
      .Frame(width: 140, height: 40)
      .Popover(Bind(() => slot.Value.PickerOpen), () => FontPicker(slot.Value));

  // Opened with the font the row uses now; a pick shows at once and the list stays open.
  private static View FontPicker(ProfileFontsModel.Row row) => new FontList(row.Picker());
}
