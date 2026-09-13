import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { ThemeToggle } from '@shared/components/theme-toggle/theme-toggle';

@Component({
  imports: [RouterLink, RouterOutlet, ThemeToggle],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {}
