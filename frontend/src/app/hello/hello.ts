import { Component, inject } from '@angular/core';
import { ApiService } from '../api.service';
import { AsyncPipe } from '@angular/common';

@Component({
  selector: 'app-hello',
  imports: [AsyncPipe],
  templateUrl: './hello.html',
})
export class Hello {
  private readonly apiService = inject(ApiService);

  readonly hello$ = this.apiService.getHello();
}
