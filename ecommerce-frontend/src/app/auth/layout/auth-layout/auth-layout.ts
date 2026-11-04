import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DecorativePanel } from '../decorative-panel/decorative-panel';

@Component({
  selector: 'app-auth-layout',
  imports: [RouterLink, DecorativePanel],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.css'
})
export class AuthLayout {

  readonly heading = input.required<string>();

  readonly subheading = input('');

  readonly headline = input('WELCOME');

  readonly subheadline = input('TO YOUR STORE NAME');

  readonly message = input('Discover products you love, track every order, and check out securely.');
}
