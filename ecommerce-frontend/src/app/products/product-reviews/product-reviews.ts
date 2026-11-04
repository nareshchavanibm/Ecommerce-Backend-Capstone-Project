import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from '../../auth/auth';
import { ReviewService } from '../../core/services/review';
import { RatingSummary, Review, ReviewPage, ReviewSort } from '../../shared/models/review.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Pagination } from '../../shared/ui/pagination/pagination';
import { RatingInput } from '../../shared/ui/rating-input/rating-input';
import { Select, SelectOption } from '../../shared/ui/select/select';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StarRating } from '../../shared/ui/star-rating/star-rating';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { MAX_REVIEW_TITLE, MAX_REVIEW_WORDS, countWords, ratingBand } from '../../shared/utils/rating';
import { maxWords } from '../../shared/validators/review-validators';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-product-reviews',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    Alert,
    Button,
    Dialog,
    Pagination,
    RatingInput,
    Select,
    Skeleton,
    StarRating,
    TextInput
  ],
  templateUrl: './product-reviews.html',
  styleUrl: './product-reviews.css'
})
export class ProductReviews {

  private readonly reviewService = inject(ReviewService);

  private readonly toast = inject(ToastService);

  private readonly destroyRef = inject(DestroyRef);

  protected readonly authService = inject(AuthService);

  readonly productId = input.required<number>();

  // Lets the product page show the same average in its header.
  readonly summaryChange = output<RatingSummary>();

  protected readonly maxWords = MAX_REVIEW_WORDS;

  protected readonly maxTitle = MAX_REVIEW_TITLE;

  protected readonly levels = [5, 4, 3, 2, 1];

  protected readonly sortOptions: SelectOption[] = [
    { value: 'newest', label: 'Newest first' },
    { value: 'highest', label: 'Highest rated' },
    { value: 'lowest', label: 'Lowest rated' }
  ];

  protected readonly sort = new FormControl<string | number>('newest', { nonNullable: true });

  protected readonly data = signal<ReviewPage | null>(null);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly pageNumber = signal(1);

  protected readonly canReview = signal(false);

  protected readonly myReview = signal<Review | null>(null);

  protected readonly formOpen = signal(false);

  protected readonly saving = signal(false);

  protected readonly formError = signal('');

  protected readonly deleteTarget = signal<Review | null>(null);

  protected readonly deleting = signal(false);

  protected readonly form = new FormGroup({
    rating: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
    title: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(MAX_REVIEW_TITLE)] }),
    body: new FormControl('', { nonNullable: true, validators: [maxWords(MAX_REVIEW_WORDS)] })
  });

  protected readonly words = toSignal(
    this.form.controls.body.valueChanges.pipe(map(value => countWords(value))),
    { initialValue: 0 }
  );

  protected readonly isAdmin = computed(() => this.authService.getRole() === 'Admin');

  protected readonly totalPages = computed(() => {

    const page = this.data();

    return page ? Math.max(1, Math.ceil(page.totalItems / page.pageSize)) : 1;
  });

  protected readonly band = computed(() => ratingBand(this.data()?.summary.average ?? 0));

  constructor() {

    toObservable(this.productId).pipe(takeUntilDestroyed()).subscribe(() => {
      this.data.set(null);
      this.pageNumber.set(1);
      this.formOpen.set(false);
      this.loadReviews();
      this.loadMine();
    });

    this.sort.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.pageNumber.set(1);
      this.loadReviews();
    });
  }

  protected loadReviews(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.reviewService.getReviews(this.productId(), this.pageNumber(), this.sort.value as ReviewSort, PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
      next: page => {
        this.data.set(page);
        this.loading.set(false);
        this.summaryChange.emit(page.summary);
      },
      error: () => {
        this.errorMessage.set('Unable to load reviews right now.');
        this.loading.set(false);
      }
    });
  }

  protected goToPage(page: number): void {
    this.pageNumber.set(page);
    this.loadReviews();
  }

  protected percent(level: number): number {

    const summary = this.data()?.summary;

    return summary && summary.count > 0 ? (summary.distribution[level - 1] / summary.count) * 100 : 0;
  }

  protected openForm(existing: Review | null = this.myReview()): void {

    this.formError.set('');
    this.form.reset({
      rating: existing?.rating ?? 0,
      title: existing?.title ?? '',
      body: existing?.body ?? ''
    });
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
  }

  protected get bodyError(): string {

    const control = this.form.controls.body;

    return control.touched && control.errors?.['maxWords']
      ? `Your review can be at most ${MAX_REVIEW_WORDS} words. You have ${this.words()}.`
      : '';
  }

  protected submit(): void {

    this.form.markAllAsTouched();

    if (this.form.invalid || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      rating: value.rating,
      title: value.title.trim() || null,
      body: value.body.trim() || null
    };
    const existing = this.myReview();

    this.saving.set(true);
    this.formError.set('');

    const request = existing
      ? this.reviewService.update(existing.id, input)
      : this.reviewService.create(this.productId(), input);

    request.subscribe({
      next: review => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.myReview.set(review);
        this.canReview.set(true);
        this.toast.success(existing ? 'Your review was updated.' : 'Thanks for your review.');
        this.loadReviews();
      },
      error: error => {
        this.saving.set(false);
        this.formError.set(error.error?.message ?? 'We could not save your review. Try again.');
      }
    });
  }

  protected confirmDelete(): void {

    const review = this.deleteTarget();

    if (!review || this.deleting()) {
      return;
    }

    this.deleting.set(true);

    this.reviewService.delete(review.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteTarget.set(null);

        if (review.isMine) {
          this.myReview.set(null);
          this.formOpen.set(false);
        }

        this.toast.success('The review was deleted.');
        this.loadReviews();
      },
      error: error => {
        this.deleting.set(false);
        this.deleteTarget.set(null);
        this.toast.error(error.error?.message ?? 'We could not delete this review.');
      }
    });
  }

  // Eligibility is only known for signed-in customers.
  private loadMine(): void {

    this.canReview.set(false);
    this.myReview.set(null);

    if (!this.authService.isLoggedIn() || this.isAdmin()) {
      return;
    }

    this.reviewService.getMine(this.productId()).subscribe({
      next: status => {
        this.canReview.set(status.canReview);
        this.myReview.set(status.review);
      },
      error: () => this.canReview.set(false)
    });
  }
}
