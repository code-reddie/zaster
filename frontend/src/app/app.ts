import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastOutlet } from './core/ui/toast';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastOutlet],
  template: `
    <router-outlet />
    <z-toast-outlet />
  `,
})
export class App {}
