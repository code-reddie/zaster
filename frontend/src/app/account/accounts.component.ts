import { Component, inject, OnInit, signal } from '@angular/core';
import { AccountStore } from './account.store';
import { TransactionStore } from '../transaction/transaction.store';

@Component({
  selector: 'app-accounts',
  imports: [],
  templateUrl: './accounts.component.html',
})
export class AccountsComponent implements OnInit {
  readonly accountStore = inject(AccountStore);
  readonly transactionStore = inject(TransactionStore);

  ngOnInit() {
    this.accountStore.loadAccounts();
  }
}
