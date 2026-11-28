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

- **Order panel.** **Order** in the toolbar opens the order panel and closes it again. It opens at the view's top right, clear of the toolbar (smaller if it must be), is carried by its head (the grip, its name and Float view) and stays where it is left, also when it is closed and opened again. It has a card per element, front-most at the top ("Front" over the list), and the game itself as the last card ("Back"). Each card shows the element as it looks, small, its name (a text is called by what it says) and under it what it is (Text, Shape, Key viewer, or the mod that adds it). A key viewer, drawn outside the game, shows as a pane with keys. The cards are as large as the toolbar's buttons.
- **Float view** is in the order panel's head: the elements stand apart in depth, in the order the cards show. While it is on, the panel steps aside and the scene has the whole view; **End Float view** in the toolbar's hint line or Esc turns it off (Cmd/Ctrl + Enter still saves and leaves).
- **Moving a card.** Dragging a card up or down shows a line where it will land, and the canvas shows the new order while it is held. Letting it go is one undo step; letting it go outside the list changes nothing.
- **Picking.** A click on a card picks its element; Shift or Cmd/Ctrl adds it. The pointer over a card outlines its elements on the canvas in the highlight color.
- **Forward and back.** Cmd/Ctrl + ] and [ move the selection past the nearest element it overlaps, so every press shows a change; with Shift, to the very front or back. When nothing it overlaps is that way, the status line says so.
- **On the canvas** a press picks the front-most element under the pointer.

### Groups

- **Making one.** With two or more selected, **Group** on the element toolbar (Cmd/Ctrl + G) makes them one group, named "Group 1", "Group 2"…. The elements gather in the order where the front-most of them was.
- **One level.** A group holds elements, not groups. Its elements always sit next to each other in the order. A group left with one element, by deleting or dragging the others out, is no group any more.
- **Picking.** A press on a grouped element picks the whole group; a second press picks the element inside. A double-click opens its editor, as for any element. A marquee takes a group once all of it is inside. The toolbar names a picked group ("Group · Combo").
- **On the order panel** a group is a card with a colored stripe, the group pictured whole and how many elements it holds. Its arrow shows its elements under it, set in beside the stripe; picking one of them on the canvas opens its group's card. A second click on a picked group's card renames it. Dragging the group's card moves the whole group; dragging an element card between a group's elements puts it in that group, and out of the group takes it out. A group's card never lands inside another group.
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
- **Adding a value.** Under the preview, both editors list the values that can go where the dock is now: one section per category, then one per mod that adds values, a mod's values in a row per group. In a text, clicking a value adds it at the caret; while a field or a piece is picked, it goes there. Values are only picked, never dragged.
- **Only what fits.** Only values that can go there are listed, under the mark of the type that goes there. Where none can go (a shape with nothing picked, a font being picked) the list is clear.
- **What a value reads now.** Each value in the dock shows its current reading after its name, as the overlay would show it, while the game has one.
- **Hovering.** Pointing at a value in the text names it, including its calculation.

## Calculating

The text and shape editors are two rows with one line across between them: above it what is edited (the preview, and the fields on the right), below it how (the **line** and the values on the left, the **input method** on the right). Nothing reaches from one part into another. Clicking a field, or a value in a text, opens it on the line. With nothing open the line is clear.

- **The line.** It shows the variable that is open: its type's mark, whose it is and its name ("Whole text › Size"), its pieces as pills, and what it gives on the right. Inside a condition it shows the slot gone into, with the way to it ("Whole text › Size › Condition › Default").
- **Values and keypad.** Under the line are the values that fit; at the right of the lower row is the input method of what is picked, a large keypad for a number: 0–9, point, + − × ÷ (drawn alike, as heavy as the digits) and backspace, filling the place with nothing under it (the header's **Done** closes the editor). The keyboard types the same keys.
- **Results.** Elsewhere, numbers show their results. One worked out from a formula reads in the accent; its formula appears only in the dock.
- **Switching and finishing.** Clicking another number switches the dock to it. Done, Enter, Esc or a click on the empty dim finishes, and the dock rests again.
- **The keyboard is the open field's.** While the dock has something open (a number, a color, the font list), the keyboard types like the keypad and nothing reaches the text, so what is selected there is never typed over or deleted. Backspace takes back one step of what was put in: a digit of a number, a letter of some words, any other value whole. Only a value of the text that is open in the dock lets typing go on behind it.
- The picked piece on the line shows a small **×** that takes it out: a number takes its operator with it, an operator the number after it. A comparison or a point has none: it is swapped for another. In the shape editor it is its own undo step.
- **Enter** finishes what is open, from however deep inside it; **Esc** steps back out of a condition's slot, then closes. A click elsewhere closes too. Everything typed is already in the element as it fits, so there is nothing to apply.

- **Order.** × and ÷ come before + and −.
- **Combining values.** An operator brings the number after it, picked (× makes × 0), so a calculation is never left hanging; type over it or pick a value for it. Without an operator, a value replaces what was there.
- See [Variables](#variables) for conditions, words and colors.
- **Display.** The text shows the result as the game will: up to two decimals, or m:ss when the result is a time. Total − elapsed shows the time left. A time divided by a time is a plain number.
- **Missing data.** A result needs every value it uses. Until the game has them, the overlay shows nothing there, and the editor uses each value's example.
- **A value that shows nothing.** A value can give nothing for now (a condition whose answer is empty, words not typed yet). The game draws nothing there; the text editor keeps its place open with a hollow box, so it can still be seen and clicked. Words not typed yet are that same hollow box wherever they are drawn: on the line, on a condition's fork and strip, and in the field rows.
- **Decimals of a value.** A value with decimals (progress, X-Accuracy, BPM, KPS, timing scale) is read where it is used at as many decimals as it shows, unless chosen otherwise there. Picked, its input method is its own number written out to four decimals in the middle of a card, over the operators: solid as far as it is read, faint past that. A digit pressed is where it ends (the whole part or the point: whole), and under the pointer the number shows what it would read. That is what a condition compares and a text writes (zeros kept), and its pill says it quietly ("현재 진행도 0.0"). So "progress (one decimal) ≠ 0" holds from 0.1%. Each value is cut the way the game writes it: progress floored, X-Accuracy cut at its last digit and never 100 before it is; BPM, KPS and timing scale, which the game doesn't write, round. Saved as `{progress-current:1}`.
- **Leaving saves.** In the overlay editor and the text and shape editors, every way out saves except the Cancel button: Esc (once nothing else is open) finishes like Save or Done, and edits open when the mod is turned off are kept. In the overlay editor and the text, shape and mod element editors, Cancel with anything changed first asks whether to discard it; **Keep editing** is focused, so Enter (or Esc) keeps the edit. With nothing changed, Cancel closes at once.
- **Live while editing.** With a level playing behind the editor (autoplay, for one), the canvas, the text and shape editors' previews, their numbers and the input dock's result follow the live values. An editor redraws only when a value it shows changes.
- **Words.** The song title can't be calculated with, so number fields refuse it with a short reason.
- **Song title.** The game's level name, "artist - song", with the level's own bold, italic, colors and sizes. Parts the level hides stay hidden (a see-through artist, a dash shrunk to nothing). A name hidden entirely shows as plain text, so the overlay still says what is playing. Broken tags, such as a seven-digit color, are left out instead of shown.

## Variables

Every property of an element is a **field** (칸): in the editors' columns each one shows its type's mark (# number, T words, ● color, ◐ true or false, ⊡ point, a pair of angles for a comparison, Aa font), its name, what it holds as chips and, when that is worked out, "= result". The numbers around a shape are the same fields as pills.

Every field the dock opens is a **variable**: a text's value, a size, a shape's width, a color. Nothing is typed into it directly; it holds values, and the dock's **input method** below the line edits the one picked. Every variable has a type, and so does every value: **number**, **words**, **color**, **true or false**, **comparison** (what a condition compares by), **point** (where an element grows from), or **anything** (a text's value). Values of different types don't mix, and a field can be finished only when what it holds gives its type.

- **The line is made of pieces.** Each value, number, word or condition is a piece, shown as a pill; clicking one picks it. A field that holds one value (a number holds one calculation) swaps it for the one picked; a text's value holds pieces side by side. Numbers join with + − × ÷; in a text's value, pieces side by side show one after another.
- **Making values.** The first section, **Make**, holds what fits of **Custom color**, **Gradient**, **Number**, **Text**, **True or false**, **Comparison**, **Point**, **Condition** and **Group** (a group only where a calculation can be). A new one is picked at once, with its input method. A text needs no **Text** at its caret: typing makes words.
- **Input methods.** Always in the same place, at the right of the lower row. A number has the keypad; words have a field the keyboard types into (spaces count), which takes the caret as soon as the words are picked; true or false has two keys; a comparison has the six comparisons; a point has the nine points and Auto; a solid color has the picker; a font has the font list; a condition has its five slots. Each has the whole place; there is no row under it. With only the caret in a text, a keyboard shows there: the typing goes into the text.
- **Conditions.** A condition is one pill on the line. Picked, its input method draws it as a fork: on top one bar, **if**, holds what is compared (a value, the comparison in a round key, a value), and under it the two answers stand side by side, **then** and **otherwise**. An answer that is one color shows as that color, one number as that number, large. A round mark on each way says which is which without reading: ✓ leads to the answer for when the comparison holds, ✕ to the other. Both ways always look the same, whatever the values are now; the folded strip marks its two answers ✓ and ✕ too. Click any part to go into that slot: the line and the input method are that slot's, and the condition stays on top of the input method, folded into a strip of its five slots with the one being filled lit. A click on another slot of the strip goes straight there; the round key at its start (or Esc) steps one level back out, and the condition unfolds. A condition inside an answer folds the same way, the nearest one on the strip. **Enter** finishes the field, from however deep inside it. The two compared slots take numbers, the comparison one of < ≤ = ≠ ≥ > (it starts as ≥) or a condition that gives one, and the answers whatever the field needs: "Combo ≥ 100 → 60, otherwise 40". The default also shows before the game has the values compared (before a level starts). Numbers the same to six decimals count as the same. In a text's value the answers may stay empty, which shows nothing.
- **Groups.** A group is a variable inside the variable, for a calculation that goes first: (Combo + 1) × 2.
- **Can't finish yet.** While a variable doesn't give its type, an empty slot for one, the element keeps what it had. Nothing is said in words: **Enter** is refused with a shake, the dock goes to the condition that lacks something, and the slot in the way is outlined. A value that can't go where it was put is refused the same way; values that can't go where the dock is now are not listed at all.
- **Keyboard.** Digits, the point and + − × ÷ go to the keypad. On a comparison < > = type comparisons, > then = makes ≥, < then = makes ≤, and ! makes ≠.

## Text

- Adding a text, double-clicking one, pressing Enter on a selected one or **Edit text** in the toolbar opens the editor. The game is hidden behind a black screen with a header (what is edited, Cancel, Done), and the text sits at the size the game draws it.
- **The text is its own content field.** Under each piece of the text, a thin bracket in its type's color and its type's mark: each run of letters that look the same is one **letters value** (T), each game value its own (# for a number). Nothing is drawn over the letters. The picked piece's bracket is the accent; the letters the caret types into light up as they grow. A piece across lines gets a bracket per line.
- **The caret belongs to letters.** Typing between two values starts new letters. A selection stays inside one letters value (dragging, Shift + arrows, Shift + Home/End); select all takes the letters the caret is in; two clicks take a word, three the whole letters value. With letters selected, the dock's line names them.
- The upper row's right holds the fields in two groups. **The whole text** (or **The picked letters**, or **The picked value**, while one is selected): **Size**, **Text color**, **Opacity**, **Font** and **Animate on change**. **Element**: **Grow from**. Selected letters styled on their own show as their own letters value while a field is open. See [Fonts](#fonts) and [Animation](#animation).
- Every field opens on the line, which names whose it is ("Whole text › Size", "“FPS” › Font"). Size, Opacity and Animate on change are typed or switched in the input method's place; Text color opens the color picker there and Font the font list. A gradient's color bar and numbers open in the column above, under a back row naming them, with the picked color's square in the input method's place. The field again, Esc or a click elsewhere closes them.
- A color is a variable: the highlight color (only its opacity is yours), a custom color, or a condition giving colors. See [Colors](#colors).
- Sizes are what the game draws at 1080p.
- ⌘/Ctrl + Enter or Esc finishes and saves, as Done does; only Cancel leaves without saving. A text left empty is removed.
- Only texts that use a changed value are redrawn, so values that update every hit cost no more than before.

## Animation

**Animate on change** in the text editor's column is a true-or-false field: it opens in the dock, where the switch sets it, and the field says whether it is on. Turned on, it makes a value grow for a moment and settle back each time what it shows changes: the combo on every hit, Miss when one happens, the BPM when it changes.

- **What it acts on.** Values in the selection, or every value in the text when nothing is selected. Typed text never moves. A selection of only text has nothing to move, so the switch reads dimmed and a press says why.
- **Mixed.** When the selection holds values that move and values that don't, the switch sits halfway; a press turns them all on.
- **Turning it on** plays it once on the text in the editor.
- **New values** follow the nearest value in the text, the one before the caret first. In a text without values they start still.
- **What counts as a change** is the text as shown. Elapsed time changes every frame but reads differently once a second, so it grows once a second. Accuracy that rounds to the same two decimals doesn't grow.
- **When it stays still.** A value that appears (the game had no data yet) or goes away doesn't grow, and neither does anything when a level starts or restarts. A combo that breaks to 0 grows, as any change does.
- Each value grows around its own middle; the text's place, size and selection outline stay as they are, and a gradient keeps its colors while a value grows.
- The built-in combo has it on. Texts from before this switch keep their combos growing; nothing else starts moving.

## Colors

A text's color, a shape's fill and its border are color [variables](#variables). The dock's **Colors** category holds **Highlight color** and **Custom color**.

- **The dock opens the color.** Opening Text color, Fill or Border puts the picker (the hue standing left of the square, HEX under them; a gradient's color adds its opacity standing on the right) in the keypad's place, and the dock's line holds that color (Text color = ● #FFFFFF). Opacity is a number field of its own beside each color. A gradient's numbers still open in the dock when clicked.
- **The picker is the color's input method.** It edits the color piece picked in the dock. The color's list holds **Highlight color** (it follows the highlight color, and a note says so), **Custom color** (a color of its own, picked in the picker) and **Gradient**. A color value always goes to the open color, even while one of its numbers is typed. Colors can't go in a number, nor in a text: a press says why.
- **Conditions.** Put a **Condition** in the color and fill its slots: "X-Accuracy = 100 → ● Highlight color, otherwise ● #5AC8FA". While a condition is picked, the picker waits with a line saying to open its result or default; inside them it edits their color (‹ Color in the condition). A condition can go in a result or default for more steps. Its colors take the color's opacity, and they show over a gradient too.
- **In the game.** A color with a condition is checked only when a value it compares changes.

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
- The column on the right holds the fields in three groups: **Fill** (color, opacity), **Border** (color, width, opacity) and **Element** (grow from). The colors open the picker in the keypad's place; the numbers are typed in the input dock and take values. Width, height and each corner's rounding are number fields around the shape.
- Squares whose corners were dragged in 1.1.0 keep that shape; corners can no longer be moved.
- Every number takes values. For example, width = current × 3 makes a progress bar.
- On the canvas a shape only moves, like a text. A new shape, like any new element, goes in front; the [order panel](#order-and-groups) puts it behind.
- ⌘/Ctrl + Z undoes inside the editor. Done is one undo step in the overlay editor. Cancelling a new shape removes it.

## Gradients

A shape's fill and border and a text's color can be gradients, as in Figma: linear, radial, angular or diamond.

- **Gradient** in a color's list of values makes it a gradient; its tool (the color bar, the kinds and the numbers) opens in the column above the keypad. The first added color is a neighbor of the current one (a little further round the color wheel, a little brighter), so the gradient shows at once.
- The bar's squares are the colors. Drag one to move it; it sticks to 0, 25, 50, 75 and 100% and to the other colors (Alt lets go). The picked one shows its position above it, typed in the input dock like W and H, with a delete button beside it. Delete also removes it in the shape editor, and ←/→ move it 1% (10% with Shift). One color left is solid again.
- Four tiles drawn in the current colors pick the kind, beside flip and a quarter turn.
- Over the preview, circles are handles and squares are colors. The round start and end handles (and a width handle for radial, angular and diamond) only move the gradient. They stick to the box's edges, middle and center and line up with each other, with guides as in the layout editor; Alt lets go and Shift turns in 15° steps. Colors hang just off the line as squares pointing at where they sit; dragging one moves its color, and clicking the line adds one. The angle shows beside the line; 90° runs left to right, as in ElementNote.
- Positions, opacities and the angle take values, so a color can follow progress.
- A gradient is always of colors of its own. Made from the highlight color, it starts from that color as a custom one; picking the highlight color for a gradient makes it that one color again.
- A text's gradient spans the whole text's box, so a gradient on part of a text carries on across it. Colors are worked out at the corners of each character, which is exact for two-color linear gradients.
- Shapes read their gradient from a small texture built only when it changes, so the game's stock UI shader draws them on every platform. Earlier versions show a gradient's first color.

## Grow from

When a text or shape changes size (a value grows longer, a width follows progress), one point of it stays put and the rest grows away from it. **Grow from** is a field of the **point** type in either editor's column: it holds one of the nine points or **Auto**, or a condition that gives points ("past half the level, grow from the top right"). A point's input method is the nine points as keys with Auto under them, and the editor marks the point on the preview while the field is open. **Auto** uses the point nearest to where the element sits on screen: an element near the right edge grows to the left. The field is saved with the element when you press Done, and a point worked out from values follows them while the game runs, the element staying where it is.

## Key viewer

Added at once as a hand viewer. Its toolbar switches between hand and foot in the same place. It is available while ElementNote runs with **Inside ADOFAI** on (Settings → Where to show); otherwise Add Element says what to turn on.

Editor panels over a key viewer cut it out with their own rounded corners, so the viewer never covers the editor.

A key viewer takes its place in the [order](#order-and-groups) like other elements and can be in a group. It is drawn by ElementNote above the game, so an element in front of it cuts its own shape out of the viewer, in the game and in the editor: opaque elements look exactly in front, and where a see-through element covers it the game shows through instead of the viewer.

## Settings from earlier versions

- **Before layers (settings version 11):** elements drew by kind, shapes behind other mods' elements, those behind texts, key viewers over everything. The first load puts the saved list in that order, so everything looks as before, and starts the progress bar and the combo as groups.

- **Before 1.1:** the fixed overlays are converted on the first load into texts with the same values, colors and places. A moved overlay keeps its anchor, offset and size. The combo becomes two texts, "Perfect" and the number, placed where the old combo was. Values that used to fade toward the highlight color (progress, accuracy, BPM) now use the highlight color directly. The labels in front of them ("Progress |", "Music Time |") keep following the game's language until the text is edited; editing turns them into plain words.
- **Color by value (up to 1.5.0):** a text color that followed a value becomes a color condition: from the highest stop ("the value ≥ this stop's number → its color"), each next stop in the default, down to the lowest stop's color. A color that changed smoothly between its stops now changes at each stop, and every stop takes the lowest stop's opacity.
- **1.1.0:** texts are rewritten once so they read the same. Progress, time and the judgment row become their parts, and values that lost their `%` get it back as text. The judgment row's Pure Perfect takes a fixed green instead of following the game's hit color.
