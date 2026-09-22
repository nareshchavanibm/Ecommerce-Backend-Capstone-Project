import { Component } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class Navbar {

  constructor(
    public authService: AuthService,
    private router: Router
  ) { }

  logout(): void {

    this.authService.logout();

    this.router.navigate(['/login']);
  }
}
