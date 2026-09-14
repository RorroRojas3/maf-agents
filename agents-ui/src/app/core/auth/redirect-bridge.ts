export const redirectPath = '/auth';

export function isRedirectBridge(location: Pick<Location, 'pathname'>): boolean {
  return location.pathname === redirectPath;
}

// Runs before Angular or MSAL start: MSAL must not consume the response the bridge relays.
export async function runRedirectBridge(): Promise<void> {
  try {
    const { broadcastResponseToMainFrame } = await import('@azure/msal-browser/redirect-bridge');
    await broadcastResponseToMainFrame();
  } catch {
    // Nothing to relay (a direct visit or a back-button hit); inside a frame, MSAL times out on its own.
    if (window.top === window.self) {
      location.replace('/');
    }
  }
}
