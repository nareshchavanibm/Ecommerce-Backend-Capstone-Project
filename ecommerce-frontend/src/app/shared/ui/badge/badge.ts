import { Component, input } from '@angular/core';

@Component({
  selector: 'app-badge',
  templateUrl: './badge.html',
  styleUrl: './badge.css',
  host: {
    '[attr.data-variant]': 'variant()'
  }
})
export class Badge {

  readonly variant = input<'neutral' | 'success' | 'warning' | 'danger' | 'sale'>('neutral');
}
