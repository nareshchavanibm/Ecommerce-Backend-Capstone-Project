import { Component, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { SITE_INFO } from '../../shared/site-info';

@Component({
  selector: 'app-about',
  imports: [RouterLink],
  templateUrl: './about.html',
  styleUrl: './about.css'
})
export class About {

  protected readonly brand = SITE_INFO.brand;

  constructor() {
    inject(Title).setTitle(`About us | ${SITE_INFO.brand}`);
  }
}
