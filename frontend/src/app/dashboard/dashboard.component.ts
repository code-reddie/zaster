import { Component, inject } from '@angular/core';
import { AuthService } from '../authentication/auth.service';
import { Router } from '@angular/router';
import { Dialog } from '@angular/cdk/dialog';
import { CreateAccountDialog } from '../account/create-account-dialog/create-account-dialog.component';
import { AccountsComponent } from '../account/accounts.component';
import { CreateTransactionDialog } from '../transaction/create-transaction-dialog/create-transaction-dialog.component';
import { TransactionsComponent } from '../transaction/transactions.component';
import { CsvImportDialog } from '../csv-import/csv-import-dialog/csv-import-dialog.component';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  imports: [AccountsComponent, TransactionsComponent],
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  openAddAccountDialog() {
    this.dialog.open(CreateAccountDialog, {
      backdropClass: ['bg-black/60', 'backdrop-blur-sm'],
    });
  }

  openAddTransactionDialog() {
    this.dialog.open(CreateTransactionDialog, {
      backdropClass: ['bg-black/60', 'backdrop-blur-sm'],
    });
  }

  openImportCsvDialog() {
    this.dialog.open(CsvImportDialog, {
      backdropClass: ['bg-black/60', 'backdrop-blur-sm'],
    });
  }
}
