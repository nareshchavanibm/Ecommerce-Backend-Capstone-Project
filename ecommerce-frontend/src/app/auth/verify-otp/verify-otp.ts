import { Component, DestroyRef, afterNextRender, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthLayout } from '../layout/auth-layout/auth-layout';
import { AuthService } from '../auth';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { OtpInput } from '../../shared/ui/otp-input/otp-input';

const RESEND_SECONDS = 30;

@Component({
  selector: 'app-verify-otp',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout, OtpInput, Button, Alert],
  templateUrl: './verify-otp.html',
  styleUrl: '../auth-form.css'
})
export class VerifyOtp {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  protected readonly email = inject(ActivatedRoute).snapshot.queryParamMap.get('email') ?? '';

  protected readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{6}$/)]
    })
  });

  protected readonly loading = signal(false);

  protected readonly verified = signal(false);

  protected readonly resending = signal(false);

  protected readonly codeError = signal('');

  protected readonly errorMessage = signal('');

  protected readonly notice = signal('');

  protected readonly secondsLeft = signal(RESEND_SECONDS);

  protected readonly timerText = computed(() => `0:${String(this.secondsLeft()).padStart(2, '0')}`);

  private timerId?: ReturnType<typeof setInterval>;

  constructor() {
    if (!this.email) {
      this.router.navigate(['/forgot-password']);
    }

    afterNextRender(() => this.startTimer());
    inject(DestroyRef).onDestroy(() => clearInterval(this.timerId));
  }

  protected submit(): void {
    this.codeError.set('');
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const code = this.form.controls.code.value;

    this.loading.set(true);

    this.authService.verifyOtp(this.email, code).subscribe({
      next: () => {
        this.verified.set(true);
        this.router.navigate(['/reset-password'], {
          queryParams: { email: this.email },
          state: { code }
        });
      },
      error: error => {
        this.loading.set(false);

        if (error.status === 400) {
          this.codeError.set('That code is incorrect or has expired.');
        } else {
          this.errorMessage.set('Something went wrong. Please try again.');
        }
      }
    });
  }

  protected resend(): void {
    this.codeError.set('');
    this.errorMessage.set('');
    this.notice.set('');
    this.resending.set(true);

    this.authService.resendOtp(this.email).subscribe({
      next: () => {
        this.resending.set(false);
        this.form.reset();
        this.notice.set('A new code is on its way.');
        this.startTimer();
      },
      error: () => {
        this.resending.set(false);
        this.errorMessage.set('We could not resend the code. Please try again.');
      }
    });
  }

  private startTimer(): void {
    clearInterval(this.timerId);
    this.secondsLeft.set(RESEND_SECONDS);

    this.timerId = setInterval(() => {
      this.secondsLeft.update(seconds => seconds - 1);

      if (this.secondsLeft() <= 0) {
        clearInterval(this.timerId);
      }
    }, 1000);
  }
}
