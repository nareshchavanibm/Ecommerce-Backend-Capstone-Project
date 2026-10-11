import { Component, booleanAttribute, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

let nextId = 0;

export interface SelectOption {
  value: string | number;
  label: string;
}

@Component({
  selector: 'app-select',
  templateUrl: './select.html',
  styleUrl: './select.css'
})
export class Select implements ControlValueAccessor {

  readonly label = input.required<string>();

  readonly options = input.required<SelectOption[]>();

  readonly placeholder = input('');

  readonly helperText = input('');

  readonly required = input(false, { transform: booleanAttribute });

  readonly error = input('');

  readonly errorMessages = input<Record<string, string>>({});

  protected readonly id = `app-select-${nextId++}`;

  protected readonly value = signal<string | number>('');

  protected readonly disabled = signal(false);

  protected readonly control = inject(NgControl, { self: true, optional: true });

  private onChange: (value: string | number) => void = () => {};

  private onTouched: () => void = () => {};

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }
  }

  // Validator errors surface only once the control is touched.
  protected get errorText(): string {
    if (this.error()) {
      return this.error();
    }

    const errors = this.control?.errors;

    if (!this.control?.touched || !errors) {
      return '';
    }

    const key = Object.keys(errors)[0];

    return this.errorMessages()[key] ?? (key === 'required' ? 'Select an option.' : 'Select a valid option.');
  }

  protected get describedBy(): string | null {
    if (this.errorText) {
      return `${this.id}-error`;
    }

    return this.helperText() ? `${this.id}-helper` : null;
  }

  protected onSelect(event: Event): void {
    const raw = (event.target as HTMLSelectElement).value;
    const match = this.options().find(option => String(option.value) === raw);
    const next = match ? match.value : raw;

    this.value.set(next);
    this.onChange(next);
  }

  protected onBlur(): void {
    this.onTouched();
  }

  writeValue(value: string | number | null): void {
    this.value.set(value ?? '');
  }

  registerOnChange(fn: (value: string | number) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
