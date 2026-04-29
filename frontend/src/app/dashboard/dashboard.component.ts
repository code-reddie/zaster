import { Component, inject } from '@angular/core';
import { AuthService } from '../authentication/auth.service';
import { Router } from '@angular/router';
import { Dialog } from '@angular/cdk/dialog';
import { CreateAccountDialog } from '../account/create-account-dialog/create-account-dialog.component';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  openDialog() {
    this.dialog.open(CreateAccountDialog, {
      minWidth: '500px',
      backdropClass: ['bg-black/60', 'backdrop-blur-sm'],
      panelClass: 'flex',
    });
  }
}
