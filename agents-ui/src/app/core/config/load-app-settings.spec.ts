import { testAppSettings } from '@testing/app-settings';
import { loadAppSettings } from './load-app-settings';

describe('loadAppSettings', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fetches config.json uncached and returns the parsed settings', async () => {
    const fetch = vi.fn(() => Promise.resolve(Response.json(testAppSettings)));
    vi.stubGlobal('fetch', fetch);

    await expect(loadAppSettings()).resolves.toEqual(testAppSettings);
    expect(fetch).toHaveBeenCalledWith('config.json', { cache: 'no-store' });
  });

  it('rejects when the file is missing', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response(null, { status: 404 })));

    await expect(loadAppSettings()).rejects.toThrow('config.json could not be loaded (HTTP 404)');
  });

  it('rejects a body that is not JSON', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('<!doctype html>')));

    await expect(loadAppSettings()).rejects.toThrow('config.json is not valid JSON');
  });

  it('rejects settings that fail validation', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(Response.json({ Api: {} })));

    await expect(loadAppSettings()).rejects.toThrow(/missing or invalid settings/);
  });
});
