import { expect, test, type Page } from '@playwright/test';

const surfaces = ['main', 'overlay', 'obs'] as const;
const fixtures = [
  'hand-default',
  'hand-custom-css',
  'foot-note-counter',
] as const;

async function collectContract(page: Page) {
  return page.locator('[data-visual-root="true"]').evaluate((root) => {
    const styleValue = (element: Element, property: string) =>
      getComputedStyle(element).getPropertyValue(property).trim();
    const keys = Array.from(root.querySelectorAll<HTMLElement>('.visual-key'));
    const counters = Array.from(root.querySelectorAll<HTMLElement>('.counter'));
    const canvas = root.querySelector<HTMLCanvasElement>('canvas');

    return {
      surface: root.getAttribute('data-surface'),
      fixture: root.getAttribute('data-fixture'),
      viewerKind: root.getAttribute('data-viewer-kind'),
      hiddenKeyCount: Number(root.getAttribute('data-hidden-key-count')),
      note: {
        enabled: root.getAttribute('data-note-enabled') === 'true',
        speed: Number(root.getAttribute('data-note-speed')),
        trackHeight: Number(root.getAttribute('data-track-height')),
        glowSize: Number(root.getAttribute('data-note-glow-size')),
        // v2 crops the backing canvas to the visible tracks. Compare the
        // public viewport here; unchanged PNGs verify actual note placement.
        canvas: canvas
          ? { width: window.innerWidth, height: window.innerHeight }
          : null,
      },
      keys: keys.map((key) => ({
        state: key.getAttribute('data-state'),
        keyElement: key.getAttribute('data-key-element'),
        className: key.className,
        childKinds: Array.from(key.children).map((child) => ({
          tag: child.tagName.toLowerCase(),
          className: child.getAttribute('class') || '',
        })),
        decorativeChildren: Array.from(key.children)
          .filter((child) => /gradient|ring|paint/i.test(child.className))
          .map((child) => child.className),
        style: {
          backgroundColor: styleValue(key, 'background-color'),
          borderStyle: styleValue(key, 'border-style'),
          borderWidth: styleValue(key, 'border-width'),
          borderColor: styleValue(key, 'border-color'),
          borderRadius: styleValue(key, 'border-radius'),
          transform: styleValue(key, 'transform'),
          opacity: styleValue(key, 'opacity'),
          overflow: styleValue(key, 'overflow'),
          position: styleValue(key, 'position'),
        },
      })),
      counters: counters.map((counter) => ({
        text: counter.textContent,
        state: counter.getAttribute('data-counter-state'),
        style: {
          fontSize: styleValue(counter, 'font-size'),
          fontWeight: styleValue(counter, 'font-weight'),
          transform: styleValue(counter, 'transform'),
          color: styleValue(counter, 'color'),
          strokeWidth: styleValue(counter, '-webkit-text-stroke-width'),
        },
      })),
    };
  });
}

test('app chrome typography does not leak into viewer surfaces', async ({
  page,
}) => {
  await page.goto('/visual/scope.html');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const contract = await page.evaluate(() => {
    const font = (selector: string) => {
      const element = document.querySelector(selector);
      if (!element) throw new Error(`Missing scope fixture: ${selector}`);
      return getComputedStyle(element).fontFamily;
    };
    const rules = Array.from(document.styleSheets).flatMap((sheet) => {
      try {
        return Array.from(sheet.cssRules, (rule) => rule.cssText);
      } catch {
        return [];
      }
    });

    return {
      app: font('#app-label'),
      appControl: font('#app-control'),
      viewer: font('#viewer-label'),
      viewerControl: font('#viewer-control'),
      portal: font('#portal-control'),
      accent: getComputedStyle(document.documentElement)
        .getPropertyValue('--ui-accent')
        .trim(),
      hasPretendardFace: rules.some(
        (rule) =>
          rule.includes('@font-face') && rule.includes('Pretendard Variable'),
      ),
    };
  });

  expect(contract.app).toContain('Pretendard Variable');
  expect(contract.appControl).toContain('Pretendard Variable');
  expect(contract.portal).toContain('Pretendard Variable');
  expect(contract.viewer).toContain('SUIT-Regular');
  expect(contract.viewerControl).toContain('SUIT-Regular');
  expect(contract.accent).toBe('#ba712b');
  expect(contract.hasPretendardFace).toBe(true);
});

test('viewer-only entry does not load app typography or tokens', async ({
  page,
}) => {
  await page.goto('/visual/index.html?surface=overlay&fixture=hand-default');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const contract = await page.evaluate(() => {
    const rules = Array.from(document.styleSheets).flatMap((sheet) => {
      try {
        return Array.from(sheet.cssRules, (rule) => rule.cssText);
      } catch {
        return [];
      }
    });
    return {
      rootFont: getComputedStyle(document.documentElement).fontFamily,
      accent: getComputedStyle(document.documentElement)
        .getPropertyValue('--ui-accent')
        .trim(),
      hasPretendardFace: rules.some(
        (rule) =>
          rule.includes('@font-face') && rule.includes('Pretendard Variable'),
      ),
    };
  });

  expect(contract.rootFont).toContain('SUIT-Regular');
  expect(contract.accent).toBe('');
  expect(contract.hasPretendardFace).toBe(false);
});

test('a held ghost key replaces its key and the newest press shows', async ({
  page,
}) => {
  await page.goto('/visual/index.html?surface=overlay&fixture=hand-ghost');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  const keys = page.locator('[data-viewer-layer="keys"]');
  const press = (key: string, active: boolean) =>
    page.evaluate(([k, a]) => window.__VISUAL_SET_KEY__?.(k, a), [
      key,
      active,
    ] as const);

  await press('E', false);
  await expect(keys).toHaveText('E');
  await press('R', true);
  await expect(keys).toHaveText('R');
  await press('E', true);
  await expect(keys).toHaveText('E');
  await press('R', false);
  await press('R', true);
  await expect(keys).toHaveText('R');
  await press('R', false);
  await expect(keys).toHaveText('E');
  await press('E', false);
  await expect(keys).toHaveText('E');
});

