import { Component, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

let nextId = 0;

@Component({
  selector: 'app-checkbox',
  templateUrl: './checkbox.html',
  styleUrl: './checkbox.css'
})
export class Checkbox implements ControlValueAccessor {

  // Shown once the control is touched and invalid.
  readonly errorMessage = input('');

  protected readonly id = `app-checkbox-${nextId++}`;

  protected readonly checked = signal(false);

  protected readonly disabled = signal(false);

  private readonly control = inject(NgControl, { self: true, optional: true });

  private onChange: (value: boolean) => void = () => {};

  private onTouched: () => void = () => {};

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }
  }

  protected get errorText(): string {
    return this.control?.touched && this.control.invalid ? this.errorMessage() : '';
  }

  protected onToggle(event: Event): void {
    const next = (event.target as HTMLInputElement).checked;

    this.checked.set(next);
    this.onChange(next);
  }

  protected onBlur(): void {
    this.onTouched();
  }

  writeValue(value: boolean | null): void {
    this.checked.set(!!value);
  }

  registerOnChange(fn: (value: boolean) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
