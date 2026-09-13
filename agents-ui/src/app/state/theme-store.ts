import { DOCUMENT, inject } from '@angular/core';
import {
  PartialStateUpdater,
  patchState,
  signalStore,
  watchState,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';

export type ThemeMode = 'light' | 'dark';

interface ThemeState {
  mode: ThemeMode;
}

const storageKey = 'andes.theme';

export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withState<ThemeState>(() => ({ mode: initialMode(inject(DOCUMENT)) })),
  withMethods((store, document = inject(DOCUMENT)) => ({
    setMode(mode: ThemeMode): void {
      patchState(store, setThemeMode(mode));
      writeStoredMode(document, mode);
    },
    toggle(): void {
      patchState(store, toggleThemeMode());
      writeStoredMode(document, store.mode());
    },
  })),
  withHooks({
    onInit(store) {
      const document = inject(DOCUMENT);
      watchState(store, ({ mode }) => document.documentElement.setAttribute('data-bs-theme', mode));
    },
  }),
);

function setThemeMode(mode: ThemeMode): PartialStateUpdater<ThemeState> {
  return () => ({ mode });
}

function toggleThemeMode(): PartialStateUpdater<ThemeState> {
  return ({ mode }) => ({ mode: mode === 'dark' ? 'light' : 'dark' });
}

// Persisted only on an explicit choice, so a visitor who never toggles keeps following the OS.
function initialMode(document: Document): ThemeMode {
  const stored = readStoredMode(document);
  if (stored) {
    return stored;
  }
  return document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)')?.matches
    ? 'dark'
    : 'light';
}

function readStoredMode(document: Document): ThemeMode | null {
  try {
    const value = document.defaultView?.localStorage.getItem(storageKey);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}

function writeStoredMode(document: Document, mode: ThemeMode): void {
  try {
    document.defaultView?.localStorage.setItem(storageKey, mode);
  } catch {
    // Storage can be blocked (private browsing); the theme still applies for this visit.
  }
}