test('primitive controls preserve controlled input semantics', async ({
  page,
}) => {
  await page.goto('/visual/primitives.html?state=controls');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  await expect(page).toHaveScreenshot('primitives-controls.png');

  const amount = page.getByRole('textbox', { name: 'Amount', exact: true });
  await amount.focus();
  await amount.press('ArrowUp');
  await expect(page.getByTestId('amount-value')).toHaveText('25');
  await amount.press('Shift+ArrowDown');
  await expect(page.getByTestId('amount-value')).toHaveText('15');

  const toggle = page.getByRole('switch', { name: 'Enable effect' });
  await expect(toggle).toHaveAttribute('aria-checked', 'true');
  await toggle.press('Space');
  await expect(toggle).toHaveAttribute('aria-checked', 'false');
  await expect(page.getByRole('button', { name: 'Saving' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Saving' })).toHaveAttribute(
    'aria-busy',
    'true',
  );
});

test('app motion gives pointer feedback and respects reduced motion', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await page.goto('/visual/primitives.html?state=controls');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const apply = page.getByRole('button', { name: 'Apply' });
  await apply.hover();
  await expect
    .poll(() =>
      apply.evaluate((element) => getComputedStyle(element).transform),
    )
    .not.toBe('none');

  const toggle = page.getByRole('switch', { name: 'Enable effect' });
  // The knob moves between its two places without overshoot, and is longer
  // and thinner on the way.
  await expect(toggle.locator('.dmn-toggle-thumb')).toHaveCSS(
    'transition-duration',
    '0.2s',
  );
  await expect(toggle.locator('.dmn-toggle-thumb')).toHaveCSS(
    'transition-timing-function',
    'cubic-bezier(0.65, 0, 0.35, 1)',
  );
  await toggle.click();
  await expect(toggle.locator('.dmn-toggle-dot')).toHaveCSS(
    'animation-name',
    /dmn-toggle-stretch-(on|off)/,
  );

  const thumb = page.locator('.dmn-segment-thumb');
  const initialTransform = await thumb.evaluate(
    (element) => getComputedStyle(element).transform,
  );
  await page.getByRole('button', { name: 'Counter' }).click();
  await expect
    .poll(() =>
      thumb.evaluate((element) => getComputedStyle(element).transform),
    )
    .not.toBe(initialTransform);

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.mouse.move(0, 0);
  await apply.hover();
  await expect(apply).toHaveCSS('transform', 'none');
});

test('dropdown supports keyboard selection and viewport clamping', async ({
  page,
}) => {
  await page.goto('/visual/primitives.html?state=dropdown');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const trigger = page.getByRole('button', { name: 'Theme' });
  await trigger.click();
  const listbox = page.getByRole('listbox', { name: 'Theme' });
  await expect(listbox).toBeVisible();
  const bounds = await listbox.boundingBox();
  expect(bounds).not.toBeNull();
  expect((bounds?.x ?? -1) >= 5).toBe(true);
  expect((bounds?.x ?? 0) + (bounds?.width ?? 0) <= 1275).toBe(true);
  expect((bounds?.y ?? -1) >= 5).toBe(true);
  expect((bounds?.y ?? 0) + (bounds?.height ?? 0) <= 715).toBe(true);
  await expect(page).toHaveScreenshot('primitives-dropdown.png');

  await trigger.press('ArrowDown');
  await trigger.press('Enter');
  await expect(page.getByTestId('dropdown-value')).toHaveText('elevated');
  await trigger.press('Space');
  await expect(listbox).toBeVisible();
  await trigger.press('Escape');
  await expect(listbox).toHaveAttribute('data-dmn-motion-state', 'closing');
  await expect(listbox).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('modal traps focus while popup and tooltip keep their own roles', async ({
  page,
}) => {
  await page.goto('/visual/primitives.html?state=modal');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const dialog = page.getByRole('dialog', { name: 'Primitive dialog' });
  await expect(dialog).toBeVisible();
  await expect(page.getByRole('menu', { name: 'Quick actions' })).toBeVisible();
  const nameInput = page.getByRole('textbox', { name: 'Dialog name' });
  await expect(nameInput).toBeFocused();

  const save = page.getByTestId('dialog-save');
  await save.focus();
  await save.press('Tab');
  await expect(nameInput).toBeFocused();

  const tooltipTarget = page.getByRole('button', { name: 'Tooltip target' });
  await tooltipTarget.focus();
  await expect(page.getByRole('tooltip')).toBeVisible();
  await expect(page).toHaveScreenshot('primitives-modal.png');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('tooltip')).toBeHidden();
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(page.getByTestId('modal-state')).toHaveText('closed');

  const opener = page.getByRole('button', { name: 'Open dialog' });
  await opener.click();
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(opener).toBeFocused();
});

const openEditor = async (
  page: Page,
  selection: 'none' | 'single' | 'batch',
  size = { width: 1120, height: 720 },
) => {
  await page.setViewportSize(size);
  await page.goto(
    `/visual/shell.html?state=key&language=en&selection=${selection}`,
  );
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
};

const bounds = async (page: Page, selector: string) => {
  const box = await page.locator(selector).first().boundingBox();
  if (!box) throw new Error(`${selector} is not on the page`);
  return box;
};

/** Resolves when the page that is showing has finished arriving. */
const settled = (page: Page) =>
  page
    .locator('.en-page')
    .last()
    .evaluate((element) =>
      Promise.all(
        element.getAnimations().map((animation) => animation.finished),
      ).then(() => undefined),
    );

/** Top and height of each head row of the sidebar page that is showing. */
const headRows = (page: Page) =>
  page
    .locator('.en-page')
    .last()
    .evaluate((element) =>
      Array.from(element.querySelector('.en-page-head')!.children).map(
        (row) => {
          const rect = row.getBoundingClientRect();
          return [row.className, rect.top, rect.height];
        },
      ),
    );

test('one bar holds both key viewers and the app; the sidebar is only about elements', async ({
  page,
}) => {
  await openEditor(page, 'none');

  const root = page.locator('[data-shell-root="true"]');
  const canvas = await bounds(page, '.visual-shell-canvas');
  const sidebar = await bounds(page, '.en-sidebar');
  // Side by side: the sidebar never covers the canvas, and nothing is left
  // of a bottom toolbar.
  expect(canvas.x + canvas.width).toBeCloseTo(sidebar.x, 0);
  expect(sidebar.width).toBe(336);
  await expect(page.locator('.dmn-toolbar')).toHaveCount(0);
  expect(canvas.y + canvas.height).toBeCloseTo(
    (await root.boundingBox())!.height,
    0,
  );

  // The window's bar holds the tabs of the two views, as a browser does.
  // Under the bar are the canvas and the sidebar, which both belong to the
  // open tab; no second row is spent on anything.
  const bar = page.locator('.en-titlebar');
  expect((await bar.boundingBox())!.height).toBe(46);
  expect(canvas.y).toBeCloseTo(46, 0);
  expect(sidebar.y).toBeCloseTo(46, 0);

  // A tab is a view. Both are always there and say which key viewer they
  // show; the open one runs straight into the workspace.
  const tabs = bar.getByRole('tablist').getByRole('tab');
  await expect(tabs).toHaveText(['Hands16K', 'FeetPedal']);
  await expect(tabs.first()).toHaveAttribute('aria-selected', 'true');
  const tabBox = (await tabs.first().boundingBox())!;
  expect(tabBox.y + tabBox.height).toBeCloseTo(46, 0);

  // Which key viewer the open view shows is changed by one button, a value
  // with an arrow, floating in the canvas's top corner.
  await expect(bar.locator('.en-picker')).toHaveCount(0);
  const picker = page
    .locator('.visual-shell-canvas')
    .getByRole('button', { name: 'Key viewer 16K' });
  await expect(picker.locator('.en-picker-arrow')).toBeVisible();
  const pickerBox = (await picker.boundingBox())!;
  expect(pickerBox.x - canvas.x).toBeCloseTo(10, 0);
  expect(pickerBox.y - canvas.y).toBeCloseTo(10, 0);
  expect(pickerBox.x + pickerBox.width).toBeLessThan(
    canvas.x + canvas.width / 2,
  );

  // Pressing the other tab only swaps the view: the tabs stay where they
  // are, and the button on the canvas is now the feet's key viewer.
  const tabPlaces = () =>
    tabs.evaluateAll((items) =>
      items.map((item) => {
        const box = item.getBoundingClientRect();
        return [box.left, box.width];
      }),
    );
  const placesBefore = await tabPlaces();
  await tabs.nth(1).click();
  await expect(tabs.nth(1)).toHaveAttribute('aria-selected', 'true');
  expect(await tabPlaces()).toEqual(placesBefore);
  await expect(
    page
      .locator('.visual-shell-canvas')
      .getByRole('button', { name: 'Key viewer Pedal' }),
  ).toBeVisible();
  await tabs.first().click();
  await expect(tabs.first()).toHaveAttribute('aria-selected', 'true');

  // Where the key viewers are shown is switched in settings only; the bar
  // has no switch of its own, and the game connection leads there.
  await expect(bar.getByRole('switch')).toHaveCount(0);
  await expect(
    bar.getByRole('button', { name: 'Settings', exact: true }),
  ).toBeVisible();

  // The sidebar starts at the element list and says what adds an element.
  const list = page.locator('.en-page');
  await expect(list.getByRole('heading', { name: 'Elements' })).toBeVisible();
  await expect(list.getByText('Add an element', { exact: true })).toBeVisible();
  for (const name of ['Key', 'Stat', 'Graph', 'Knob', 'Image']) {
    await expect(
      list.getByRole('button', { name: `Add ${name}`, exact: true }),
    ).toBeVisible();
  }
  // Nothing that applies to the whole app or the whole key viewer is here.
  for (const name of ['Presets', 'Background', 'Reset', 'Raining speed']) {
    await expect(page.locator('.en-sidebar').getByText(name)).toHaveCount(0);
  }
  // Every control has a name; none is an unlabeled icon.
  const unnamed = await page
    .locator('.en-sidebar button, .en-titlebar button')
    .evaluateAll(
      (buttons) =>
        buttons.filter(
          (button) =>
            !button.textContent?.trim() && !button.getAttribute('aria-label'),
        ).length,
    );
  expect(unnamed).toBe(0);
  await expect(root).toHaveScreenshot('shell-editor-list.png');

  // The list is the page: it reaches the bottom of the sidebar and is the
  // only thing that scrolls.
  const elements = await bounds(page, '.en-element-list');
  expect(elements.y + elements.height).toBeCloseTo(
    sidebar.y + sidebar.height,
    0,
  );
  expect(
    await list
      .locator('.en-page-body')
      .evaluate((body) => getComputedStyle(body).overflowY),
  ).toBe('hidden');

  // Pressing an element in the list opens its page; the way back says where.
  // The row's picture writes the key's name too; the row's name is pressed.
  await list
    .getByText('Space', { exact: true })
    .and(list.locator(':not(.en-key-thumb-label)'))
    .click();
  // The element's page has no title: the canvas shows what is selected.
  await expect(page.locator('.en-page')).toHaveAttribute('aria-label', 'Space');
  await expect(page.locator('.en-page-title-row')).toHaveCount(0);
  await expect(page.locator('.en-page-back')).toHaveText('Elements');
  await page.locator('.en-page-back').click();
  await expect(
    page.locator('.en-page').getByRole('heading', { name: 'Elements' }),
  ).toBeVisible();
});

test('an element page edits small things in place and opens a page only for big ones', async ({
  page,
}) => {
  await openEditor(page, 'single');

  const page1 = page.locator('.en-page');
  await expect(page1).toHaveAttribute('aria-label', 'Space');
  // Nothing names or renames the key here; Delete is the one action.
  await expect(page1.locator('.en-page-head input')).toHaveCount(0);
  await expect(page1.locator('.en-page-actions button')).toHaveText(['Delete']);
  const inPlace = ['Input', 'Position', 'Size'];
  const pages = ['Shape and color', 'Text', 'Raining', 'Counts', 'Sound'];
  // Every part is a row led by its picture; what is entered opens a page.
  await expect(page.locator('.en-row-label')).toHaveText([
    ...inPlace,
    ...pages,
    'Ghost key',
  ]);
  await expect(page.locator('.en-row > .en-row-icon')).toHaveCount(
    inPlace.length + pages.length + 1,
  );

  // A single value or a few numbers are edited on the row itself: the row
  // holds its control and opens nothing.
  for (const name of inPlace) {
    const row = page.locator('.en-row').filter({
      has: page.locator('.en-row-label', { hasText: new RegExp(`^${name}$`) }),
    });
    await expect(row).toHaveAttribute('data-kind', 'field');
    await expect(row.locator('.en-row-hit')).toHaveCount(0);
  }
  await expect(page.getByLabel('Position X', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Size H', { exact: true })).toBeVisible();
  // A row that is entered is pressed anywhere on it and holds no control;
  // it says what the part is set to.
  const sidebarBox = await bounds(page, '.en-sidebar');
  const partRow = (name: string) =>
    page.locator('.en-row').filter({
      has: page.locator('.en-row-label', { hasText: new RegExp(`^${name}$`) }),
    });
  for (const name of pages) {
    const hit = page.getByRole('button', { name, exact: true });
    await expect(hit).toHaveClass(/en-row-hit/);
    await expect(partRow(name).locator('input, [role="switch"]')).toHaveCount(
      0,
    );
    // Large enough to press without aiming, and inside the sidebar.
    const box = (await hit.boundingBox())!;
    expect(box.height).toBeGreaterThanOrEqual(48);
    expect(box.width).toBeGreaterThanOrEqual(240);
    expect(box.x + box.width).toBeLessThanOrEqual(
      sidebarBox.x + sidebarBox.width,
    );
  }
  for (const part of ['Raining', 'Counts']) {
    await expect(partRow(part)).toContainText('On');
  }
  // Fields in rows are the size of every field in the sidebar.
  expect(
    (await page.getByLabel('Position X', { exact: true }).boundingBox())!
      .height,
  ).toBeGreaterThanOrEqual(30);
  // The head is one fixed row, the way back and Delete; under it, the list.
  await expect(page.locator('.en-page-head > *')).toHaveCount(1);
  await expect(page.locator('.en-page-head .dmn-segmented')).toHaveCount(0);
  await expect(page.locator('.en-group-card')).toHaveCount(0);

  // Typing in a field moves nothing, whatever is in it.
  const rowTops = () =>
    page
      .locator('.en-row')
      .evaluateAll((rows) =>
        rows.map((row) => [
          row.getBoundingClientRect().top,
          row.getBoundingClientRect().height,
        ]),
      );
  const tops = await rowTops();
  const x = page.getByLabel('Position X', { exact: true });
  await x.fill('');
  expect(await rowTops()).toEqual(tops);
  await x.fill('99999');
  expect(await rowTops()).toEqual(tops);
  await page.keyboard.press('Escape');

  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveScreenshot('shell-editor.png');

  const home = await headRows(page);
  expect(home).toHaveLength(1);
  let partHead: Awaited<ReturnType<typeof headRows>> | undefined;
  const sidebar = await bounds(page, '.en-sidebar');
  for (const part of pages) {
    await page.getByRole('button', { name: part, exact: true }).click();
    await expect(
      page.locator('.en-page').getByRole('heading', { name: part }),
    ).toBeVisible();
    await settled(page);
    // The way back is at the same place and size as on the element's page,
    // and every part's page has its title in the same row under it.
    const head = await headRows(page);
    expect(head[0]).toEqual(home[0]);
    expect(head).toHaveLength(2);
    partHead ??= head;
    expect(head).toEqual(partHead);
    // The part's settings stay inside the sidebar.
    const body = await bounds(page, '.en-page-body');
    expect(body.x).toBeGreaterThanOrEqual(sidebar.x);
    expect(body.x + body.width).toBeLessThanOrEqual(sidebar.x + sidebar.width);
    await page.locator('.en-page-back').click();
    await expect(page.locator('.en-row-label').first()).toHaveText('Input');
  }

  // The second face of a key is a row of its own, with a page of its own.
  await page.getByRole('button', { name: 'Ghost key', exact: true }).click();
  await expect(
    page.locator('.en-page').getByRole('heading', { name: 'Ghost key' }),
  ).toBeVisible();
  await expect(
    page.locator('.en-page-trailing').getByRole('switch'),
  ).toBeVisible();
  await expect(page.locator('.en-page-back')).toHaveText('Space');
});

test('switching Raining off keeps its settings in place', async ({ page }) => {
  await openEditor(page, 'single');
  await page.getByRole('button', { name: 'Raining', exact: true }).click();

  const rowTops = () =>
    page
      .locator('.en-part-content .dmn-property-row')
      .evaluateAll((rows) =>
        rows.map((row) => Math.round(row.getBoundingClientRect().top)),
      );
  const before = await rowTops();
  expect(before.length).toBeGreaterThan(4);

  const power = page.locator('.en-page-trailing').getByRole('switch');
  await power.click();
  await expect(power).toHaveAttribute('aria-checked', 'false');
  await expect(page.locator('.en-page-state')).toHaveText('Off');
  // Nothing appears or disappears: the same rows at the same places.
  expect(await rowTops()).toEqual(before);
  await expect(
    page.locator('.en-part-content .en-dimmed').first(),
  ).toHaveAttribute('inert', '');
  await expect(page.locator('[data-shell-root="true"]')).toHaveScreenshot(
    'shell-part-off.png',
  );

  // The first page says the same state, now off.
  await page.locator('.en-page-back').click();
  await expect(
    page.locator('.en-row').filter({ hasText: 'Raining' }),
  ).toContainText('Off');
});

test('choosing opens a chooser beside the sidebar that points at its row; the page stays', async ({
  page,
}) => {
  await openEditor(page, 'single');
  await page.getByRole('button', { name: 'Raining', exact: true }).click();
  const sidebar = await bounds(page, '.en-sidebar');
  const heading = page
    .locator('.en-page')
    .getByRole('heading', { name: 'Raining' });
  await settled(page);
  const headBefore = await headRows(page);
  const row = page
    .locator('.en-part-content .en-section')
    .filter({ has: page.locator('.en-section-title', { hasText: 'Notes' }) })
    .locator('.dmn-property-row')
    .filter({
      has: page.locator('.dmn-property-row__label', { hasText: /^Color$/ }),
    });
  const rowBefore = (await row.boundingBox())!;

  await row.getByRole('button').click();
  const chooser = page.locator('.en-chooser');
  await expect(chooser).toBeVisible();
  await chooser.evaluate((element) =>
    Promise.all(
      element.getAnimations().map((animation) => animation.finished),
    ).then(() => undefined),
  );
  // Nothing navigated and nothing moved: the page that opened it is as it was.
  await expect(heading).toBeVisible();
  expect(await headRows(page)).toEqual(headBefore);
  expect(await row.boundingBox()).toEqual(rowBefore);
  // It is beside the sidebar and covers none of it.
  const box = (await chooser.boundingBox())!;
  expect(box.x + box.width).toBeLessThanOrEqual(sidebar.x);
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.y).toBeGreaterThanOrEqual(0);
  // Its pointer is at the row it was opened from.
  const pointer = (await chooser.locator('.en-chooser-pointer').boundingBox())!;
  const pointerMiddle = pointer.y + pointer.height / 2;
  expect(pointerMiddle).toBeGreaterThanOrEqual(rowBefore.y);
  expect(pointerMiddle).toBeLessThanOrEqual(rowBefore.y + rowBefore.height);
  await expect(page.locator('[data-shell-root="true"]')).toHaveScreenshot(
    'shell-chooser.png',
  );

  // Escape closes it, and the page is as it was.
  await page.keyboard.press('Escape');
  await expect(chooser).toHaveCount(0);
  await expect(heading).toBeVisible();
  expect(await headRows(page)).toEqual(headBefore);
});

test('a color with two states shows both in its row; nothing is switched inside the chooser', async ({
  page,
}) => {
  await openEditor(page, 'single');
  await page
    .getByRole('button', { name: 'Shape and color', exact: true })
    .click();
  await settled(page);
  const sidebar = await bounds(page, '.en-sidebar');
  for (const color of ['Background', 'Border Color']) {
    const pair = page.getByRole('group', { name: color, exact: true });
    // Each state is a button with its color and its name.
    await expect(pair.locator('.en-state')).toHaveText(['At rest', 'Pressed']);
    await expect(pair.locator('.en-state-swatch')).toHaveCount(2);
    const box = (await pair.boundingBox())!;
    expect(box.x + box.width).toBeLessThanOrEqual(sidebar.x + sidebar.width);
  }

  const rest = page.getByRole('button', { name: 'Background: At rest' });
  const pressed = page.getByRole('button', { name: 'Background: Pressed' });
  const chooser = page.locator('.en-chooser');
  await pressed.click();
  await expect(chooser).toBeVisible();
  await expect(pressed).toHaveAttribute('aria-expanded', 'true');
  await expect(rest).toHaveAttribute('aria-expanded', 'false');
  // The chooser has no switch of its own between the two.
  await expect(chooser.getByText('At rest')).toHaveCount(0);
  await expect(chooser.getByText('Pressed')).toHaveCount(0);
  await expect(chooser.getByRole('tab')).toHaveCount(0);
  // The other state of the same color: the chooser stays and changes what
  // it is about.
  const fieldBefore = await chooser
    .locator('.en-color-input')
    .first()
    .inputValue();
  await rest.click();
  await expect(chooser).toBeVisible();
  await expect(rest).toHaveAttribute('aria-expanded', 'true');
  await expect(pressed).toHaveAttribute('aria-expanded', 'false');
  await expect(chooser.locator('.en-color-input').first()).not.toHaveValue(
    fieldBefore,
  );
  // Pressing the open one closes it.
  await rest.click();
  await expect(chooser).toHaveCount(0);
});

test('several keys share one page, and its switches show mixed states', async ({
  page,
}) => {
  await openEditor(page, 'batch');
  // The page of what is selected has no title: the canvas shows it.
  await expect(page.locator('.en-page')).toHaveAttribute(
    'aria-label',
    '2 selected',
  );
  await expect(page.locator('.en-page-title-row')).toHaveCount(0);
  await expect(page.locator('.en-row-label')).toContainText([
    'Shape and color',
    'Text',
    'Raining',
    'Counts',
    'Sound',
  ]);
  await page.getByRole('button', { name: 'Counts', exact: true }).click();
  await expect(page.locator('.en-page-state')).toHaveText('On');
  await page.locator('.en-page-trailing').getByRole('switch').click();
  await expect(page.locator('.en-page-state')).toHaveText('Off');
});

test('dragging the edge of the sidebar resizes it and the canvas gives way', async ({
  page,
}) => {
  await openEditor(page, 'none');
  const handle = page.getByRole('separator', { name: 'Resize sidebar' });
  const start = await bounds(page, '.en-sidebar');
  const grip = (await handle.boundingBox())!;
  const y = grip.y + grip.height / 2;

  await page.mouse.move(grip.x + grip.width / 2, y);
  await page.mouse.down();
  await page.mouse.move(grip.x + grip.width / 2 - 100, y, { steps: 4 });
  await page.mouse.up();
  const wider = await bounds(page, '.en-sidebar');
  expect(wider.width).toBe(start.width + 100);
  const canvas = await bounds(page, '.visual-shell-canvas');
  expect(canvas.x + canvas.width).toBeCloseTo(wider.x, 0);

  // It stops at its limits instead of squeezing the canvas away.
  await page.mouse.move(wider.x, y);
  await page.mouse.down();
  await page.mouse.move(0, y, { steps: 4 });
  await page.mouse.up();
  expect((await bounds(page, '.en-sidebar')).width).toBe(560);

  await handle.dblclick();
  expect((await bounds(page, '.en-sidebar')).width).toBe(336);
});

test('editor shell keeps app chrome geometry', async ({ page }) => {
  await openEditor(page, 'single');
  await page
    .getByRole('button', { name: 'Shape and color', exact: true })
    .click();

  const contract = await page
    .locator('[data-shell-root="true"]')
    .evaluate((element) => {
      const frame = getComputedStyle(element);
      const height = (selector: string) =>
        element.querySelector<HTMLElement>(selector)!.getBoundingClientRect()
          .height;
      const propertyLabelStyle = getComputedStyle(
        element.querySelector<HTMLElement>('.dmn-property-row__label')!,
      );
      return {
        size: [
          element.getBoundingClientRect().width,
          element.getBoundingClientRect().height,
        ],
        titlebarHeight: height('.dmn-titlebar'),
        frameRadius: frame.borderRadius,
        head: ['.en-page-nav', '.en-page-title-row'].map(height),
        propertyRowHeight: height('.dmn-property-row'),
        propertyLabelTypography: [
          propertyLabelStyle.fontSize,
          propertyLabelStyle.lineHeight,
          propertyLabelStyle.fontWeight,
        ],
        appTypography: [frame.fontSize, frame.lineHeight, frame.fontWeight],
      };
    });
  expect(contract.size).toEqual([1120, 720]);
  expect(contract.titlebarHeight).toBe(46);
  expect(contract.head).toEqual([38, 40]);
  // The sidebar is a size up from the rest of the app: one setting a line.
  expect(contract.propertyRowHeight).toBe(44);
  expect(contract.propertyLabelTypography).toEqual(['14px', '20px', '500']);
  expect(contract.appTypography).toEqual(['12px', '18px', '500']);
  expect(contract.frameRadius).toBe('8px');
});

test('settings categories lead to short pages and add-on management', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1120, height: 720 });
  await page.goto('/visual/shell.html?state=settings&language=en');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveScreenshot('shell-settings.png');

  await expect(
    page
      .getByRole('navigation', { name: 'Settings categories' })
      .getByRole('button'),
  ).toHaveCount(7);
  // Settings open on where the key viewers are shown: inside the game first,
  // then the desktop, then OBS as an extra. Each has a switch of its own.
  await expect(
    page.getByRole('heading', { name: 'Where to show', exact: true }),
  ).toBeVisible();
  // The three places are large buttons with a small moving picture each,
  // and one of them is in use at a time.
  const places = page.locator('[data-output-button]');
  await expect(places).toHaveCount(3);
  expect(
    await places.evaluateAll((buttons) =>
      buttons.map((button) => [
        button.getAttribute('data-output-button'),
        button.getAttribute('role'),
        button.getAttribute('aria-checked'),
        button.querySelector('svg') !== null,
      ]),
    ),
  ).toEqual([
    ['game', 'radio', 'true', true],
    ['desktop', 'radio', 'false', true],
    ['obs', 'radio', 'false', true],
  ]);
  // In one row, the same size, whichever is chosen.
  const placeBoxes = () =>
    places.evaluateAll((buttons) =>
      buttons.map((button) => {
        const box = button.getBoundingClientRect();
        return [
          Math.round(box.top),
          Math.round(box.width),
          Math.round(box.height),
        ];
      }),
    );
  const boxes = await placeBoxes();
  expect(new Set(boxes.map((box) => box.join())).size).toBe(1);

  // The place in use moves its picture; the others stand still. Choosing
  // another one moves the choice, and the settings under the buttons are
  // those of the chosen place only.
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  const moving = (place: string) =>
    page
      .locator(`[data-output-button="${place}"] .en-output-window`)
      .evaluate((element) => getComputedStyle(element).animationName);
  expect(await moving('game')).toBe('en-output-drift');
  expect(await moving('desktop')).toBe('none');
  await expect(page.locator('[data-output]')).toHaveCount(0);
  await page.getByRole('radio', { name: 'On the desktop' }).click();
  await expect(
    page.getByRole('radio', { name: 'On the desktop' }),
  ).toHaveAttribute('aria-checked', 'true');
  await expect(
    page.getByRole('radio', { name: 'Inside ADOFAI' }),
  ).toHaveAttribute('aria-checked', 'false');
  expect(await moving('game')).toBe('none');
  expect(await moving('desktop')).toBe('en-output-drift');
  await expect(page.locator('[data-output="desktop"]')).toBeVisible();
  await expect(
    page.getByRole('switch', { name: 'Lock position' }),
  ).toBeVisible();
  expect(await placeBoxes()).toEqual(boxes);
  await page.getByRole('radio', { name: 'OBS broadcast (extra)' }).click();
  await expect(page.locator('[data-output="desktop"]')).toHaveCount(0);
  await expect(page.locator('[data-output="obs"]')).toBeVisible();
  // The key limiter is about the game, whatever place is chosen.
  await expect(page.getByRole('switch', { name: 'Key limiter' })).toBeVisible();
  await page.getByRole('radio', { name: 'Inside ADOFAI' }).click();
  await expect(page.locator('[data-output]')).toHaveCount(0);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // Nothing tells the player to turn OBS on for the game.
  await expect(
    page.locator('[data-output-button="game"]').getByText(/OBS/),
  ).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Broadcast', exact: true }),
  ).toHaveCount(0);

  // What applies to every key lives here, not in the editor's sidebar.
  await page.getByRole('button', { name: 'Viewer', exact: true }).click();
  // Raining has no dialog: each of its values is a row here, kept as it is
  // changed, for every key viewer or for one of them.
  for (const name of [
    'Background',
    'Overlay Padding',
    'Key viewer to set Raining for',
    'Raining speed',
    'Raining length',
    'Flow downward',
    'Fade at the ends',
    'Keep short Raining one length',
    'Delay the key press',
  ]) {
    await expect(
      page
        .locator('.dmn-setting-row__label')
        .filter({ hasText: new RegExp(`^${name}$`) }),
    ).toHaveCount(1);
  }
  await expect(page.getByRole('button', { name: 'Configure' })).toHaveCount(0);
  // Rows that follow a switch stay in place while it is off.
  const shortLength = page.getByRole('textbox', {
    name: 'Short Raining length',
  });
  await expect(shortLength).toBeDisabled();
  await page
    .getByRole('switch', { name: 'Keep short Raining one length' })
    .click();
  await expect(shortLength).toBeEnabled();
  await page
    .getByRole('switch', { name: 'Keep short Raining one length' })
    .click();
  await expect(page.locator('[data-setting-help="perKey"]')).toHaveCount(1);
  // Leaving settings is the first thing in the corner: left of the title.
  const leave = (await page
    .getByRole('button', { name: 'Back to the editor' })
    .boundingBox())!;
  const title = (await page
    .getByRole('heading', { name: 'Settings', exact: true })
    .boundingBox())!;
  expect(leave.x + leave.width).toBeLessThanOrEqual(title.x);
  expect(
    Math.abs(leave.y + leave.height / 2 - (title.y + title.height / 2)),
  ).toBeLessThan(2);
  const geometry = await root.evaluate((element) => {
    const content = element.querySelector<HTMLElement>('.en-settings-page')!;
    return {
      clientHeight: content.clientHeight,
      scrollHeight: content.scrollHeight,
      clientWidth: content.clientWidth,
      scrollWidth: content.scrollWidth,
    };
  });
  expect(geometry.scrollWidth).toBe(geometry.clientWidth);

  await page
    .getByRole('button', { name: 'Editing screen', exact: true })
    .click();
  await expect(
    page.getByRole('switch', { name: 'Alignment Guides' }),
  ).toBeVisible();
  // Presets are kept as files from the key viewer gallery, not from here.
  await expect(
    page.getByRole('button', { name: 'Presets', exact: true }),
  ).toHaveCount(0);
  // How often Raining is drawn is a matter of the computer: it is advanced.
  await page.getByRole('button', { name: 'Advanced', exact: true }).click();
  await expect(
    page.getByRole('textbox', { name: 'Raining frame limit' }),
  ).toBeVisible();

  await page.getByRole('button', { name: 'Add-ons', exact: true }).click();
  await page.getByRole('button', { name: /manage plugins/i }).click();
  const content = page.locator('.en-settings-page');
  await expect(
    content.getByRole('heading', { name: 'Add-on features', exact: true }),
  ).toBeVisible();
  await expect(content.getByText('Input Meter', { exact: true })).toBeVisible();
  await expect(root).toHaveScreenshot('shell-settings-plugins.png');

  await content
    .getByRole('button', { name: /manage plugins|플러그인/i })
    .click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.locator('.dmn-manager-surface')).toBeVisible();
  await expect(page.locator('body')).toHaveScreenshot(
    'shell-plugin-manager.png',
  );
});

