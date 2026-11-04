import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

import { countWords } from '../utils/rating';

export function maxWords(max: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const actual = countWords(control.value);

    return actual > max ? { maxWords: { max, actual } } : null;
  };
}
