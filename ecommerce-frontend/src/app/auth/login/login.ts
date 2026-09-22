import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../auth';

@Component({
  selector: 'app-login',

  imports: [
    FormsModule,
    RouterLink
  ],

  templateUrl: './login.html',

  styleUrl: './login.css'
})
export class Login {

  email = '';

  password = '';

  errorMessage = '';


  constructor(
    private authService: AuthService,

    private router: Router
  ) { }


  login(): void {

    this.errorMessage = '';


    if (!this.email.trim()) {

      this.errorMessage =
        'Email is required';

      return;
    }


    if (!this.password.trim()) {

      this.errorMessage =
        'Password is required';

      return;
    }


    this.authService
      .login(
        this.email,
        this.password
      )
      .subscribe({

        next: (response) => {

          console.log(
            'Login successful:',
            response
          );

          this.router.navigate([
            '/home'
          ]);

        },


        error: (error) => {

          console.error(
            'Login failed:',
            error
          );

          this.errorMessage =
            'Invalid email or password';

        }

      });
  }

}
