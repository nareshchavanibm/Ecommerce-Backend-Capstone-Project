import { Component, booleanAttribute, input, output } from '@angular/core';

import { Button } from '../button/button';

@Component({
  selector: 'app-social-button',
  imports: [Button],
  templateUrl: './social-button.html',
  styleUrl: './social-button.css'
})
export class SocialButton {

  readonly provider = input.required<'google' | 'apple'>();

  readonly disabled = input(false, { transform: booleanAttribute });

  readonly pressed = output<void>();
}
