import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

export type ToastVariant = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  message: string;
  variant: ToastVariant;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {

  readonly toasts = signal<Toast[]>([]);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private nextId = 0;

  show(message: string, variant: ToastVariant = 'info', durationMs = 4000): void {

    const id = this.nextId++;

    this.toasts.update(list => [...list, { id, message, variant }]);

    if (this.isBrowser && durationMs > 0) {
      setTimeout(() => this.dismiss(id), durationMs);
    }
  }

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error', 6000);
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(toast => toast.id !== id));
  }
}
