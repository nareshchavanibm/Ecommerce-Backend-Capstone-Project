import { Component, booleanAttribute, input, output } from '@angular/core';

@Component({
  selector: 'app-quantity-stepper',
  templateUrl: './quantity-stepper.html',
  styleUrl: './quantity-stepper.css'
})
export class QuantityStepper {

  readonly value = input.required<number>();

  readonly min = input(1);

  // null means no known upper limit (the server still validates).
  readonly max = input<number | null>(null);

  readonly label = input('Quantity');

  readonly disabled = input(false, { transform: booleanAttribute });

  readonly valueChange = output<number>();

  protected canIncrease(): boolean {

    const max = this.max();

    return !this.disabled() && (max === null || this.value() < max);
  }

  protected canDecrease(): boolean {
    return !this.disabled() && this.value() > this.min();
  }

  protected step(delta: number): void {

    const next = this.value() + delta;

    if (delta < 0 ? this.canDecrease() : this.canIncrease()) {
      this.valueChange.emit(next);
    }
  }
}
