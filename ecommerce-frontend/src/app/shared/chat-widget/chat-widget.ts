import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ChatMessage, ChatService, MAX_CHAT_LENGTH } from '../../core/services/chat';

@Component({
  selector: 'app-chat-widget',
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink],
  templateUrl: './chat-widget.html',
  styleUrl: './chat-widget.css'
})
export class ChatWidget {

  protected readonly chat = inject(ChatService);

  protected readonly maxLength = MAX_CHAT_LENGTH;

  protected readonly open = signal(false);

  protected readonly draft = signal('');

  protected readonly touched = signal(false);

  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('input');

  private readonly log = viewChild<ElementRef<HTMLElement>>('log');

  private readonly launcher = viewChild<ElementRef<HTMLButtonElement>>('launcher');

  constructor() {
    effect(() => {
      this.chat.messages();
      this.chat.busy();

      queueMicrotask(() => {
        const log = this.log()?.nativeElement;

        if (log) {
          log.scrollTop = log.scrollHeight;
        }
      });
    });
  }

  protected error(): string {
    if (!this.touched()) {
      return '';
    }

    const length = this.draft().trim().length;

    if (length === 0) {
      return 'Type a message to send.';
    }

    return length > this.maxLength ? `Keep messages under ${this.maxLength} characters.` : '';
  }

  protected toggle(): void {
    if (this.open()) {
      this.close();
      return;
    }

    this.open.set(true);
    this.chat.loadConfig();
    queueMicrotask(() => this.input()?.nativeElement.focus());
  }

  protected close(): void {
    this.open.set(false);
    queueMicrotask(() => this.launcher()?.nativeElement.focus());
  }

  protected submit(): void {
    this.touched.set(true);

    if (this.error() || this.chat.busy()) {
      return;
    }

    this.chat.send(this.draft());
    this.draft.set('');
    this.touched.set(false);
  }

  protected ask(text: string): void {
    this.chat.send(text);
  }

  protected onEnter(event: Event): void {
    // Shift+Enter keeps the newline.
    if (!(event as KeyboardEvent).shiftKey) {
      event.preventDefault();
      this.submit();
    }
  }

  protected newChat(): void {
    this.chat.reset();
    this.draft.set('');
    this.touched.set(false);
    this.input()?.nativeElement.focus();
  }

  protected rate(message: ChatMessage, rating: 'up' | 'down'): void {
    this.chat.rate(message, rating);
  }

  protected isLastAssistant(message: ChatMessage): boolean {
    const list = this.chat.messages();

    return message.role === 'assistant' && !message.error && list[list.length - 1] === message;
  }
}
