import { Component, booleanAttribute, input, output } from '@angular/core';

import { SocialButton } from '../social-button/social-button';

@Component({
  selector: 'app-social-login',
  imports: [SocialButton],
  templateUrl: './social-login.html',
  styleUrl: './social-login.css'
})
export class SocialLogin {

  readonly disabled = input(false, { transform: booleanAttribute });

  readonly google = output<void>();

  readonly apple = output<void>();
}
