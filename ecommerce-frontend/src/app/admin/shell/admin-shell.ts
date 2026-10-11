import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../auth/auth';

interface NavChild {
  label: string;
  link: string;
}

interface NavItem {
  label: string;
  link: string;
  // SVG path data drawn on a 24x24 grid.
  icon: string;
  exact?: boolean;
  children?: NavChild[];
}

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.css'
})
export class AdminShell {

  protected readonly email = inject(AuthService).getEmail() ?? '';

  protected readonly nav: NavItem[] = [
    { label: 'Dashboard', link: '/admin', exact: true, icon: 'M4 4h7v7H4zM13 4h7v4h-7zM13 11h7v9h-7zM4 14h7v6H4z' },
    {
      label: 'Products',
      link: '/admin/products',
      icon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3zM12 12l8-4.5M12 12v9M12 12L4 7.5',
      children: [
        { label: 'Add product', link: '/admin/products/add' },
        { label: 'Import', link: '/admin/products/import' },
        { label: 'Bulk update', link: '/admin/products/bulk-update' }
      ]
    },
    { label: 'Categories', link: '/admin/categories', icon: 'M4 4h7l9 9-7 7-9-9V4zM8 8h.01' },
    { label: 'Inventory', link: '/admin/inventory', icon: 'M3 9l9-5 9 5v10H3V9zM9 19v-6h6v6' },
    { label: 'Orders', link: '/admin/orders', icon: 'M3 4h2.5l2 11h10l2-8H7M9.5 19.5h.01M16.5 19.5h.01' }
  ];
}
