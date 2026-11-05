import { describe, expect, it, vi } from 'vitest';
import { makeSpritePosition } from '@utils/sprite/spriteFixtures';
import { updateSpriteCollection } from './useSpriteEditor';

vi.mock('@components/main/Modal/content/editors/SpriteEditorModal', () => ({
  default: () => null,
}));

describe('sprite editor persistence', () => {
  it('replaces only the selected stable ID and retains other tabs and siblings', () => {
    const first = makeSpritePosition({ id: 'first', groupId: 'group-a' });
    const second = makeSpritePosition({ id: 'second' });
    const foot = [makeSpritePosition({ id: 'foot' })];
    const current = { hand: [first, second], foot };
    const result = updateSpriteCollection(current, 'hand', 'first', {
      ...first,
      id: 'changed-by-modal',
      dx: 42,
    });
    expect(result.hand[0]).toMatchObject({
      id: 'first',
      dx: 42,
      groupId: 'group-a',
    });
    expect(result.hand[1]).toBe(second);
    expect(result.foot).toBe(foot);
    expect(current.hand[0].dx).toBe(0);
  });

  it('adds and deletes independently without emptying another viewer', () => {
    const sprite = makeSpritePosition({ id: 'new' });
    const current = { foot: [makeSpritePosition({ id: 'foot' })] };
    const added = updateSpriteCollection(current, 'hand', undefined, sprite);
    expect(added.hand).toEqual([sprite]);
    const deleted = updateSpriteCollection(added, 'hand', 'new', null);
    expect(deleted.hand).toEqual([]);
    expect(deleted.foot).toBe(current.foot);
  });

  it('does not revive a sprite removed while its editor was open', () => {
    expect(() =>
      updateSpriteCollection(
        { hand: [] },
        'hand',
        'removed',
        makeSpritePosition(),
      ),
    ).toThrow('removed');
  });
});
