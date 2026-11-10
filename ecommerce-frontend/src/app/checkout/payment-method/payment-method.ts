import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth';
import { PAYMENT_METHODS, PaymentSelection, UPI_OPTIONS } from '../../core/services/payment-selection';
import {
  CARD_BRAND_LABELS, cardBrand, cardNumberValidator, digitsOnly, expiryValidator, formatCardNumber, formatExpiry
} from '../../shared/utils/card';

type CardField = 'number' | 'name' | 'expiry' | 'cvv';

const UPI_ID_PATTERN = /^[\w.-]{2,256}@[a-zA-Z]{2,64}$/;

const PHONE_PATTERN = /^[6-9]\d{9}$/;

const UPI_LOGO_TEXT: Record<string, string> = { GooglePay: 'G', PhonePe: 'Pe', Paytm: 'Pt', BHIM: 'B', UpiId: '@' };

const MESSAGES: Record<CardField | 'upiId' | 'upiPhone', Record<string, string>> = {
  upiPhone: { required: 'Enter your mobile number.', pattern: 'Enter a valid 10 digit mobile number.' },
  number: { required: 'Enter the card number.', incomplete: 'Enter the full card number.', cardNumber: 'Enter a valid card number.' },
  name: { required: 'Enter the name on the card.' },
  expiry: { required: 'Enter the expiry date.', expiry: 'Use the format MM/YY.', expired: 'This card has expired.' },
  cvv: { required: 'Enter the security code.', pattern: 'Enter the 3 or 4 digit security code.' },
  upiId: { required: 'Enter your UPI ID.', pattern: 'Enter a valid UPI ID, for example name@bank.' }
};

@Component({
  selector: 'app-payment-method',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './payment-method.html',
  styleUrl: './payment-method.css'
})
export class PaymentMethodPage implements OnInit {

  private readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  private readonly selection = inject(PaymentSelection);

  protected readonly methods = PAYMENT_METHODS;

  protected readonly upiOptions = UPI_OPTIONS;

  protected readonly brandChips = ['Visa', 'Mastercard', 'RuPay', 'Amex'];

  protected readonly method = new FormControl<string>(this.selection.method(), { nonNullable: true, validators: [Validators.required] });

  // Card details are validated here only and are never sent, stored or kept after leaving the page.
  protected readonly cardForm = new FormGroup({
    number: new FormControl('', { nonNullable: true, validators: [Validators.required, cardNumberValidator] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(60)] }),
    expiry: new FormControl('', { nonNullable: true, validators: [Validators.required, expiryValidator] }),
    cvv: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{3,4}$/)] })
  });

  protected readonly upiApp = new FormControl<string>(this.selection.upiApp() || 'GooglePay', { nonNullable: true });

  protected readonly upiId = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(UPI_ID_PATTERN)] });

  // The mobile number is only used to word the confirmation here; the API never receives it.
  protected readonly upiPhone = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(PHONE_PATTERN)] });

  // The part of the card preview that matches the focused field is highlighted.
  protected readonly focused = signal<CardField | ''>('');

  protected readonly methodError = signal(false);

  ngOnInit(): void {

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
    }
  }

  protected get brand() {
    return cardBrand(this.cardForm.controls.number.value);
  }

  protected get brandLabel(): string {
    return CARD_BRAND_LABELS[this.brand];
  }

  protected get previewCvv(): string {
    return '\u2022'.repeat(Math.max(3, this.cardForm.controls.cvv.value.length));
  }

  protected get previewNumber(): string {

    const digits = digitsOnly(this.cardForm.controls.number.value);
    const groups = this.brand === 'amex' ? [4, 6, 5] : [4, 4, 4, 4];
    let start = 0;

    return groups
      .map(size => {
        const part = digits.slice(start, start + size).padEnd(size, '\u2022');
        start += size;
        return part;
      })
      .join(' ');
  }

  protected onNumber(event: Event): void {

    const input = event.target as HTMLInputElement;
    const formatted = formatCardNumber(input.value);

    input.value = formatted;
    this.cardForm.controls.number.setValue(formatted);
  }

  protected onExpiry(event: Event): void {

    const input = event.target as HTMLInputElement;
    const formatted = formatExpiry(input.value);

    input.value = formatted;
    this.cardForm.controls.expiry.setValue(formatted);
  }

  protected onCvv(event: Event): void {

    const input = event.target as HTMLInputElement;
    const digits = digitsOnly(input.value).slice(0, 4);

    input.value = digits;
    this.cardForm.controls.cvv.setValue(digits);
  }

  protected cardError(field: CardField): string {

    const control = this.cardForm.controls[field];

    if (!control.touched || !control.errors) {
      return '';
    }

    return MESSAGES[field][Object.keys(control.errors)[0]] ?? 'Enter a valid value.';
  }

  protected logoText(app: string): string {
    return UPI_LOGO_TEXT[app] ?? '';
  }

  protected onPhone(event: Event): void {

    const input = event.target as HTMLInputElement;
    const digits = digitsOnly(input.value).slice(0, 10);

    input.value = digits;
    this.upiPhone.setValue(digits);
  }

  protected upiPhoneError(): string {

    if (!this.upiPhone.touched || !this.upiPhone.errors) {
      return '';
    }

    return MESSAGES.upiPhone[Object.keys(this.upiPhone.errors)[0]] ?? 'Enter a valid value.';
  }

  protected upiIdError(): string {

    if (!this.upiId.touched || !this.upiId.errors) {
      return '';
    }

    return MESSAGES.upiId[Object.keys(this.upiId.errors)[0]] ?? 'Enter a valid value.';
  }

  protected submit(event: Event): void {

    event.preventDefault();

    this.method.markAsTouched();
    this.methodError.set(this.method.invalid);

    if (this.method.invalid) {
      return;
    }

    const method = this.method.value;
    let summary = PAYMENT_METHODS.find(item => item.value === method)?.label ?? method;

    if (method === 'Card') {
      this.cardForm.markAllAsTouched();

      if (this.cardForm.invalid) {
        return;
      }

      const digits = digitsOnly(this.cardForm.controls.number.value);
      summary = `${this.brandLabel || 'Card'} ending ${digits.slice(-4)}`;
    }

    if (method === 'UPI') {
      if (this.upiApp.value === 'UpiId') {
        this.upiId.markAsTouched();

        if (this.upiId.invalid) {
          return;
        }

        summary = `UPI \u00b7 ${this.upiId.value.trim()}`;
      } else {
        this.upiPhone.markAsTouched();

        if (this.upiPhone.invalid) {
          return;
        }

        const label = UPI_OPTIONS.find(item => item.value === this.upiApp.value)?.label;

        summary = `UPI \u00b7 ${label} \u00b7 +91 ${'\u2022'.repeat(6)}${this.upiPhone.value.slice(-4)}`;
      }
    }

    this.selection.method.set(method);
    this.selection.upiApp.set(method === 'UPI' ? this.upiApp.value : '');
    this.selection.summary.set(summary);

    this.router.navigate(['/checkout']);
  }
}
