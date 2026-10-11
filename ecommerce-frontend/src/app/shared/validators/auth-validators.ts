import { ValidatorFn } from '@angular/forms';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const SYMBOL_PATTERN = /[^A-Za-z0-9\s]/;

export function emailFormat(): ValidatorFn {
  return control =>
    !control.value || EMAIL_PATTERN.test(control.value) ? null : { email: true };
}

// Min 8 characters, at least one number and one symbol.
export function strongPassword(): ValidatorFn {
  return control => {
    const value: string = control.value ?? '';

    if (!value) {
      return null;
    }

    if (value.length < 8) {
      return { passwordLength: true };
    }

    if (!/\d/.test(value)) {
      return { passwordNumber: true };
    }

    if (!SYMBOL_PATTERN.test(value)) {
      return { passwordSymbol: true };
    }

    return null;
  };
}

// Sibling control in the same FormGroup; revalidate this control when the sibling changes.
export function matchesControl(otherName: string): ValidatorFn {
  return control => {
    const other = control.parent?.get(otherName);

    return other && control.value !== other.value ? { mismatch: true } : null;
  };
}

export function splitFullName(value: string): { firstName: string; lastName: string } {
  const [firstName = '', ...rest] = value.trim().split(/\s+/);

  return { firstName, lastName: rest.join(' ') };
}

// Backend requires first and last name of 2-50 characters each.
export function fullName(): ValidatorFn {
  return control => {
    const value: string = control.value ?? '';

    if (!value.trim()) {
      return null;
    }

    const { firstName, lastName } = splitFullName(value);
    const valid = [firstName, lastName].every(part => part.length >= 2 && part.length <= 50);

    return valid ? null : { fullName: true };
  };
}

export const PASSWORD_STRENGTH_LABELS = ['', 'Weak', 'Fair', 'Good', 'Strong'];

// 0-4: length, number, symbol, and extra length or mixed case.
export function passwordStrength(value: string): number {
  if (!value) {
    return 0;
  }

  const score =
    Number(value.length >= 8) +
    Number(/\d/.test(value)) +
    Number(SYMBOL_PATTERN.test(value)) +
    Number(value.length >= 12 || (/[a-z]/.test(value) && /[A-Z]/.test(value)));

  return value.length < 8 ? Math.min(score, 1) : Math.max(score, 1);
}
