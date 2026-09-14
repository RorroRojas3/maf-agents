import { AppSettings } from '@shared/models/config/app-settings.model';

export function parseAppSettings(json: unknown): AppSettings {
  const invalid: string[] = [];

  const text = (path: string): string => {
    const value = valueAt(json, path);
    if (typeof value === 'string' && value.trim() !== '') {
      return value.trim();
    }
    invalid.push(path);
    return '';
  };

  const url = (path: string): string => {
    const value = text(path);
    if (value !== '' && !isHttpUrl(value)) {
      invalid.push(path);
    }
    return value;
  };

  const settings: AppSettings = {
    AzureAd: {
      Instance: withTrailingSlash(url('AzureAd.Instance')),
      TenantId: text('AzureAd.TenantId'),
      ClientId: text('AzureAd.ClientId'),
      Audience: text('AzureAd.Audience'),
    },
    Api: {
      BaseUrl: url('Api.BaseUrl').replace(/\/+$/, ''),
    },
  };

  // Keys only: a value may be a placeholder the pipeline failed to replace, and it has no place in a log.
  if (invalid.length > 0) {
    throw new Error(`config.json has missing or invalid settings: ${invalid.join(', ')}`);
  }
  return settings;
}

function valueAt(json: unknown, path: string): unknown {
  return path
    .split('.')
    .reduce<unknown>((node, key) => (isRecord(node) ? node[key] : undefined), json);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isHttpUrl(value: string): boolean {
  return URL.canParse(value) && ['http:', 'https:'].includes(new URL(value).protocol);
}

function withTrailingSlash(value: string): string {
  return value === '' || value.endsWith('/') ? value : `${value}/`;
}
