import { Component, booleanAttribute, computed, input, signal } from '@angular/core';

import { PASSWORD_STRENGTH_LABELS, passwordStrength } from '../../validators/auth-validators';
import { TextInput } from '../text-input/text-input';

@Component({
  selector: 'app-password-input',
  templateUrl: './password-input.html',
  styleUrls: ['../text-input/text-input.css', './password-input.css']
})
export class PasswordInput extends TextInput {

  readonly showStrength = input(false, { transform: booleanAttribute });

  protected readonly visible = signal(false);

  protected readonly strength = computed(() => passwordStrength(this.value()));

  protected readonly strengthLabel = computed(() => PASSWORD_STRENGTH_LABELS[this.strength()]);

  protected readonly segments = [1, 2, 3, 4];

  protected toggle(): void {
    this.visible.update(visible => !visible);
  }
}
