import { TestBed } from '@angular/core/testing';
import { ThemeStore } from './theme-store';

describe('ThemeStore', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-bs-theme');
  });

  it('defaults to light and applies it to the document when nothing is stored', () => {
    const store = TestBed.inject(ThemeStore);

    expect(store.mode()).toBe('light');
    expect(document.documentElement.getAttribute('data-bs-theme')).toBe('light');
    expect(localStorage.getItem('andes.theme')).toBeNull();
  });

  it('starts from the stored mode', () => {
    localStorage.setItem('andes.theme', 'dark');

    expect(TestBed.inject(ThemeStore).mode()).toBe('dark');
  });

  it('ignores an unrecognized stored value', () => {
    localStorage.setItem('andes.theme', 'sepia');

    expect(TestBed.inject(ThemeStore).mode()).toBe('light');
  });

  it('toggles the mode, applies it to the document and remembers it', () => {
    const store = TestBed.inject(ThemeStore);

    store.toggle();

    expect(store.mode()).toBe('dark');
    expect(document.documentElement.getAttribute('data-bs-theme')).toBe('dark');
    expect(localStorage.getItem('andes.theme')).toBe('dark');
  });

  it('sets an explicit mode', () => {
    localStorage.setItem('andes.theme', 'dark');
    const store = TestBed.inject(ThemeStore);

    store.setMode('light');

    expect(store.mode()).toBe('light');
    expect(localStorage.getItem('andes.theme')).toBe('light');
  });
});
