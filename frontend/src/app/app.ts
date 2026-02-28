import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Hello } from './hello/hello';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Hello],
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('zaster');
}
