import { AbstractControl, ValidationErrors } from '@angular/forms';

export type CardBrand = 'visa' | 'mastercard' | 'rupay' | 'amex' | '';

export const CARD_BRAND_LABELS: Record<CardBrand, string> = {
  visa: 'Visa',
  mastercard: 'Mastercard',
  rupay: 'RuPay',
  amex: 'Amex',
  '': ''
};

export const digitsOnly = (value: string): string => value.replace(/\D/g, '');

export function cardBrand(value: string): CardBrand {

  const digits = digitsOnly(value);

  if (/^3[47]/.test(digits)) {
    return 'amex';
  }

  if (/^4/.test(digits)) {
    return 'visa';
  }

  if (/^(5[1-5]|2(2[2-9][1-9]|[3-6]\d\d|7[01]\d|720))/.test(digits)) {
    return 'mastercard';
  }

  if (/^(60|65|81|82|508)/.test(digits)) {
    return 'rupay';
  }

  return '';
}

export function formatCardNumber(value: string): string {

  const digits = digitsOnly(value);

  if (cardBrand(digits) === 'amex') {
    return [digits.slice(0, 4), digits.slice(4, 10), digits.slice(10, 15)].filter(Boolean).join(' ');
  }

  return (digits.slice(0, 19).match(/.{1,4}/g) ?? []).join(' ');
}

// "1" stays "1", "12" becomes "12/", "123" becomes "12/3".
export function formatExpiry(value: string): string {

  const digits = digitsOnly(value).slice(0, 4);

  if (digits.length < 2) {
    return digits;
  }

  return `${digits.slice(0, 2)}/${digits.slice(2)}`;
}

export function cardNumberValidator(control: AbstractControl<string>): ValidationErrors | null {

  const digits = digitsOnly(control.value ?? '');

  if (!digits) {
    return null;
  }

  const expected = cardBrand(digits) === 'amex' ? 15 : 16;

  if (digits.length < 13 || (digits.length < expected && digits.length < 19)) {
    return { incomplete: true };
  }

  let sum = 0;

  for (let i = 0; i < digits.length; i++) {
    let digit = Number(digits[digits.length - 1 - i]);

    if (i % 2 === 1) {
      digit *= 2;

      if (digit > 9) {
        digit -= 9;
      }
    }

    sum += digit;
  }

  return sum % 10 === 0 ? null : { cardNumber: true };
}

export function expiryValidator(control: AbstractControl<string>): ValidationErrors | null {

  if (!control.value) {
    return null;
  }

  const match = /^(0[1-9]|1[0-2])\/(\d{2})$/.exec(control.value);

  if (!match) {
    return { expiry: true };
  }

  const now = new Date();
  const year = 2000 + Number(match[2]);
  const month = Number(match[1]);

  return year < now.getFullYear() || (year === now.getFullYear() && month < now.getMonth() + 1)
    ? { expired: true }
    : null;
}
