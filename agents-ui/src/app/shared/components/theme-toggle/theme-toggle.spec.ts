import { TestBed } from '@angular/core/testing';
import { ThemeStore } from '@state/theme-store';
import { ThemeToggle } from './theme-toggle';

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('offers dark mode from light and switches the store on click', async () => {
    const fixture = TestBed.createComponent(ThemeToggle);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const button = host.querySelector('button') as HTMLButtonElement;

    expect(host.querySelector('i')?.classList.contains('bi-moon-stars')).toBe(true);
    expect(button.getAttribute('aria-label')).toBe('Switch to dark theme');

    button.click();
    await fixture.whenStable();

    expect(TestBed.inject(ThemeStore).mode()).toBe('dark');
    expect(host.querySelector('i')?.classList.contains('bi-sun')).toBe(true);
    expect(button.getAttribute('aria-label')).toBe('Switch to light theme');
  });
});
