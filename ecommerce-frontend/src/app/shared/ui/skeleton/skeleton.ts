import { Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  templateUrl: './skeleton.html',
  styleUrl: './skeleton.css',
  host: {
    'aria-hidden': 'true',
    '[attr.data-shape]': 'shape()',
    '[style.width]': 'width()',
    '[style.height]': 'height()'
  }
})
export class Skeleton {

  readonly shape = input<'text' | 'block' | 'circle'>('text');

  readonly width = input('100%');

  readonly height = input('');
}
