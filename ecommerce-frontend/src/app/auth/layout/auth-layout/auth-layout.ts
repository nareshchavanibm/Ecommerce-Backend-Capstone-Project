import { Component, input } from '@angular/core';

import { DecorativePanel } from '../decorative-panel/decorative-panel';

@Component({
  selector: 'app-auth-layout',
  imports: [DecorativePanel],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.css'
})
export class AuthLayout {

  readonly headline = input('WELCOME');

  readonly subheadline = input('TO YOUR STORE NAME');

  readonly message = input('Discover products you love, track every order, and check out securely.');
}
