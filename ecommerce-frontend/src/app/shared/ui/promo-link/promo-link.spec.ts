import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { PromoLink } from './promo-link';

@Component({
  imports: [PromoLink],
  template: `<a [appPromoLink]="url()">Go</a>`
})
class Host {
  url = signal('/products?category=Shoes');
}

describe('PromoLink', () => {

  function setup() {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const anchor = fixture.nativeElement.querySelector('a') as HTMLAnchorElement;
    return { fixture, anchor, host: fixture.componentInstance };
  }

  it('routes same-site paths through the router', () => {
    const { anchor } = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const event = new MouseEvent('click', { bubbles: true, cancelable: true });
    anchor.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/products?category=Shoes');
    expect(anchor.hasAttribute('target')).toBe(false);
  });

  it('opens http(s) links in a new tab without opener access', () => {
    const { fixture, anchor, host } = setup();

    host.url.set('https://example.com/sale');
    fixture.detectChanges();

    expect(anchor.getAttribute('href')).toBe('https://example.com/sale');
    expect(anchor.target).toBe('_blank');
    expect(anchor.rel).toBe('noopener noreferrer');
  });

  it('falls back to the product list for unsafe links', () => {
    const { fixture, anchor, host } = setup();

    for (const url of ['javascript:alert(1)', '//evil.example.com']) {
      host.url.set(url);
      fixture.detectChanges();

      expect(anchor.getAttribute('href')).toBe('/products');
    }
  });
});
