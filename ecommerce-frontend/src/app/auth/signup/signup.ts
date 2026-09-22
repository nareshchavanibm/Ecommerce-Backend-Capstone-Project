import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth';

@Component({
  selector: 'app-signup',
  imports: [FormsModule, RouterLink],
  templateUrl: './signup.html',
  styleUrl: './signup.css'
})
export class Signup {

  firstName = '';
  lastName = '';
  email = '';
  password = '';
  confirmPassword = '';

  errorMessage = '';
  successMessage = '';

  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  signup(form: any): void {

    this.errorMessage = '';
    this.successMessage = '';

    // Check Angular form validation
    if (form.invalid) {
      this.errorMessage = 'Please correct the errors in the form.';
      return;
    }

    // Extra frontend validation
    if (this.password !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    const request = {
      firstName: this.firstName.trim(),
      lastName: this.lastName.trim(),
      email: this.email.trim(),
      password: this.password
    };

    this.authService.signup(request).subscribe({

      next: (response) => {

        console.log('Signup successful:', response);

        this.successMessage =
          'Registration successful. Please login.';

        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 1000);
      },

      error: (error) => {

        console.error('Signup failed:', error);

        // Duplicate email from backend
        if (error.status === 400) {

          this.errorMessage =
            error.error?.message ||
            'Registration failed. Please check your details.';

        } else {

          this.errorMessage =
            'Something went wrong. Please try again.';
        }
      }
    });
  }
}
