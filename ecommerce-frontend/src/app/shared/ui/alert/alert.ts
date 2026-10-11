import { Component, input } from '@angular/core';

@Component({
  selector: 'app-alert',
  templateUrl: './alert.html',
  styleUrl: './alert.css',
  host: {
    '[attr.role]': 'variant() === "error" ? "alert" : "status"',
    '[attr.data-variant]': 'variant()'
  }
})
export class Alert {

  readonly variant = input<'error' | 'success' | 'info'>('error');
}
