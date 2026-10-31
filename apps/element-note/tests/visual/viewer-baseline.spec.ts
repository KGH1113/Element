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
        canvas: canvas ? { width: canvas.width, height: canvas.height } : null,
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
  await expect(toggle.locator('.dmn-toggle-thumb')).toHaveCSS(
    'transition-duration',
    '0.24s',
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

test('OBS mode prompt asks one titled question with equal actions', async ({
  page,
}) => {
  await page.setViewportSize({ width: 902, height: 488 });
  await page.goto('/visual/shell.html?state=obs-prompt');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const card = page.locator('.dmn-obs-prompt');
  await expect(card).toHaveScreenshot('obs-mode-prompt.png');
  const enable = card.getByRole('button', { name: 'Turn on' });
  const dismiss = card.getByRole('button', { name: 'Not now' });
  await expect(enable).toBeFocused();
  const [enableBox, dismissBox] = await Promise.all([
    enable.boundingBox(),
    dismiss.boundingBox(),
  ]);
  // Same size and one line each; the primary action comes last.
  expect(enableBox?.width).toBe(dismissBox?.width);
  expect(enableBox?.height).toBe(dismissBox?.height);
  expect(enableBox!.x).toBeGreaterThan(dismissBox!.x);
});

test('editor shell keeps app chrome geometry and glass hierarchy', async ({
  page,
}) => {
  await page.setViewportSize({ width: 902, height: 488 });
  await page.goto('/visual/shell.html?state=editor');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveScreenshot('shell-editor.png');
  const contract = await root.evaluate((element) => {
    const frame = getComputedStyle(element);
    const panel = getComputedStyle(
      element.querySelector<HTMLElement>('.dmn-properties-panel')!,
    );
    const toolbar = element.querySelector<HTMLElement>('.dmn-toolbar')!;
    const titlebar = element.querySelector<HTMLElement>('.dmn-titlebar')!;
    const panelHeader = element.querySelector<HTMLElement>(
      '.dmn-properties-panel__header',
    )!;
    const propertyRow =
      element.querySelector<HTMLElement>('.dmn-property-row')!;
    const propertyLabel = element.querySelector<HTMLElement>(
      '.dmn-property-row__label',
    )!;
    const propertyLabelStyle = getComputedStyle(propertyLabel);
    return {
      size: [
        element.getBoundingClientRect().width,
        element.getBoundingClientRect().height,
      ],
      titlebarHeight: titlebar.getBoundingClientRect().height,
      toolbarHeight: toolbar.getBoundingClientRect().height,
      frameRadius: frame.borderRadius,
      panelWidth: element
        .querySelector<HTMLElement>('.dmn-properties-panel')!
        .getBoundingClientRect().width,
      panelHeaderHeight: panelHeader.getBoundingClientRect().height,
      propertyRowHeight: propertyRow.getBoundingClientRect().height,
      propertyLabelTypography: [
        propertyLabelStyle.fontSize,
        propertyLabelStyle.lineHeight,
        propertyLabelStyle.fontWeight,
      ],
      appTypography: [frame.fontSize, frame.lineHeight, frame.fontWeight],
      panelBackdrop: panel.backdropFilter,
      panelBackground: panel.backgroundColor,
    };
  });
  expect(contract.size).toEqual([902, 488]);
  expect(contract.titlebarHeight).toBe(30);
  expect(contract.toolbarHeight).toBe(60);
  expect(contract.panelWidth).toBe(240);
  expect(contract.panelHeaderHeight).toBe(48);
  expect(contract.propertyRowHeight).toBe(32);
  expect(contract.propertyLabelTypography).toEqual(['13px', '18px', '500']);
  expect(contract.appTypography).toEqual(['12px', '18px', '500']);
  expect(contract.frameRadius).toBe('8px');
  expect(contract.panelBackdrop).toContain('blur(18px)');
  expect(contract.panelBackground).not.toBe('rgba(0, 0, 0, 0)');
});

test('toolbar palette popup remains inside the shell viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 902, height: 488 });
  await page.goto('/visual/shell.html?state=editor');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  await page.getByRole('button', { name: 'Palette' }).click();

  // FloatingPopup owns the single popup surface; picker content is unpainted.
  const popup = page.locator('.dmn-popup-surface[data-dmn-app-portal]');
  await expect(popup).toBeVisible();
  const [popupBounds, rootBounds] = await Promise.all([
    popup.boundingBox(),
    page.locator('[data-shell-root="true"]').boundingBox(),
  ]);
  expect(popupBounds).not.toBeNull();
  expect(rootBounds).not.toBeNull();
  expect((popupBounds?.x ?? 0) >= (rootBounds?.x ?? 0)).toBe(true);
  expect(
    (popupBounds?.x ?? 0) + (popupBounds?.width ?? 0) <=
      (rootBounds?.x ?? 0) + (rootBounds?.width ?? 0),
  ).toBe(true);
  expect((popupBounds?.y ?? 0) >= (rootBounds?.y ?? 0)).toBe(true);
  expect(
    (popupBounds?.y ?? 0) + (popupBounds?.height ?? 0) <=
      (rootBounds?.y ?? 0) + (rootBounds?.height ?? 0),
  ).toBe(true);
  await expect(page.locator('body')).toHaveScreenshot(
    'shell-toolbar-popup.png',
  );
});

