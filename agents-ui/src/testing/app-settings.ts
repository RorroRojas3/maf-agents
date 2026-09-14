import { AppSettings } from '@shared/models/config/app-settings.model';

export const testAppSettings: AppSettings = {
  AzureAd: {
    Instance: 'https://login.microsoftonline.com/',
    TenantId: '11111111-1111-4111-8111-111111111111',
    ClientId: '22222222-2222-4222-8222-222222222222',
    Audience: '22222222-2222-4222-8222-222222222222/.default',
  },
  Api: { BaseUrl: 'https://api.test' },
};
