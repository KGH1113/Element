# Overlay elements

The overlay editor (Settings → Overlay) places three kinds of elements: **Text**, **Key viewer** and **Shape**.

- **Texts and shapes** only move on the canvas; their size is set in their editor. While one is selected, the toolbar shows **Edit text** or **Edit shape** where the scale was. Double-clicking or pressing Enter opens the same editor. Scales from earlier versions stay as they were.
- **Key viewers** keep the scale and resize handles. Selecting one opens a second toolbar with hand and foot; drag it by its grip. It keeps its place for the next selection and never blocks the canvas.
- **Edit key viewer** in that toolbar, a double-click or Enter brings ElementNote in front of the game on the tab that viewer shows. If ElementNote is closed or too old to take the request, the help line under the main toolbar says what to do.

## Selecting several

- **Dragging over empty canvas** selects every element whose visible content lies fully inside the area. An element only partly inside stays out, so a large background shape isn't caught by accident.
- **Cmd/Ctrl or Shift + click** adds an element to the selection or takes it out. **Cmd/Ctrl or Shift + drag** draws the area even when it starts on an element, and adds what it covers to the selection.
- **Cmd/Ctrl + A** selects every element on the canvas. A plain click on empty canvas clears the selection.
- **Moving.** Dragging any selected element moves them all; snapping uses the outline of the whole group. Arrow keys move them all too. A click on one of them without moving selects it alone.
- **Delete and Reset** act on every selected element, as one undo step.
- **Align and distribute.** With two or more selected, the second toolbar lines them up: left, center, right, top, middle or bottom, against the box they cover together. With three or more, it also spaces them evenly across or down, keeping the outer two in place. Each press is one undo step; when there is nothing to change, such as a column already evenly spaced, the toolbar says so instead.
- While several are selected, the scale greys out and Edit text/shape, the resize handles and the key viewer toolbar step aside. Double-clicking one selects it alone and opens its editor.

## Order and groups

What is in front and what is grouped is one list: the overlay draws the saved elements in their order, the last one in front. Key viewers are elements of that list like any other.

