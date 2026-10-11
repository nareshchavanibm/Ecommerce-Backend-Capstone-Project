import { Component, booleanAttribute, input } from '@angular/core';

@Component({
  selector: 'button[app-button]',
  templateUrl: './button.html',
  styleUrl: './button.css',
  host: {
    '[attr.data-variant]': 'variant()',
    '[attr.aria-busy]': 'loading() ? "true" : null',
    '[class.btn--full]': 'fullWidth()',
    '[disabled]': 'disabled() || loading()'
  }
})
export class Button {

  readonly variant = input<'primary' | 'outline' | 'ghost'>('primary');

  readonly loading = input(false, { transform: booleanAttribute });

  readonly disabled = input(false, { transform: booleanAttribute });

  readonly fullWidth = input(false, { transform: booleanAttribute });

  readonly arrow = input(false, { transform: booleanAttribute });
}
