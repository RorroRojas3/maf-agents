import { parseAppSettings } from './parse-app-settings';

function validJson() {
  return {
    AzureAd: {
      Instance: 'https://login.microsoftonline.com',
      TenantId: ' tenant ',
      ClientId: 'client',
      Audience: 'client/.default',
    },
    Api: { BaseUrl: 'https://api.test//' },
  };
}

describe('parseAppSettings', () => {
  it('trims values and normalizes the instance and base URL slashes', () => {
    expect(parseAppSettings(validJson())).toEqual({
      AzureAd: {
        Instance: 'https://login.microsoftonline.com/',
        TenantId: 'tenant',
        ClientId: 'client',
        Audience: 'client/.default',
      },
      Api: { BaseUrl: 'https://api.test' },
    });
  });

  it('names every missing, blank or non-string key', () => {
    const json = {
      AzureAd: { Instance: 'https://login.microsoftonline.com/', TenantId: '   ', ClientId: 42 },
      Api: {},
    };

    expect(() => parseAppSettings(json)).toThrow(
      /^config\.json has missing or invalid settings: AzureAd\.TenantId, AzureAd\.ClientId, AzureAd\.Audience, Api\.BaseUrl$/,
    );
  });

  it('rejects URLs that are relative or not http(s)', () => {
    const json = validJson();
    json.AzureAd.Instance = 'login.microsoftonline.com';
    json.Api.BaseUrl = 'ftp://api.test';

    expect(() => parseAppSettings(json)).toThrow(
      /^config\.json has missing or invalid settings: AzureAd\.Instance, Api\.BaseUrl$/,
    );
  });

  it('never echoes the offending value', () => {
    const json = validJson();
    json.Api.BaseUrl = '#{Api.BaseUrl}#';

    expect(() => parseAppSettings(json)).toThrow(
      /^config\.json has missing or invalid settings: Api\.BaseUrl$/,
    );
  });

  it('rejects a body that is not an object', () => {
    expect(() => parseAppSettings(['not', 'settings'])).toThrow(/AzureAd\.Instance/);
  });
});