- **Order panel.** **Order** in the toolbar opens a panel above it, from that button, and the same button closes it; the panel never blocks the canvas. It has a card per element, front-most at the top, and the game itself as the last card. Each card shows the element as it looks, small, and its name (a text is called by what it says). A key viewer, drawn outside the game, shows as a pane with keys.
- **Moving a card.** Dragging a card up or down shows a line where it will land, and the canvas shows the new order while it is held. Letting it go is one undo step; letting it go outside the list changes nothing.
- **Picking.** A click on a card picks its element; Shift or Cmd/Ctrl adds it. The pointer over a card outlines its elements on the canvas in the highlight color.
- **Forward and back.** Cmd/Ctrl + ] and [ move the selection past the nearest element it overlaps, so every press shows a change; with Shift, to the very front or back. When nothing it overlaps is that way, the status line says so.
- **On the canvas** a press picks the front-most element under the pointer.

### Groups

- **Making one.** With two or more selected, **Group** on the element toolbar (Cmd/Ctrl + G) makes them one group, named "Group 1", "Group 2"…. The elements gather in the order where the front-most of them was.
- **One level.** A group holds elements, not groups. Its elements always sit next to each other in the order. A group left with one element, by deleting or dragging the others out, is no group any more.
- **Picking.** A press on a grouped element picks the whole group; a second press picks the element inside. A double-click opens its editor, as for any element. A marquee takes a group once all of it is inside. The toolbar names a picked group ("Group · Combo").
- **On the order panel** a group is a card with a colored stripe, the group pictured whole and how many it holds. Its arrow shows its elements under it, set in beside the stripe; picking one of them on the canvas opens its group's card. A second click on a picked group's card renames it. Dragging the group's card moves the whole group; dragging an element card between a group's elements puts it in that group, and out of the group takes it out. A group's card never lands inside another group.
- **Ungrouping.** With a group picked, the element toolbar shows its name and **Ungroup** (Cmd/Ctrl + Shift + G). Its elements stay where they are, picked.
- **Together on any screen.** When a group moves, every element of it is saved against the same screen point, the one nearest the group as a whole, so the group stays together when the resolution changes. With a key viewer in it, that point is a corner or the middle, the points a key viewer can hold.
- **Lining up.** Several groups and elements line up and space out with each group as one box. A group picked alone lines up its own elements.
- **Built in.** The progress bar's three shapes and the combo with its "Perfect" title start as groups, named "Progress bar" and "Combo" in the player's language. Reset all brings back the first order and these groups.

## Values

A value is something the game knows, kept live. It behaves as one character: the caret skips it, a selection takes it whole, and one Backspace deletes it.

| Category | Values |
| --- | --- |
| Progress | Start, current |
| Judgment | X-Accuracy, Overload, Too Early, Very Early, Early Perfect, Perfect −, Pure Perfect, Perfect +, Late Perfect, Very Late, Too Late, Miss, Combo, Timing scale |
| Time | Elapsed, total |
| Speed | Tile BPM, current BPM, keys per second |
| Song | Song title, attempts, full attempts |
| System | FPS |

- **Units are words you type.** Values carry no units, so write `%` after progress or accuracy yourself.
- **Composite values are built from parts.** Values that combined others are gone. The old progress `0%-48%` is now start, `%-`, current and `%`. The old time `1:27~3:48` is elapsed, `~` and total.
- **Adding a value.** The input dock at the bottom of both editors, which is always there, lists the categories on its left, then one row per mod that adds values, and shows the picked one's values beside the list; a mod's values sit in a row per group. The category picked last stays picked for the next editor. In a text, clicking a value adds it at the caret; while a formula is being typed, it goes into the formula. You can also drag it: an accent caret shows where it lands in the text, and a number lights up when the value can go there.
- **Finding a value.** Typing in the dock's search shows every value it finds, by category and mod: Korean and English names, keys (`kps`), group headings and mod names all count, with case, spaces and dashes left out. Enter adds the first one; × or Esc clears the search.
- **What a value reads now.** Each value in the dock shows its current reading after its name, as the overlay would show it, while the game has one.
- **Hovering.** Pointing at a value in the text names it, including its calculation.

## Calculating

Clicking a number, or a value in a text, types its formula in the input dock along the bottom of the editor. The preview always sits above the dock, so it is never covered. With nothing being typed, the dock's top line says what to do and the keypad rests.

- **Formula line.** The top line shows what is edited, its formula with values as chips, and the result on the right.
- **Values and keypad.** Below are all values and a large keypad: 0–9, point, + − × ÷, backspace, **AC** to start over and **Apply** to finish the formula (the header's **Done** closes the editor). The keyboard types the same keys.
- **Results.** Elsewhere, numbers show their results. One worked out from a formula reads in the accent; its formula appears only in the dock.
- **Switching and finishing.** Clicking another number switches the dock to it. Done, Enter, Esc or a click on the empty dim finishes, and the dock rests again.
- **AC** starts over without touching the number until something new is typed; it is its own undo step.

- **Order.** × and ÷ come before + and −.
- **Combining values.** Type an operator, then pick another value (or drop it on the formula line). Without an operator, a value replaces what was there.
- **Display.** The text shows the result as the game will: up to two decimals, or m:ss when the result is a time. Total − elapsed shows the time left. A time divided by a time is a plain number.
- **Missing data.** A result needs every value it uses. Until the game has them, the overlay shows nothing there, and the editor uses each value's example.
- **Leaving saves.** In the overlay editor and the text and shape editors, every way out saves except the Cancel button: Esc (once nothing else is open) finishes like Save or Done, and edits open when the mod is turned off are kept.
- **Live while editing.** With a level playing behind the editor (autoplay, for one), the canvas, the text and shape editors' previews, their numbers and the input dock's result follow the live values. An editor redraws only when a value it shows changes.
- **Words.** The song title can't be calculated with, so number fields refuse it with a short reason.
- **Song title.** The game's level name, "artist - song", with the level's own bold, italic, colors and sizes. Parts the level hides stay hidden (a see-through artist, a dash shrunk to nothing). A name hidden entirely shows as plain text, so the overlay still says what is playing. Broken tags, such as a seven-digit color, are left out instead of shown.

## Text

- Adding a text, double-clicking one, pressing Enter on a selected one or **Edit text** in the toolbar opens the editor. The game is hidden behind a black screen with a header (what is edited, Cancel, Done), and the text sits at the size the game draws it.
- A column on the right, above the keypad, has a row per property: **Font**, **Size**, **Text color**, **Animate** and **Grow from**, each with what it holds. Font, size, color and Animate act on the selected characters, or on the whole text when nothing is selected. Size takes values too. See [Fonts](#fonts) and [Animation](#animation).
- Text color, Font, Animate and Grow from open in that column: the rows give way to a back row naming what is open (‹ Text color), with the color picker, the font list or the nine points under it. The text stays in sight; the dock's line says which color is picked. The back row, Esc or a click elsewhere closes it.
- Notices (a value that can't go somewhere, a number kept in range) show for a few seconds right above the dock's formula line.
- The picker's opacity works like the editor's other numbers: clicking it types its formula on the keypad right below, and a value from the dock can be dropped on it. The slider shows the result; moving it makes the opacity a plain number again.
- Every color is either **Highlight Color** (follows the highlight color; only its opacity is yours) or **Custom Color**.
- Sizes are what the game draws at 1080p.
- ⌘/Ctrl + Enter or Esc finishes and saves, as Done does; only Cancel leaves without saving. A text left empty is removed.
- Only texts that use a changed value are redrawn, so values that update every hit cost no more than before.

## Animation

**Animate** in the text editor's column opens a tool there (‹ Animate) with two switches, and the row says what is on (Grow · Color, Grow, Color or Off). **Grow when it changes** makes a value grow for a moment and settle back each time what it shows changes: the combo on every hit, Miss when one happens, the BPM when it changes. **Color by value** is [below](#color-by-value).

- **What it acts on.** Values in the selection, or every value in the text when nothing is selected. Typed text never moves. A selection of only text has nothing to move, so the switch reads dimmed and a press says why.
- **Mixed.** When the selection holds values that move and values that don't, the switch sits halfway; a press turns them all on.
- **Turning it on** plays it once on the text in the editor.
- **New values** follow the nearest value in the text, the one before the caret first. In a text without values they start still.
- **What counts as a change** is the text as shown. Elapsed time changes every frame but reads differently once a second, so it grows once a second. Accuracy that rounds to the same two decimals doesn't grow.
- **When it stays still.** A value that appears (the game had no data yet) or goes away doesn't grow, and neither does anything when a level starts or restarts. A combo that breaks to 0 grows, as any change does.
- Each value grows around its own middle; the text's place, size and selection outline stay as they are, and a gradient keeps its colors while a value grows.
- The built-in combo has it on. Texts from before this switch keep their combos growing; nothing else starts moving.

### Color by value

**Color by value** makes the text's color follow a value as it changes: X-Accuracy turning sky blue from 99.5 and the highlight color at 100, or progress blending into the highlight color as a level goes on. Like Text color it acts on the selection, or the whole text when nothing is selected.

- **The band.** The tool draws the color as a band: the lowest stop at its left end, the highest at its right, each stop a round handle with its value above it. Under the band a pointer marks where the value is now ("Now 99.81"), or the editor's example while no level plays; past the ends it waits at the end.
- **Changes smoothly or at once.** Smoothly blends from stop to stop, mixed as eyes see colors (Oklab), so white to blue passes no gray. At once jumps to a stop's color the moment the value reaches it and holds it up to the next stop. Below the lowest stop and above the highest, their colors hold.
- **Stops.** A click on a handle picks it; a click on the band between handles adds a stop there in the color the band already has, and keeps dragging it. Dragging snaps to tidy numbers for the band's range (tenths for 95–100, fives for 0–200); Alt drags freely. Dragging past an end grows the band, which fits the stops again when the handle is let go. Up to eight stops; the last two stay.
- **The picked stop** shows its value, typed on the keypad, and its color, which opens the color picker beside the column (Highlight or Custom, with opacity). While it is open the text wears that stop's color. The bin removes the stop.
- **Follows.** The value row names the value the color follows. Turning the color on follows the first number in the selection; with none, the values to pick from open at once. The row lists the text's values, then **Another value**, after which the next value clicked in the dock is followed instead of added to the text (Esc stops). Another mod's values work too. Words such as the song title can't pick a color. Following another value keeps the colors and how they are spread across the new value's range.
- **First look.** X-Accuracy changes at once, from the text's own color at 95 to green at 98, sky blue at 99.5 and the highlight color at 100. Progress changes smoothly from the text's own color at 0 to the highlight color at 100. Other values blend the same two colors from 0 to a round number half again above today's value. A text in the highlight color starts from white, so the change shows.
- **One way at a time.** While the color follows a value, Text color reads **By value** and its swatch shows the band. Picking a color or gradient there stops following the value, and a notice says so. Each character keeps its own color underneath: turning Color by value off brings it back.
- **In the game.** The color changes only when the followed value does, without extra work per frame. Before the value has data (before a level starts), the text shows its own color.
- Undo, Cancel and Done work as for every other edit; dragging a stop is one undo step, and so is everything picked while the color picker stays on one stop.

## Fonts

- **Default font.** **Default font** in the overlay editor's toolbar, beside the highlight color, picks the font every text uses. The first row is the built-in MapleStory font. Cancel puts the previous font back.
- **A text's own font.** **Font** in the text editor's column changes the selected characters, or the whole text when nothing is selected, like size and color. **Default font** makes them follow the overlay's default again. A pick shows on the text at once and the list stays open to try another; one undo takes back everything tried while it was open.
- **Where fonts come from.** The list has the fonts added to the mod, then the fonts installed on this PC. Installed fonts are read in the background the first time the list opens.
- **Adding a file.** **Add file** opens the system's file dialog for TTF, OTF and TTC files. The chosen files are copied into `Mods/Element/Fonts`, which updates keep, and the first one is applied. Files put in that folder by hand show up the next time the list opens; **Folder** opens it. Linux has no dialog, so the button opens the folder instead.
- **Search.** Typing part of a name narrows the list; Enter applies the first match.
- **Saved by name.** A font is saved by family and style, such as Pretendard Bold, so a layout finds the same font on another PC whether it is installed there or sits in the Fonts folder. A font this PC lacks is drawn in the default font, and the list says so with what to do.
- **Missing characters.** Characters a font lacks, such as Hangul in a Latin font, come from the built-in font. A font with no Latin letters, such as Arial Hebrew, takes them from the system's Latin font (Helvetica on macOS, Segoe UI on Windows) first, as other apps show it.
- Every font keeps the overlay's black outline and shadow.

## Shape

Add Element → Shape asks for a square, triangle or circle, then opens the shape editor. The kind stays fixed afterwards.

The default layout has a **progress bar** made of three shapes, as JipperResourcePack draws it: a black bar centered along the top, a light gray track 2 px inside it, and a fill in the highlight color over the track whose width is current progress × 6.36, growing from its left. Select both to move them together. Deleting them keeps them deleted; Reset all brings them back. Layouts from earlier versions get the bar once.

- The shape sits in the middle at its in-game size, with **W** under it and **H** to its right.
- Each corner of a square or triangle has an arc just outside it. Clicking the arc edits that corner's rounding and shows its value beside the arc.
- The column on the right has **Fill**, **Border**, **Border width** and **Grow from**, as in the text editor. Fill and Border open the color picker in the column, under a back row naming the color. Their opacity in the picker and the border width are typed in the input dock and take values.
- Squares whose corners were dragged in 1.1.0 keep that shape; corners can no longer be moved.
- Every number takes values. For example, width = current × 3 makes a progress bar.
- On the canvas a shape only moves, like a text. A new shape, like any new element, goes in front; the [order panel](#order-and-groups) puts it behind.
- ⌘/Ctrl + Z undoes inside the editor. Done is one undo step in the overlay editor. Cancelling a new shape removes it.

## Gradients

A shape's fill and border and a text's color can be gradients, as in Figma: linear, radial, angular or diamond.

- There is no switch between solid and gradient. The color picker in either editor has a color bar on top; one color is solid, and clicking the bar adds a color and makes a gradient. The first added color is a neighbor of the current one (a little further round the color wheel, a little brighter), so the gradient shows at once.
- The bar's squares are the colors. Drag one to move it; it sticks to 0, 25, 50, 75 and 100% and to the other colors (Alt lets go). The picked one shows its position above it, typed in the input dock like W and H, with a delete button beside it. Delete also removes it in the shape editor, and ←/→ move it 1% (10% with Shift). One color left is solid again.
- Four tiles drawn in the current colors pick the kind, beside flip and a quarter turn.
- Over the preview, circles are handles and squares are colors. The round start and end handles (and a width handle for radial, angular and diamond) only move the gradient. They stick to the box's edges, middle and center and line up with each other, with guides as in the layout editor; Alt lets go and Shift turns in 15° steps. Colors hang just off the line as squares pointing at where they sit; dragging one moves its color, and clicking the line adds one. The angle shows beside the line; 90° runs left to right, as in ElementNote.
- Positions, opacities and the angle take values, so a color can follow progress.
- Following the highlight color, the whole gradient is that one color and each stop sets only its opacity.
- A text's gradient spans the whole text's box, so a gradient on part of a text carries on across it. Colors are worked out at the corners of each character, which is exact for two-color linear gradients.
- Shapes read their gradient from a small texture built only when it changes, so the game's stock UI shader draws them on every platform. Earlier versions show a gradient's first color.

## Grow from

When a text or shape changes size (a value grows longer, a width follows progress), one point of it stays put and the rest grows away from it. The **Grow from** row in either editor's column names the current point and picks it from nine, with arrows showing where it grows; the editor marks the point on the preview. **Auto** uses the point nearest to where the element sits on screen: an element near the right edge grows to the left. The choice is saved with the element when you press Done.

## Key viewer

Added at once as a hand viewer. Its toolbar switches between hand and foot in the same place. It is available while ElementNote runs with **Inside ADOFAI** on (Settings → Where to show); otherwise Add Element says what to turn on.

Editor panels over a key viewer cut it out with their own rounded corners, so the viewer never covers the editor.

A key viewer takes its place in the [order](#order-and-groups) like other elements and can be in a group. It is drawn by ElementNote above the game, so an element in front of it cuts its own shape out of the viewer, in the game and in the editor: opaque elements look exactly in front, and where a see-through element covers it the game shows through instead of the viewer.

## Settings from earlier versions

- **Before layers (settings version 11):** elements drew by kind, shapes behind other mods' elements, those behind texts, key viewers over everything. The first load puts the saved list in that order, so everything looks as before, and starts the progress bar and the combo as groups.

- **Before 1.1:** the fixed overlays are converted on the first load into texts with the same values, colors and places. A moved overlay keeps its anchor, offset and size. The combo becomes two texts, "Perfect" and the number, placed where the old combo was. Values that used to fade toward the highlight color (progress, accuracy, BPM) now use the highlight color directly. The labels in front of them ("Progress |", "Music Time |") keep following the game's language until the text is edited; editing turns them into plain words.
- **1.1.0:** texts are rewritten once so they read the same. Progress, time and the judgment row become their parts, and values that lost their `%` get it back as text. The judgment row's Pure Perfect takes a fixed green instead of following the game's hit color.