test('font and sound managers share the management surface', async ({
  page,
}) => {
  await page.setViewportSize({ width: 902, height: 488 });
  await page.goto('/visual/shell.html?state=font');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  await expect(page.locator('.dmn-manager-surface')).toBeVisible();
  await expect(page.locator('body')).toHaveScreenshot('shell-font-manager.png');

  await page.goto('/visual/shell.html?state=sound');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  await expect(page.getByText('Mechanical Click')).toBeVisible();
  await expect(page.locator('.dmn-manager-surface')).toBeVisible();
  await expect(page.locator('body')).toHaveScreenshot(
    'shell-sound-manager.png',
  );
});

test('shell CSS geometry is stable at DPR 1 and DPR 2', async ({ browser }) => {
  const collect = async (deviceScaleFactor: number) => {
    const context = await browser.newContext({
      viewport: { width: 902, height: 488 },
      deviceScaleFactor,
      locale: 'en-US',
      timezoneId: 'UTC',
      colorScheme: 'dark',
      reducedMotion: 'reduce',
    });
    const page = await context.newPage();
    await page.goto('http://127.0.0.1:3400/visual/shell.html?state=editor');
    await page.waitForFunction(() => window.__VISUAL_READY__ === true);
    const geometry = await page
      .locator('[data-shell-root="true"]')
      .evaluate((root) => {
        const rect = (selector: string) => {
          const bounds = root
            .querySelector<HTMLElement>(selector)!
            .getBoundingClientRect();
          return [bounds.x, bounds.y, bounds.width, bounds.height].map(
            (value) => Number(value.toFixed(2)),
          );
        };
        const rootBounds = root.getBoundingClientRect();
        return {
          root: [
            rootBounds.x,
            rootBounds.y,
            rootBounds.width,
            rootBounds.height,
          ].map((value) => Number(value.toFixed(2))),
          titlebar: rect('.dmn-titlebar'),
          sidebar: rect('.en-sidebar'),
          head: rect('.en-page-head'),
        };
      });
    await context.close();
    return geometry;
  };

  expect(await collect(2)).toEqual(await collect(1));
});

