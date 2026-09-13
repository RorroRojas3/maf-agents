import { Component, computed, inject } from '@angular/core';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { ThemeStore } from '@state/theme-store';

@Component({
  imports: [NgbTooltip],
  selector: 'app-theme-toggle',
  templateUrl: './theme-toggle.html',
})
export class ThemeToggle {
  protected readonly store = inject(ThemeStore);
  protected readonly label = computed(() =>
    this.store.mode() === 'dark' ? 'Switch to light theme' : 'Switch to dark theme',
  );
}
