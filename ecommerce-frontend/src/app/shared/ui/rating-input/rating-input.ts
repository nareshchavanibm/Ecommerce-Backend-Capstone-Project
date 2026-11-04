import { Component, DoCheck, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';

import { ratingBand, ratingLabel } from '../../utils/rating';

let nextId = 0;

// Star picker for forms; stars take the color of the band they select.
@Component({
  selector: 'app-rating-input',
  templateUrl: './rating-input.html',
  styleUrl: './rating-input.css'
})
export class RatingInput implements ControlValueAccessor, DoCheck {

  readonly label = input('Your rating');

  readonly errorMessage = input('Choose a rating from 1 to 5.');

  protected readonly id = `app-rating-input-${nextId++}`;

  protected readonly stars = [1, 2, 3, 4, 5];

  protected readonly value = signal(0);

  protected readonly hover = signal(0);

  protected readonly disabled = signal(false);

  private readonly showError = signal(false);

  protected readonly control = inject(NgControl, { self: true, optional: true });

  private onChange: (value: number) => void = () => {};

  private onTouched: () => void = () => {};

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }
  }

  // Inline blur validation: shown once the group has been left without a rating.
  protected get errorText(): string {
    return this.showError() ? this.errorMessage() : '';
  }

  // markAllAsTouched() on submit changes no input or event of this component, so it is picked up here.
  ngDoCheck(): void {
    this.showError.set(!!this.control?.touched && !!this.control.invalid);
  }

  protected get shown(): number {
    return this.hover() || this.value();
  }

  protected get band(): number | null {
    return this.shown ? ratingBand(this.shown) : null;
  }

  protected get shownLabel(): string {
    return this.shown ? `${this.shown} of 5, ${ratingLabel(this.shown)}` : 'Select a rating';
  }

  protected optionLabel(star: number): string {
    return `${star} ${star === 1 ? 'star' : 'stars'}, ${ratingLabel(star)}`;
  }

  protected select(star: number): void {
    this.value.set(star);
    this.onChange(star);
  }

  // Focus leaving the whole group (not moving between stars) counts as touched.
  protected onFocusOut(event: FocusEvent, group: HTMLElement): void {
    if (!group.contains(event.relatedTarget as Node | null)) {
      this.onTouched();
    }
  }

  writeValue(value: number | null): void {
    this.value.set(value ?? 0);
  }

  registerOnChange(fn: (value: number) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }
}
