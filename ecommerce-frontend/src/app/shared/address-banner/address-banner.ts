import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';

import { AuthService } from '../../auth/auth';
import { AccountService } from '../../core/services/account';

@Component({
  selector: 'app-address-banner',
  imports: [RouterLink],
  templateUrl: './address-banner.html',
  styleUrl: './address-banner.css'
})
export class AddressBanner {

  private readonly authService = inject(AuthService);

  private readonly accountService = inject(AccountService);

  private readonly router = inject(Router);

  private readonly dismissed = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  private readonly isCustomer = computed(
    () => this.authService.isLoggedIn() && this.authService.getRole() !== 'Admin'
  );

  protected readonly visible = computed(() =>
    this.isCustomer()
    && this.accountService.addressCount() === 0
    && !this.dismissed()
    && this.url().split(/[?#]/)[0] !== '/profile'
  );

  constructor() {

    effect(() => {

      if (!this.isCustomer()) {
        // Signed out (or an admin): forget the previous user's state.
        untracked(() => {
          this.accountService.addressCount.set(null);
          this.dismissed.set(false);
        });
        return;
      }

      if (untracked(() => this.accountService.addressCount()) === null) {
        this.accountService.getAddresses().subscribe({ error: () => { } });
      }
    });
  }

  protected dismiss(): void {
    this.dismissed.set(true);
  }
}
