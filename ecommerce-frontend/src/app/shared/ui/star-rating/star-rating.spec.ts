import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule, FormControl, Validators } from '@angular/forms';

import { RatingInput } from '../rating-input/rating-input';
import { StarRating } from './star-rating';

@Component({
  imports: [StarRating],
  template: '<app-star-rating [value]="value()" [count]="count()" showLabel />'
})
class StarHost {
  value = signal(4.2);
  count = signal<number | null>(12);
}

@Component({
  imports: [ReactiveFormsModule, RatingInput],
  template: '<app-rating-input [formControl]="rating" />'
})
class InputHost {
  rating = new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] });
}

describe('StarRating', () => {
  let fixture: ComponentFixture<StarHost>;

  beforeEach(() => {
    fixture = TestBed.createComponent(StarHost);
    fixture.detectChanges();
  });

  const host = () => fixture.nativeElement.querySelector('app-star-rating') as HTMLElement;

  it('describes the rating for assistive technology', () => {
    expect(host().getAttribute('aria-label')).toBe('Rated 4.2 out of 5, Good, 12 reviews');
  });

  it('colors by band and fills partial stars', () => {
    expect(host().getAttribute('data-band')).toBe('4');
    expect((host().querySelector('.stars__row--fill') as HTMLElement).style.width).toBe('84%');
  });

  it('updates the band when the rating drops', () => {
    fixture.componentInstance.value.set(1.8);
    fixture.componentInstance.count.set(null);
    fixture.detectChanges();

    expect(host().getAttribute('data-band')).toBe('2');
    expect(host().getAttribute('aria-label')).toBe('Rated 1.8 out of 5, Below average');
  });
});

describe('RatingInput', () => {
  let fixture: ComponentFixture<InputHost>;

  beforeEach(() => {
    fixture = TestBed.createComponent(InputHost);
    fixture.detectChanges();
  });

  const radios = () => Array.from(fixture.nativeElement.querySelectorAll('input[type=radio]')) as HTMLInputElement[];

  it('offers five stars and writes the choice to the form', () => {
    expect(radios().length).toBe(5);

    radios()[3].click();
    fixture.detectChanges();

    expect(fixture.componentInstance.rating.value).toBe(4);
    expect(fixture.nativeElement.querySelector('.rate__stars').getAttribute('data-band')).toBe('4');
  });

  it('shows an error once touched without a rating', () => {
    expect(fixture.nativeElement.querySelector('.rate__error')).toBeNull();

    fixture.componentInstance.rating.markAsTouched();
    fixture.nativeElement.querySelector('fieldset').dispatchEvent(new FocusEvent('focusout'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rate__error')?.textContent).toContain('Choose a rating');
  });
});