test('settings navigation and detail controls remain reachable in a narrow viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 700, height: 488 });
  await page.goto('/visual/shell.html?state=settings&language=en');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveCSS('width', '700px');
  await expect(
    page.getByRole('navigation', { name: 'Settings categories' }),
  ).toBeVisible();
  const geometry = await root.evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
    settingsNavigationWidth: element
      .querySelector<HTMLElement>('.en-settings-nav')!
      .getBoundingClientRect().width,
    settingsContentWidth: element
      .querySelector<HTMLElement>('.en-settings-page')!
      .getBoundingClientRect().width,
  }));
  expect(geometry.scrollWidth).toBe(geometry.clientWidth);
  expect(geometry.settingsNavigationWidth + geometry.settingsContentWidth).toBe(
    geometry.clientWidth,
  );

  await page.getByRole('button', { name: 'Add-ons', exact: true }).click();
  await page.getByRole('button', { name: 'Configure', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Appearance files (CSS)', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('switch', { name: 'Use appearance files', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: '← Add-ons', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Add-ons', exact: true }),
  ).toBeVisible();

  await page
    .getByRole('searchbox', { name: 'Find a setting', exact: true })
    .fill('counts');
  await expect(
    page.getByRole('heading', { name: 'Search results', exact: true }),
  ).toBeVisible();
  await page
    .locator('.en-settings-result')
    .filter({ has: page.getByText('Press counts', { exact: true }) })
    .click();
  await expect(page.locator('[data-setting-help="perKey"]')).toBeVisible();
  await expect(
    page.getByRole('searchbox', { name: 'Find a setting', exact: true }),
  ).toHaveValue('');
});

