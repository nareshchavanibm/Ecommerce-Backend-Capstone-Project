import { Component, booleanAttribute, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

let nextId = 0;

const DEFAULT_MESSAGES: Record<string, string> = {
  required: 'This field is required.',
  email: 'Enter a valid email address.',
  minlength: 'Value is too short.',
  passwordLength: 'Use at least 8 characters.',
  passwordNumber: 'Include at least one number.',
  passwordSymbol: 'Include at least one symbol.',
  mismatch: 'Passwords do not match.',
  fullName: 'Enter your first and last name.'
};

@Component({
  selector: 'app-text-input',
  templateUrl: './text-input.html',
  styleUrl: './text-input.css'
})
export class TextInput implements ControlValueAccessor {

  readonly label = input.required<string>();

  readonly type = input<'text' | 'email' | 'tel'>('text');

  readonly icon = input<'user' | 'mail' | 'lock' | ''>('');

  readonly placeholder = input('');

  readonly helperText = input('');

  readonly autocomplete = input('off');

  readonly required = input(false, { transform: booleanAttribute });

  // Keeps only digits as the user types or pastes.
  readonly numericOnly = input(false, { transform: booleanAttribute });

  readonly maxlength = input<number | null>(null);

  readonly success = input(false, { transform: booleanAttribute });

  // Explicit error (e.g. from the server); overrides validator errors.
  readonly error = input('');

  // Per-validator-key message overrides, e.g. { required: 'Enter your email.' }
  readonly errorMessages = input<Record<string, string>>({});

  protected readonly id = `app-text-input-${nextId++}`;

  protected readonly value = signal('');

  protected readonly disabled = signal(false);

  protected readonly control = inject(NgControl, { self: true, optional: true });

  private onChange: (value: string) => void = () => {};

  private onTouched: () => void = () => {};

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }
  }

  // Inline blur validation: validator errors surface only once touched.
  protected get errorText(): string {
    if (this.error()) {
      return this.error();
    }

    const errors = this.control?.errors;

    if (!this.control?.touched || !errors) {
      return '';
    }

    const key = Object.keys(errors)[0];

    return this.errorMessages()[key] ?? DEFAULT_MESSAGES[key] ?? 'Enter a valid value.';
  }

  protected get describedBy(): string | null {
    if (this.errorText) {
      return `${this.id}-error`;
    }

    return this.helperText() ? `${this.id}-helper` : null;
  }

  protected onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let next = input.value;

    if (this.numericOnly()) {
      next = next.replace(/\D/g, '');
    }

    const max = this.maxlength();

    if (max !== null) {
      next = next.slice(0, max);
    }

    if (next !== input.value) {
      input.value = next;
    }

    this.value.set(next);
    this.onChange(next);
  }

  protected onBlur(): void {
    this.onTouched();
  }

  writeValue(value: string | null): void {
    this.value.set(value ?? '');
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
