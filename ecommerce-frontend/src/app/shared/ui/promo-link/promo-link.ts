import { Directive, inject, input } from '@angular/core';
import { Router } from '@angular/router';

// Links a promotion to its target: same-site paths use the router, http(s) links open in a new tab.
@Directive({
  selector: 'a[appPromoLink]',
  host: {
    draggable: 'false',
    '[attr.href]': 'href()',
    '[attr.target]': 'external() ? "_blank" : null',
    '[attr.rel]': 'external() ? "noopener noreferrer" : null',
    '(click)': 'onClick($event)'
  }
})
export class PromoLink {

  private readonly router = inject(Router);

  readonly appPromoLink = input.required<string>();

  protected href(): string {
    return this.external() || this.internal() ? this.appPromoLink().trim() : '/products';
  }

  protected external(): boolean {
    return /^https?:\/\//i.test(this.appPromoLink().trim());
  }

  protected onClick(event: MouseEvent): void {

    if (this.external() || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    this.router.navigateByUrl(this.href());
  }

  private internal(): boolean {
    const url = this.appPromoLink().trim();

    return url.startsWith('/') && !url.startsWith('//');
  }
}
