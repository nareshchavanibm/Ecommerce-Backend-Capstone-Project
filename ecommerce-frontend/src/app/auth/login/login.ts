import { Component, afterNextRender, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthLayout } from '../layout/auth-layout/auth-layout';
import { AuthService } from '../auth';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { Checkbox } from '../../shared/ui/checkbox/checkbox';
import { PasswordInput } from '../../shared/ui/password-input/password-input';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { emailFormat } from '../../shared/validators/auth-validators';

const REMEMBERED_EMAIL_KEY = 'rememberedEmail';

@Component({
  selector: 'app-login',
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
  templateUrl: './login.html',
  styleUrl: '../auth-form.css'
})
export class Login {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, emailFormat()] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    remember: new FormControl(false, { nonNullable: true })
  });

  protected readonly loading = signal(false);

  protected readonly errorMessage = signal('');

  protected readonly notice = signal('');

  constructor() {
    const params = inject(ActivatedRoute).snapshot.queryParamMap;

    if (params.has('registered')) {
      this.notice.set('Account created. Sign in to continue.');
    } else if (params.has('reset')) {
      this.notice.set('Password updated. Sign in with your new password.');
    }

    afterNextRender(() => {
      const email = localStorage.getItem(REMEMBERED_EMAIL_KEY);

      if (email) {
        this.form.patchValue({ email, remember: true });
      }
    });
  }

  protected submit(): void {
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, remember } = this.form.getRawValue();

    this.loading.set(true);

    this.authService.login(email.trim(), password).subscribe({
      next: () => {
        if (remember) {
          localStorage.setItem(REMEMBERED_EMAIL_KEY, email.trim());
        } else {
          localStorage.removeItem(REMEMBERED_EMAIL_KEY);
        }

        this.router.navigate(['/home']);
      },
      error: error => {
        this.loading.set(false);
        this.errorMessage.set(
          error.status === 401
            ? 'Incorrect email or password.'
            : 'Something went wrong. Please try again.'
        );
      }
    });
  }
}
