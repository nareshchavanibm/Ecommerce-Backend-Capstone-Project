import { DatePipe } from '@angular/common';
import { Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AppNotification, NotificationService } from '../../core/services/notifications';

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  templateUrl: './notification-bell.html',
  styleUrl: './notification-bell.css'
})
export class NotificationBell {

  protected readonly notifications = inject(NotificationService);

  private readonly router = inject(Router);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update(value => !value);
  }

  protected openItem(item: AppNotification): void {
    this.notifications.markRead(item.id);
    this.open.set(false);
    this.router.navigateByUrl(item.link);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.open.set(false);
  }
}