test('changing a slot shows only the key viewers made for it, as pictures', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1120, height: 720 });
  await page.goto(
    '/visual/shell.html?state=gallery&language=en&selection=none',
  );
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const gallery = page.locator('.en-gallery');
  await expect(
    gallery.getByRole('heading', { name: 'Key viewer for Hands' }),
  ).toBeVisible();
  // Only the hands' key viewers; the feet's are not shown here.
  await expect(gallery.locator('.en-card .en-card-name')).toHaveText([
    '16K',
    '10K',
    '4K',
  ]);
  await expect(gallery.getByText('Pedal')).toHaveCount(0);
  // Each one is drawn as it really looks, at rest: ten keys for 10K, four
  // for 4K, and nothing in the picture can be pressed.
  await expect(
    gallery.locator('[data-viewer-id="hand-second"] .en-thumb [data-state]'),
  ).toHaveCount(10);
  await expect(
    gallery.locator('[data-viewer-id="hand-third"] .en-thumb [data-state]'),
  ).toHaveCount(4);
  // The one that is up is marked by its frame, and only that one. The page
  // says nothing more than that: no help line, no badge.
  await expect(gallery.locator('.en-card[data-shown]')).toHaveCount(1);
  await expect(
    gallery.locator('.en-card[data-shown] .en-card-name'),
  ).toHaveText('16K');
  await expect(gallery.locator('p')).toHaveCount(0);
  await expect(gallery.getByText('Up', { exact: true })).toHaveCount(0);
  await expect(
    gallery.getByRole('button', { name: 'New key viewer' }),
  ).toBeVisible();
  // The cards share the whole width: the row ends where the page's room ends.
  await gallery
    .locator('.en-gallery-grid')
    .evaluate((element) =>
      Promise.all(
        element
          .getAnimations({ subtree: true })
          .map((animation) => animation.finished),
      ).then(() => undefined),
    );
  const cards = await gallery
    .locator('.en-gallery-grid > *')
    .evaluateAll((items) =>
      items.map((item) => {
        const box = item.getBoundingClientRect();
        return { left: box.left, right: box.right, width: box.width };
      }),
    );
  const grid = (await gallery.locator('.en-gallery-grid').boundingBox())!;
  expect(new Set(cards.map((card) => Math.round(card.width))).size).toBe(1);
  expect(cards[0].left).toBeCloseTo(grid.x, 0);
  expect(cards[cards.length - 1].right).toBeCloseTo(grid.x + grid.width, 0);
  // The picture is as wide as its card, and the key viewer is scaled with it.
  const picture = (await gallery
    .locator('[data-viewer-id="hand-second"] .en-card-picture')
    .boundingBox())!;
  const thumb = (await gallery
    .locator('[data-viewer-id="hand-second"] .en-thumb')
    .boundingBox())!;
  expect(picture.width).toBeCloseTo(cards[0].width, 0);
  expect(thumb.width).toBeCloseTo(picture.width, 0);
  expect(thumb.height).toBeCloseTo(picture.height, 0);
  // Every key viewer, with its settings, is kept as one file from the head.
  for (const name of ['Load all', 'Save all']) {
    await expect(
      gallery.getByRole('button', { name, exact: true }),
    ).toBeVisible();
  }
  // While choosing, the window bar is just the window bar, at the same height.
  expect((await page.locator('.en-titlebar').boundingBox())!.height).toBe(46);
  await expect(page.locator('.en-tab, .en-picker')).toHaveCount(0);
  await expect(page.locator('[data-shell-root="true"]')).toHaveScreenshot(
    'shell-gallery.png',
  );

  // Things done to a key viewer are in its own menu, which opens right under
  // the button that was pressed, and each has its mark.
  const more = gallery.getByRole('button', { name: 'Menu of 10K' });
  await more.click();
  const menu = page.getByRole('menu');
  for (const name of [
    'Rename',
    // One key viewer is kept as a file from its own card.
    'Save to a file',
    'Load from a file',
    'Set press counts to 0',
    'Reset to how it started',
    'Delete',
  ]) {
    const item = menu.getByRole('menuitem', { name, exact: true });
    await expect(item).toBeVisible();
    await expect(item.locator('svg')).toHaveCount(1);
  }
  await menu.evaluate((element) =>
    Promise.all(
      element.getAnimations().map((animation) => animation.finished),
    ).then(() => undefined),
  );
  const menuBox = (await menu.boundingBox())!;
  const moreBox = (await more.boundingBox())!;
  expect(menuBox.y).toBeGreaterThanOrEqual(moreBox.y + moreBox.height);
  expect(menuBox.y - (moreBox.y + moreBox.height)).toBeLessThan(12);
  expect(menuBox.x + menuBox.width).toBeCloseTo(moreBox.x + moreBox.width, 0);

  // Renaming happens on the card: the name becomes a field in the same place.
  const nameBox = (await gallery
    .locator('[data-viewer-id="hand-second"] .en-card-name')
    .boundingBox())!;
  await menu.getByRole('menuitem', { name: 'Rename', exact: true }).click();
  const renameField = gallery.locator(
    '[data-viewer-id="hand-second"] .en-card-name-input',
  );
  await expect(renameField).toBeFocused();
  await expect(renameField).toHaveValue('10K');
  await expect(page.locator('.dmn-modal-backdrop')).toHaveCount(0);
  const fieldBox = (await renameField.boundingBox())!;
  // The text stays where the name was: same line, same left edge. The field
  // is a line under the name, so nothing hangs outside the card.
  expect(fieldBox.y + fieldBox.height / 2).toBeCloseTo(
    nameBox.y + nameBox.height / 2,
    0,
  );
  expect(fieldBox.x).toBeCloseTo(nameBox.x, 0);
  const renamedCard = (await gallery
    .locator('[data-viewer-id="hand-second"]')
    .boundingBox())!;
  expect(fieldBox.x).toBeGreaterThanOrEqual(renamedCard.x);
  await page.keyboard.press('Escape');
  await expect(renameField).toHaveCount(0);
  await expect(gallery).toBeVisible();

  // A new key viewer is named where its card will be, and a name that is
  // taken is refused in place without moving the cards.
  const cardBoxes = () =>
    gallery
      .locator('.en-card[data-viewer-id]')
      .evaluateAll((cards) =>
        cards.map((card) => Math.round(card.getBoundingClientRect().top)),
      );
  const cardsBefore = await cardBoxes();
  await gallery.getByRole('button', { name: 'New key viewer' }).click();
  const newField = gallery.locator('.en-card-draft .en-card-name-input');
  await expect(newField).toBeFocused();
  // The field of a new key viewer starts where every name starts.
  const draft = (await gallery.locator('.en-card-draft').boundingBox())!;
  const draftField = (await newField.boundingBox())!;
  expect(draftField.x - draft.x).toBeCloseTo(nameBox.x - renamedCard.x, 0);
  expect(draftField.x + draftField.width).toBeLessThanOrEqual(
    draft.x + draft.width,
  );
  await page.keyboard.type('4K');
  await page.keyboard.press('Enter');
  await expect(gallery.getByRole('alert')).toBeVisible();
  expect(await cardBoxes()).toEqual(cardsBefore);
  await expect(page.locator('[data-shell-root="true"]')).toHaveScreenshot(
    'shell-gallery-name.png',
  );
  await page.keyboard.press('Escape');
  await expect(newField).toHaveCount(0);
});

