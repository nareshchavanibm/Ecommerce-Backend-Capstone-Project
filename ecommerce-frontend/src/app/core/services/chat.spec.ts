import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ChatService } from './chat';

describe('ChatService', () => {

  let service: ChatService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ChatService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the message with history and appends the reply', () => {
    service.send('  Where is my order?  ');

    const request = http.expectOne(req => req.url.endsWith('/api/chat'));

    expect(request.request.body.message).toBe('Where is my order?');
    expect(request.request.body.history).toEqual([]);
    expect(service.busy()).toBe(true);

    request.flush({
      conversationId: 'abc',
      reply: 'Please sign in.',
      agent: 'customer-service',
      products: [],
      order: null,
      escalate: false,
      supportEmail: null
    });

    expect(service.busy()).toBe(false);
    expect(service.messages().map(m => m.role)).toEqual(['user', 'assistant']);

    service.send('Thanks');

    const next = http.expectOne(req => req.url.endsWith('/api/chat'));

    expect(next.request.body.conversationId).toBe('abc');
    expect(next.request.body.agent).toBe('customer-service');
    expect(next.request.body.history.length).toBe(2);

    next.flush({ conversationId: 'abc', reply: 'Welcome', agent: 'customer-service', products: [], order: null, escalate: false, supportEmail: null });
  });

  it('shows a friendly error and keeps failed turns out of the history', () => {
    service.send('hello');
    http.expectOne(req => req.url.endsWith('/api/chat')).flush({}, { status: 429, statusText: 'Too Many Requests' });

    const last = service.messages().at(-1);

    expect(last?.error).toBe(true);
    expect(last?.text).toContain('too quickly');

    service.send('again');

    const retry = http.expectOne(req => req.url.endsWith('/api/chat'));

    expect(retry.request.body.history).toEqual([{ role: 'user', content: 'hello' }]);

    retry.flush({}, { status: 503, statusText: 'Unavailable' });
  });

  it('ignores blank messages', () => {
    service.send('   ');

    http.expectNone(req => req.url.endsWith('/api/chat'));
    expect(service.messages().length).toBe(0);
  });

  it('uses the role config from the server and clears the chat when the role changes', () => {
    service.loadConfig();
    http.expectOne(req => req.url.endsWith('/api/chat/config')).flush({
      role: 'customer', title: 'Shopping assistant', intro: 'i', suggestions: ['Where is my order?']
    });

    service.send('hello');
    http.expectOne(req => req.url.endsWith('/api/chat')).flush({
      conversationId: 'abc', reply: 'Hi', agent: 'customer-service', products: [], order: null, escalate: false, supportEmail: null
    });
    expect(service.messages().length).toBe(2);

    service.loadConfig();
    http.expectOne(req => req.url.endsWith('/api/chat/config')).flush({
      role: 'admin', title: 'Admin assistant', intro: 'a', suggestions: ['Show recent orders']
    });

    expect(service.config().title).toBe('Admin assistant');
    expect(service.messages().length).toBe(0);
  });
});
