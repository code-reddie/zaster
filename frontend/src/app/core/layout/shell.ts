import { Component, computed, DestroyRef, effect, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountStore } from '../../features/accounts/account.store';
import { CategoryStore } from '../../features/categories/category.store';
import { AuthService } from '../auth/auth.service';
import { DialogService } from '../ui/dialog';
import { Icon, IconName } from '../ui/icon';
import { AccountSwitcherSheet } from './account-switcher-sheet';
import { Logo } from './logo';
import { openLabel } from './open-label';

interface NavItem {
  path: string;
  label: string;
  short: string;
  icon: IconName;
  badge?: number;
}

/**
 * Rahmen für alle Seiten nach der Anmeldung. Ab 1024px Breite (PC, iPad quer) Seitenleiste links,
 * darunter Kontowahl oben und Navigationsleiste unten.
 */
@Component({
  selector: 'z-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, Logo],
  templateUrl: './shell.html',
})
export class Shell {
  protected readonly accounts = inject(AccountStore);
  protected readonly auth = inject(AuthService);
  private readonly dialog = inject(DialogService);
  private readonly categories = inject(CategoryStore);
  protected readonly openLabel = openLabel;

  constructor() {
    // Auch Seiten ohne Konto in der Adresse (Einstellungen) zeigen die Kontenliste.
    void this.accounts.ensureLoaded();

    // Zurück in der App (anderer Tab, iPhone aus dem Hintergrund): Konten neu laden, damit die Zahl
    // der offenen Buchungen auch nach einem automatischen Abruf stimmt.
    const onVisible = () => {
      if (document.visibilityState === 'visible' && this.accounts.loaded()) {
        void this.accounts.load();
      }
    };
    document.addEventListener('visibilitychange', onVisible);
    inject(DestroyRef).onDestroy(() => document.removeEventListener('visibilitychange', onVisible));

    // Kategorien und Regeln gehören zum Konto und werden beim Kontowechsel neu geladen.
    effect(() => {
      const accountId = this.accounts.selectedId();
      if (accountId !== null) {
        void this.categories.use(accountId);
      }
    });
  }

  protected readonly nav = computed<NavItem[]>(() => {
    const account = this.accounts.selected();
    if (!account) {
      return [];
    }
    const base = `/konten/${account.id}`;
    return [
      { path: `${base}/buchungen`, label: 'Buchungen', short: 'Buchungen', icon: 'list' },
      {
        path: `${base}/offen`,
        label: 'Ohne Kategorie',
        short: 'Offen',
        icon: 'inbox',
        badge: account.openTransactions,
      },
      { path: `${base}/verteilung`, label: 'Verteilung', short: 'Verteilung', icon: 'flow' },
      {
        path: `${base}/kategorien`,
        label: 'Kategorien & Regeln',
        short: 'Kategorien',
        icon: 'tag',
      },
      { path: `${base}/konto`, label: 'Konto & Abruf', short: 'Konto', icon: 'bank' },
    ];
  });

  protected openSwitcher() {
    this.dialog.open(AccountSwitcherSheet);
  }
}
