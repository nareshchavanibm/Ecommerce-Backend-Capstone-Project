import { Component, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth';
import { SITE_INFO } from '../../shared/site-info';

@Component({
  selector: 'app-contact',
  imports: [RouterLink],
  templateUrl: './contact.html',
  styleUrl: './contact.css'
})
export class Contact {

  protected readonly info = SITE_INFO;

  protected readonly authService = inject(AuthService);

  constructor() {
    inject(Title).setTitle(`Contact us | ${SITE_INFO.brand}`);
  }
}