test('settings cards and plugin management use the fixed detail surface', async ({
  page,
}) => {
  await page.setViewportSize({ width: 902, height: 488 });
  await page.goto('/visual/shell.html?state=settings');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);
  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveScreenshot('shell-settings.png');

  const densityContract = await root.evaluate((element) => {
    const card = element.querySelector<HTMLElement>('.dmn-setting-card')!;
    const row = card.firstElementChild as HTMLElement;
    const label = row.querySelector<HTMLElement>('p')!;
    const cardStyle = getComputedStyle(card);
    const labelStyle = getComputedStyle(label);
    return {
      cardPadding: [cardStyle.paddingTop, cardStyle.paddingRight],
      rowHeight: row.getBoundingClientRect().height,
      labelTypography: [
        labelStyle.fontSize,
        labelStyle.lineHeight,
        labelStyle.fontWeight,
      ],
    };
  });
  expect(densityContract.cardPadding).toEqual(['8px', '14px']);
  expect(densityContract.rowHeight).toBe(36);
  expect(densityContract.labelTypography).toEqual(['13px', '18px', '500']);

  await page.getByRole('button', { name: 'Plugins', exact: true }).click();
  const sidePanel = page.getByRole('region', { name: /Plugins/i });
  await expect(sidePanel).toBeVisible();
  await expect(sidePanel.getByText('Input Meter')).toBeVisible();
  await expect(root).toHaveScreenshot('shell-settings-plugins.png');

  await sidePanel
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
          toolbar: rect('.dmn-toolbar'),
          panel: rect('.dmn-properties-panel'),
        };
      });
    await context.close();
    return geometry;
  };

  expect(await collect(2)).toEqual(await collect(1));
});

test('settings collapses to a single pane in a narrow resized viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 700, height: 488 });
  await page.goto('/visual/shell.html?state=settings');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const root = page.locator('[data-shell-root="true"]');
  await expect(root).toHaveCSS('width', '700px');
  await expect(page.locator('.dmn-settings-detail')).toBeHidden();
  const geometry = await root.evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
    settingsListWidth: element
      .querySelector<HTMLElement>('.dmn-settings-list')!
      .getBoundingClientRect().width,
    toolbarWidth: element
      .querySelector<HTMLElement>('.dmn-toolbar')!
      .getBoundingClientRect().width,
  }));
  expect(geometry.scrollWidth).toBe(geometry.clientWidth);
  expect(geometry.settingsListWidth).toBe(geometry.clientWidth);
  expect(geometry.toolbarWidth).toBe(geometry.clientWidth);
});

test('viewer tab popup anchors above the hand and foot triggers', async ({
  page,
}) => {
  await page.goto('/visual/shell.html?state=tabs');
  await page.waitForFunction(() => window.__VISUAL_READY__ === true);

  const triggers = page.locator('.viewer-tab-trigger');
  await expect(triggers).toHaveCount(2);

  for (let index = 0; index < 2; index += 1) {
    const trigger = triggers.nth(index);
    await trigger.click();

    const popup = page.locator('.viewer-tab-popup');
    await expect(popup).toBeVisible();
    // The popup has no arrow (matching upstream); it must sit above its
    // trigger and span the trigger's centre line.
    await expect
      .poll(async () => {
        const triggerBox = await trigger.boundingBox();
        const popupBox = await popup.boundingBox();
        if (!triggerBox || !popupBox) return false;
        const triggerCenter = triggerBox.x + triggerBox.width / 2;
        return (
          popupBox.y + popupBox.height <= triggerBox.y &&
          popupBox.x <= triggerCenter &&
          popupBox.x + popupBox.width >= triggerCenter
        );
      })
      .toBe(true);

    await trigger.click();
    await expect(popup).toHaveCount(0);
  }
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