test('a long element list says how much is below instead of showing a scrollbar', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1120, height: 440 });
  await page.goto(
    '/visual/shell.html?state=key&language=en&selection=none&viewer=hand-second',
  );
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const list = page.locator(
    '.en-element-list .properties-panel-overlay-viewport',
  );
  // No scrollbar takes room in the list.
  expect(
    await list.evaluate((view) => view.offsetWidth - view.clientWidth),
  ).toBe(0);
  // Every row shows that it can be dragged to another place.
  await expect(list.locator('.en-layer-grip')).toHaveCount(10);

  const more = page.locator('.en-list-more');
  await expect(more).toHaveAttribute('data-shown', 'true');
  // Rows are 48px high and 12px of room follows the last one.
  const hiddenRows = await list.evaluate((view) =>
    Math.round(
      (view.scrollHeight - view.scrollTop - view.clientHeight - 12) / 48,
    ),
  );
  await expect(more).toHaveText(`${hiddenRows} more below`);
  // It lies over the list, so showing it moved nothing.
  const listBox = (await list.boundingBox())!;
  const moreBox = (await more.boundingBox())!;
  expect(moreBox.y + moreBox.height).toBeLessThanOrEqual(
    listBox.y + listBox.height,
  );
  await expect(page.locator('[data-shell-root="true"]')).toHaveScreenshot(
    'shell-list-more.png',
  );

  // Pressing it goes down; at the end of the list it leaves.
  await more.click();
  await expect
    .poll(() => list.evaluate((view) => view.scrollTop))
    .toBeGreaterThan(0);
  await list.evaluate((view) => view.scrollTo(0, view.scrollHeight));
  await expect(more).not.toHaveAttribute('data-shown', 'true');
  await expect(more).toHaveCSS('opacity', '0');
});

