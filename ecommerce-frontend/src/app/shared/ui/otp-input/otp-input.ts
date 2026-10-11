import { Component, ElementRef, booleanAttribute, inject, input, output, signal, viewChildren } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

const LENGTH = 6;

let nextId = 0;

@Component({
  selector: 'app-otp-input',
  templateUrl: './otp-input.html',
  styleUrl: './otp-input.css'
})
export class OtpInput implements ControlValueAccessor {

  readonly label = input('Verification code');

  readonly helperText = input('');

  readonly error = input('');

  readonly errorMessage = input('Enter the 6-digit code.');

  readonly success = input(false, { transform: booleanAttribute });

  readonly completed = output<string>();

  protected readonly id = `app-otp-input-${nextId++}`;

  protected readonly indexes = Array.from({ length: LENGTH }, (_, i) => i);

  protected readonly digits = signal<string[]>(Array(LENGTH).fill(''));

  protected readonly disabled = signal(false);

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');

  private readonly control = inject(NgControl, { self: true, optional: true });

  private onChange: (value: string) => void = () => {};

  private onTouched: () => void = () => {};

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }
  }

  protected get errorText(): string {
    if (this.error()) {
      return this.error();
    }

    return this.control?.touched && this.control.invalid ? this.errorMessage() : '';
  }

  protected get describedBy(): string | null {
    if (this.errorText) {
      return `${this.id}-error`;
    }

    return this.helperText() ? `${this.id}-helper` : null;
  }

  protected onInput(index: number, event: Event): void {
    const element = event.target as HTMLInputElement;
    const raw = element.value.replace(/\D/g, '');

    if (!raw) {
      this.update(index, '');
    } else if (raw.length > 2) {
      this.fill(index, raw);
    } else {
      this.update(index, raw.slice(-1));
      this.focusBox(index + 1);
    }

    element.value = this.digits()[index];
  }

  protected onKeydown(index: number, event: KeyboardEvent): void {
    switch (event.key) {
      case 'Backspace':
        if (!this.digits()[index] && index > 0) {
          event.preventDefault();
          this.update(index - 1, '');
          this.focusBox(index - 1);
        }
        break;
      case 'ArrowLeft':
        event.preventDefault();
        this.focusBox(index - 1);
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.focusBox(index + 1);
        break;
    }
  }

  protected onPaste(index: number, event: ClipboardEvent): void {
    event.preventDefault();

    const text = (event.clipboardData?.getData('text') ?? '').replace(/\D/g, '');

    this.fill(text.length >= LENGTH ? 0 : index, text);
  }

  protected onFocus(event: FocusEvent): void {
    (event.target as HTMLInputElement).select();
  }

  protected onFocusOut(event: FocusEvent): void {
    const group = event.currentTarget as HTMLElement;

    if (!group.contains(event.relatedTarget as Node | null)) {
      this.onTouched();
    }
  }

  private update(index: number, digit: string): void {
    const next = [...this.digits()];

    next[index] = digit;
    this.commit(next);
  }

  private fill(start: number, text: string): void {
    const chars = text.slice(0, LENGTH - start).split('');

    if (!chars.length) {
      return;
    }

    const next = [...this.digits()];

    chars.forEach((char, i) => (next[start + i] = char));
    this.commit(next);
    this.focusBox(Math.min(start + chars.length, LENGTH - 1));
  }

  private commit(next: string[]): void {
    const value = next.join('');

    this.digits.set(next);
    this.onChange(value);

    if (value.length === LENGTH) {
      this.completed.emit(value);
    }
  }

  private focusBox(index: number): void {
    this.boxes()[index]?.nativeElement.focus();
  }

  writeValue(value: string | null): void {
    const chars = (value ?? '').replace(/\D/g, '').slice(0, LENGTH).split('');

    this.digits.set(Array.from({ length: LENGTH }, (_, i) => chars[i] ?? ''));
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
