import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import CountDisplay from './CountDisplay';

describe('counter paint compatibility', () => {
  it('keeps the v1 CSS variables and stroke without adding a gradient layer', () => {
    const html = renderToStaticMarkup(
      <CountDisplay
        count={7}
        fillColor="#ffffff"
        strokeColor="#000000"
        fontWeight={700}
      />,
    );
    expect(html).toContain('class="counter"');
    expect(html).toContain('data-text="7"');
    expect(html).toContain('--counter-stroke-width-default:1px');
    expect(html).toContain('font-weight:700');
    expect(html).not.toContain('background-image:');
  });

  it('renders v2 gradient fills while retaining the configured v1 outline', () => {
    const html = renderToStaticMarkup(
      <CountDisplay
        count={7}
        strokeColor="#000000"
        fillGradient={{
          angle: 90,
          stops: [
            { color: '#ff000080', pos: 0 },
            { color: '#0000ff80', pos: 1 },
          ],
        }}
      />,
    );
    expect(html).toContain('background-image:linear-gradient(90deg');
    expect(html).toContain('background-clip:text');
    expect(html).toContain('--counter-color-default:transparent');
    expect(html).toContain('--counter-stroke-width-default:1px');
  });

  it('uses the v2 independent bold flag only when supplied', () => {
    const html = renderToStaticMarkup(
      <CountDisplay count={1} fontWeight={500} fontBold />,
    );
    expect(html).toContain('font-weight:800');
  });
});
