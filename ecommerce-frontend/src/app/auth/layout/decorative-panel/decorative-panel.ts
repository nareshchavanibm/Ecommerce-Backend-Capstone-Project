import { Component, input } from '@angular/core';

@Component({
  selector: 'app-decorative-panel',
  templateUrl: './decorative-panel.html',
  styleUrl: './decorative-panel.css'
})
export class DecorativePanel {

  readonly headline = input('WELCOME');

  readonly subheadline = input('');

  readonly message = input('');
}
