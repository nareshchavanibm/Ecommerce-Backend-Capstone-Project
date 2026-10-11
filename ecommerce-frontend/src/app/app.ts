import { Component, computed, inject } from '@angular/core';
import { Location } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { Navbar } from './shared/navbar/navbar';
import { AddressBanner } from './shared/address-banner/address-banner';
import { Footer } from './shared/footer/footer';
import { ChatWidget } from './shared/chat-widget/chat-widget';
import { ToastOutlet } from './shared/ui/toast/toast-outlet';

const AUTH_PATHS = ['/login', '/register', '/forgot-password', '/verify-otp', '/reset-password'];

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    Navbar,
    AddressBanner,
    Footer,
    ChatWidget,
    ToastOutlet
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {

  private readonly router = inject(Router);

  private readonly location = inject(Location);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects)
    ),
    { initialValue: this.location.path() || this.router.url }
  );

  // Auth pages use their own full-screen layout.
  protected readonly isAuthRoute = computed(() => AUTH_PATHS.includes(this.url().split('?')[0]));

  protected readonly isHomeRoute = computed(() => this.url().split(/[?#]/)[0] === '/home');
}
