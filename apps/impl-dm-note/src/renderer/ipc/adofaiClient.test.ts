import { describe, expect, it, vi } from 'vitest';
import { IpcVersionMismatchError, tryConnect } from '@adofai-ipc/client';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('ADOFAI-IPC client integration', () => {
  it('discovers a port, waits for the namespace, and sends the sync call', async () => {
    const requests: Array<{ url: string; init?: RequestInit }> = [];
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        requests.push({ url, init });

        if (url.includes(':32145/')) throw new Error('connection refused');
        if (url.endsWith('/ipc/health')) {
          return jsonResponse({
            ok: true,
            server: 'AdofaiIpc',
            serverVersion: '0.4.1',
            protocolVersion: 1,
            port: 32146,
          });
        }
        if (url.includes('/ipc/namespaces/impl-resourcepack')) {
          return jsonResponse({
            namespace: 'impl-resourcepack',
            displayName: 'ImplResourcePack',
            version: '0.1.0',
            status: 'ready',
            methods: ['key-limiter.sync'],
          });
        }
        return jsonResponse({ ok: true, result: { applied: true } });
      },
    );

    const client = await tryConnect({ fetch: fetchMock as typeof fetch });
    await client.waitForNamespace('impl-resourcepack', { status: 'ready' });
    await client.call({
      namespace: 'impl-resourcepack',
      method: 'key-limiter.sync',
      params: { revision: 1 },
    });

    const post = requests.find((request) => request.init?.method === 'POST');
    expect(post?.url).toBe('http://127.0.0.1:32146/ipc');
    expect(JSON.parse(String(post?.init?.body))).toMatchObject({
      namespace: 'impl-resourcepack',
      method: 'key-limiter.sync',
      params: { revision: 1 },
    });
  });

  it('stops discovery when the server product version differs', async () => {
    const fetchMock = vi.fn(async () =>
      jsonResponse({
        ok: true,
        server: 'AdofaiIpc',
        serverVersion: '0.2.0',
        protocolVersion: 1,
        port: 32145,
      }),
    );

    await expect(
      tryConnect({ fetch: fetchMock as typeof fetch }),
    ).rejects.toBeInstanceOf(IpcVersionMismatchError);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
