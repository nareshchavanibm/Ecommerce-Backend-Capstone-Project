import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../auth/auth';
import { ToastService } from '../../shared/ui/toast/toast.service';

export interface AppNotification {
  id: number;
  title: string;
  message: string;
  link: string;
  time: Date;
  read: boolean;
}

// Shape stored by and pushed from the order service.
interface NotificationDto {
  id: number;
  title: string;
  message: string;
  link: string;
  createdAt: string;
  read: boolean;
}

const MAX_ITEMS = 20;
const RETRY_MS = 15000;

@Injectable({
  providedIn: 'root'
})
export class NotificationService {

  private readonly auth = inject(AuthService);

  private readonly http = inject(HttpClient);

  private readonly toast = inject(ToastService);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly items = signal<AppNotification[]>([]);

  readonly unread = computed(() => this.items().filter(item => !item.read).length);

  // Emits whenever an order changes so open pages can refresh themselves.
  readonly ordersChanged = new Subject<void>();

  private connection?: HubConnection;

  private retryTimer?: ReturnType<typeof setTimeout>;

  private readonly apiUrl = `${environment.apiUrl}/api/notifications`;

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.start();
      } else {
        void this.stop();
      }
    });
  }

  markRead(id: number): void {
    this.items.update(list => list.map(item => item.id === id ? { ...item, read: true } : item));
    this.http.put(`${this.apiUrl}/${id}/read`, null).subscribe({ error: () => undefined });
  }

  markAllRead(): void {
    this.items.update(list => list.map(item => ({ ...item, read: true })));
    this.http.put(`${this.apiUrl}/read-all`, null).subscribe({ error: () => undefined });
  }

  clear(): void {
    this.items.set([]);
    this.http.delete(this.apiUrl).subscribe({ error: () => undefined });
  }

  private async start(): Promise<void> {

    if (!this.isBrowser || this.connection) {
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/notifications`, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
        // The gateway's CORS policy does not allow credentials.
        withCredentials: false
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection = connection;

    connection.on('Notification', (dto: NotificationDto) => this.push(dto));

    // Anything sent while the connection was down is picked up from the stored list.
    connection.onreconnected(() => this.load());

    try {
      await connection.start();
      this.load();
    } catch {
      // The order service may be down or restarting; try again while still signed in.
      this.connection = undefined;
      this.retryTimer = setTimeout(() => {
        if (this.auth.isLoggedIn()) {
          void this.start();
        }
      }, RETRY_MS);
    }
  }

  private async stop(): Promise<void> {

    clearTimeout(this.retryTimer);

    const connection = this.connection;

    this.connection = undefined;
    this.items.set([]);

    if (connection) {
      await connection.stop();
    }
  }

  private load(): void {

    this.http.get<NotificationDto[]>(this.apiUrl).subscribe({
      next: stored => {
        const known = new Set(this.items().map(item => item.id));
        const missed = stored.filter(item => !known.has(item.id));

        this.items.update(list => this.merge(list, stored));

        const unread = missed.filter(item => !item.read).length;

        if (unread > 0) {
          this.toast.show(`You have ${unread} unread ${unread === 1 ? 'notification' : 'notifications'}.`, 'info');
          this.ordersChanged.next();
        }
      },
      error: () => undefined
    });
  }

  private push(dto: NotificationDto): void {

    if (this.items().some(item => item.id === dto.id)) {
      return;
    }

    this.items.update(list => this.merge(list, [dto]));

    this.toast.show(`${dto.title}: ${dto.message}`, 'info');
    this.ordersChanged.next();
  }

  // Newest first, one entry per id, capped.
  private merge(list: AppNotification[], incoming: NotificationDto[]): AppNotification[] {

    const byId = new Map(list.map(item => [item.id, item]));

    for (const dto of incoming) {
      byId.set(dto.id, byId.get(dto.id) ?? {
        id: dto.id,
        title: dto.title,
        message: dto.message,
        link: dto.link,
        time: new Date(dto.createdAt),
        read: dto.read
      });
    }

    return [...byId.values()]
      .sort((a, b) => b.time.getTime() - a.time.getTime())
      .slice(0, MAX_ITEMS);
  }
}
