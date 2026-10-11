import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface ChatProduct {
  id: number;
  name: string;
  price: number;
  imageUrl: string | null;
  category: string;
}

export interface ChatOrder {
  orderNumber: string;
  status: string;
  totalAmount: number;
  orderDate: string;
  items: string[];
}

export interface ChatMessage {
  id: number;
  role: 'user' | 'assistant';
  text: string;
  error?: boolean;
  products?: ChatProduct[];
  order?: ChatOrder | null;
  escalate?: boolean;
  supportEmail?: string | null;
  rated?: 'up' | 'down';
}

interface ChatResponse {
  conversationId: string;
  reply: string;
  agent: string;
  products: ChatProduct[];
  order: ChatOrder | null;
  escalate: boolean;
  supportEmail: string | null;
}

export const MAX_CHAT_LENGTH = 1000;

export interface ChatConfig {
  role: 'anonymous' | 'customer' | 'admin';
  title: string;
  intro: string;
  suggestions: string[];
}

const DEFAULT_CONFIG: ChatConfig = {
  role: 'anonymous',
  title: 'Shopping assistant',
  intro: 'Ask about an order, how the store works, or what to buy. Never share card numbers or passwords here.',
  suggestions: ['Where is my order?', 'How do I cancel an order?', 'Recommend a gift under 1000']
};

const HISTORY_TURNS = 10;

@Injectable({ providedIn: 'root' })
export class ChatService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/api/chat`;

  private nextId = 1;

  private conversationId: string | null = null;

  private agent: string | null = null;

  readonly messages = signal<ChatMessage[]>([]);

  readonly busy = signal(false);

  readonly config = signal<ChatConfig>(DEFAULT_CONFIG);

  private lastToken: string | null | undefined;

  // Title, intro and suggestions depend on who is signed in; the server decides the role from the token.
  loadConfig(): void {
    const token = typeof localStorage === 'undefined' ? null : localStorage.getItem('token');

    // A different sign-in must never see the previous conversation.
    if (this.lastToken !== undefined && token !== this.lastToken) {
      this.reset();
    }

    this.lastToken = token;

    this.http.get<ChatConfig>(`${this.apiUrl}/config`).subscribe({
      next: config => {
        if (config.role !== this.config().role) {
          this.reset();
        }

        this.config.set(config);
      },
      error: () => undefined
    });
  }

  send(text: string): void {
    const message = text.trim();

    if (!message || this.busy()) {
      return;
    }

    // Failed turns are not part of the conversation the model should see.
    const history = this.messages()
      .filter(m => !m.error)
      .slice(-HISTORY_TURNS)
      .map(m => ({ role: m.role, content: m.text }));

    this.append({ role: 'user', text: message });
    this.busy.set(true);

    this.http.post<ChatResponse>(this.apiUrl, {
      message,
      conversationId: this.conversationId,
      history,
      agent: this.agent
    }).subscribe({
      next: response => {
        this.conversationId = response.conversationId;
        this.agent = response.agent === 'none' ? this.agent : response.agent;

        this.append({
          role: 'assistant',
          text: response.reply,
          products: response.products,
          order: response.order,
          escalate: response.escalate,
          supportEmail: response.supportEmail
        });

        this.busy.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.append({ role: 'assistant', text: this.errorText(error), error: true });
        this.busy.set(false);
      }
    });
  }

  rate(message: ChatMessage, rating: 'up' | 'down'): void {
    if (!this.conversationId || message.rated) {
      return;
    }

    this.messages.update(list => list.map(m => m.id === message.id ? { ...m, rated: rating } : m));

    this.http.post(`${this.apiUrl}/feedback`, {
      conversationId: this.conversationId,
      rating,
      agent: this.agent
    }).subscribe({ error: () => undefined });
  }

  reset(): void {
    this.messages.set([]);
    this.conversationId = null;
    this.agent = null;
  }

  private append(message: Omit<ChatMessage, 'id'>): void {
    this.messages.update(list => [...list, { ...message, id: this.nextId++ }]);
  }

  private errorText(error: HttpErrorResponse): string {
    if (error.status === 429) {
      return 'You are sending messages too quickly. Please wait a moment and try again.';
    }

    if (error.status === 503) {
      return 'The assistant is unavailable right now. Please try again shortly.';
    }

    return 'Something went wrong. Please try again.';
  }
}
