import { AppSettings } from '@shared/models/config/app-settings.model';
import { parseAppSettings } from '@shared/utils/config/parse-app-settings';

export const appSettingsFile = 'config.json';

export async function loadAppSettings(): Promise<AppSettings> {
  // no-store: the pipeline rewrites this file per environment, so a cached copy would pin stale values.
  const response = await fetch(appSettingsFile, { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`${appSettingsFile} could not be loaded (HTTP ${response.status})`);
  }

  let json: unknown;
  try {
    json = await response.json();
  } catch {
    throw new Error(`${appSettingsFile} is not valid JSON`);
  }
  return parseAppSettings(json);
}
