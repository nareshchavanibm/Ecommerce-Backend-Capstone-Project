import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthLayout } from '../layout/auth-layout/auth-layout';
import { AuthService } from '../auth';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { Checkbox } from '../../shared/ui/checkbox/checkbox';
import { PasswordInput } from '../../shared/ui/password-input/password-input';
import { TextInput } from '../../shared/ui/text-input/text-input';
import {
  emailFormat,
  fullName,
  matchesControl,
  splitFullName,
  strongPassword
} from '../../shared/validators/auth-validators';

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AuthLayout,
    TextInput,
    PasswordInput,
    Checkbox,
    Button,
    Alert
  ],
  templateUrl: './register.html',
  styleUrl: '../auth-form.css'
})
export class Register {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, fullName()] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, emailFormat()] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, strongPassword()] }),
    confirmPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, matchesControl('password')]
    }),
    terms: new FormControl(false, { nonNullable: true, validators: [Validators.requiredTrue] })
  });

  protected readonly loading = signal(false);

  protected readonly errorMessage = signal('');

  constructor() {
    this.form.controls.password.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.form.controls.confirmPassword.updateValueAndValidity());
  }

  protected submit(): void {
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    this.loading.set(true);

    this.authService
      .signup({
        ...splitFullName(value.fullName),
        email: value.email.trim(),
        password: value.password
      })
      .subscribe({
        next: () => this.router.navigate(['/login'], { queryParams: { registered: 1 } }),
        error: error => {
          this.loading.set(false);
          this.errorMessage.set(
            error.status === 400
              ? error.error?.message || 'Registration failed. Please check your details.'
              : 'Something went wrong. Please try again.'
          );
        }
      });
  }
}
