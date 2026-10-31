import { describe, expect, it, vi } from 'vitest';
import { watchObsModePrompt } from './obsModePrompt';

function sources(options: { running?: boolean; inGame?: boolean } = {}) {
  const listeners = new Set<() => void>();
  let running = options.running ?? false;
  let inGame = options.inGame ?? true;
  return {
    sources: {
      onGameReady: (listener: () => void) => {
        listeners.add(listener);
        return () => listeners.delete(listener);
      },
      obsRunning: () => Promise.resolve(running),
      inGameViewersEnabled: () => inGame,
    },
    connect: async () => {
      listeners.forEach((listener) => listener());
      await Promise.resolve();
    },
    setRunning: (value: boolean) => (running = value),
    setInGame: (value: boolean) => (inGame = value),
  };
}

describe('OBS mode prompt', () => {
  it('asks once when the game connects while OBS mode is off', async () => {
    const game = sources();
    const ask = vi.fn();
    watchObsModePrompt(game.sources, ask);
    await game.connect();
    await game.connect();
    expect(ask).toHaveBeenCalledTimes(1);
  });

  it('stays quiet while OBS mode is on, and asks if it is off later', async () => {
    const game = sources({ running: true });
    const ask = vi.fn();
    watchObsModePrompt(game.sources, ask);
    await game.connect();
    expect(ask).not.toHaveBeenCalled();
    game.setRunning(false);
    await game.connect();
    expect(ask).toHaveBeenCalledTimes(1);
  });

  it('does not ask a player who turned in-game viewers off', async () => {
    const game = sources({ inGame: false });
    const ask = vi.fn();
    watchObsModePrompt(game.sources, ask);
    await game.connect();
    expect(ask).not.toHaveBeenCalled();
  });

  it('drops a pending check after it stops', async () => {
    const game = sources();
    const ask = vi.fn();
    const stop = watchObsModePrompt(game.sources, ask);
    const connected = game.connect();
    stop();
    await connected;
    expect(ask).not.toHaveBeenCalled();
  });
});