for (const surface of surfaces) {
  for (const fixture of fixtures) {
    test(`${surface} preserves ${fixture}`, async ({ page }) => {
      await page.goto(
        `/visual/index.html?surface=${surface}&fixture=${fixture}`,
      );
      await page.waitForFunction(() => window.__VISUAL_READY__ === true);

      const root = page.locator('[data-visual-root="true"]');
      const contract = await collectContract(page);

      expect(
        contract.keys.every((key) => key.decorativeChildren.length === 0),
      ).toBe(true);
      expect(contract.note.speed).toBe(180);
      expect(contract.note.trackHeight).toBe(150);
      expect(contract.note.glowSize).toBe(20);

      if (fixture.startsWith('hand-')) {
        expect(contract.keys.at(-1)?.state).toBe('active');
      }

      if (fixture === 'hand-custom-css') {
        expect(contract.keys[0]?.style.borderStyle).toBe('none');
        expect(contract.keys[0]?.style.transform).not.toBe('none');
        expect(contract.keys[1]?.style.opacity).toBe('0.55');
      }

      if (fixture === 'foot-note-counter') {
        expect(contract.note.enabled).toBe(true);
        expect(contract.note.canvas).not.toBeNull();
        expect(contract.hiddenKeyCount).toBe(1);
      }

      expect(JSON.stringify(contract, null, 2)).toMatchSnapshot(
        `${surface}-${fixture}-contract.txt`,
      );
      await expect(root).toHaveScreenshot(`${surface}-${fixture}.png`);
    });
  }
}
