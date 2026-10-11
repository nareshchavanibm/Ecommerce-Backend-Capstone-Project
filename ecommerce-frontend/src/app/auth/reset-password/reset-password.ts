import { Component, DestroyRef, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthLayout } from '../layout/auth-layout/auth-layout';
import { AuthService } from '../auth';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { PasswordInput } from '../../shared/ui/password-input/password-input';
import { matchesControl, strongPassword } from '../../shared/validators/auth-validators';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout, PasswordInput, Button, Alert],
  templateUrl: './reset-password.html',
  styleUrl: '../auth-form.css'
})
export class ResetPassword {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  private readonly email = inject(ActivatedRoute).snapshot.queryParamMap.get('email') ?? '';

  // The verified code arrives via navigation state so it never appears in the URL.
  private code = '';

  protected readonly form = new FormGroup({
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, strongPassword()] }),
    confirmPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, matchesControl('password')]
    })
  });

  protected readonly loading = signal(false);

  protected readonly errorMessage = signal('');

  constructor() {
    this.form.controls.password.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.form.controls.confirmPassword.updateValueAndValidity());

    afterNextRender(() => {
      this.code = history.state?.code ?? '';

      if (!this.email || !this.code) {
        this.router.navigate(['/forgot-password']);
      }
    });
  }

  protected submit(): void {
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);

    this.authService.resetPassword(this.email, this.code, this.form.controls.password.value).subscribe({
      next: () => this.router.navigate(['/login'], { queryParams: { reset: 1 } }),
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('We could not reset your password. The code may have expired.');
      }
    });
  }
}
