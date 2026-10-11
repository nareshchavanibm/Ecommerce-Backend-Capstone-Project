import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthLayout } from '../layout/auth-layout/auth-layout';
import { AuthService } from '../auth';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { emailFormat } from '../../shared/validators/auth-validators';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout, TextInput, Button, Alert],
  templateUrl: './forgot-password.html',
  styleUrl: '../auth-form.css'
})
export class ForgotPassword {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, emailFormat()] })
  });

  protected readonly loading = signal(false);

  protected readonly errorMessage = signal('');

  protected submit(): void {
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const email = this.form.controls.email.value.trim();

    this.loading.set(true);

    this.authService.forgotPassword(email).subscribe({
      next: () => this.router.navigate(['/verify-otp'], { queryParams: { email } }),
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('We could not send a code. Please try again.');
      }
    });
  }
}
