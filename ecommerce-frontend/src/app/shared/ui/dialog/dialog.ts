import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, PLATFORM_ID, booleanAttribute, effect, inject, input, model, viewChild } from '@angular/core';

let nextId = 0;

@Component({
  selector: 'app-dialog',
  templateUrl: './dialog.html',
  styleUrl: './dialog.css'
})
export class Dialog {

  readonly open = model(false);

  readonly heading = input.required<string>();

  // On small screens the dialog docks to the bottom as a sheet.
  readonly sheet = input(false, { transform: booleanAttribute });

  protected readonly id = `app-dialog-${nextId++}`;

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  constructor() {
    // Native <dialog> provides the focus trap, Escape handling and focus restore.
    effect(() => {
      if (!this.isBrowser) {
        return;
      }

      const element = this.dialog().nativeElement;

      if (this.open() && !element.open) {
        element.showModal();
      } else if (!this.open() && element.open) {
        element.close();
      }
    });
  }

  protected onClose(): void {
    this.open.set(false);
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.open.set(false);
    }
  }
}
