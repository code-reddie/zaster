import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';
import { Shell } from './core/layout/shell';
import { accountGuard, startRedirect } from './features/accounts/account.guards';

export const routes: Routes = [
  {
    path: 'anmelden',
    canActivate: [guestGuard],
    title: 'Anmelden · Zaster',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'registrieren',
    canActivate: [guestGuard],
    title: 'Registrieren · Zaster',
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [startRedirect], children: [] },
      {
        path: 'konten/neu',
        title: 'Konto hinzufügen · Zaster',
        loadComponent: () =>
          import('./features/accounts/create-account.page').then((m) => m.CreateAccountPage),
      },
      {
        path: 'konten/:accountId',
        canActivate: [accountGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'buchungen' },
          {
            path: 'buchungen',
            title: 'Buchungen · Zaster',
            loadComponent: () =>
              import('./features/transactions/transactions.page').then((m) => m.TransactionsPage),
          },
          {
            path: 'offen',
            title: 'Ohne Kategorie · Zaster',
            loadComponent: () =>
              import('./features/transactions/open.page').then((m) => m.OpenPage),
          },
          {
            path: 'verteilung',
            title: 'Verteilung · Zaster',
            loadComponent: () => import('./features/flows/flows.page').then((m) => m.FlowsPage),
          },
          {
            path: 'kategorien',
            title: 'Kategorien · Zaster',
            loadComponent: () =>
              import('./features/categories/categories.page').then((m) => m.CategoriesPage),
          },
          {
            path: 'konto',
            title: 'Konto · Zaster',
            loadComponent: () =>
              import('./features/accounts/account.page').then((m) => m.AccountPage),
          },
        ],
      },
      {
        path: 'einstellungen',
        title: 'Einstellungen · Zaster',
        loadComponent: () =>
          import('./features/settings/settings.page').then((m) => m.SettingsPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
