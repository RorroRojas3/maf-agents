// Property names mirror public/config.json, which the deployment pipeline rewrites by key.
export interface AppSettings {
  readonly AzureAd: AzureAdSettings;
  readonly Api: ApiSettings;
}

export interface AzureAdSettings {
  readonly Instance: string;
  readonly TenantId: string;
  readonly ClientId: string;
  readonly Audience: string;
}

export interface ApiSettings {
  readonly BaseUrl: string;
}
