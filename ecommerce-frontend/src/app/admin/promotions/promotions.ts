import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';

import { PromotionService } from '../../core/services/promotion';
import { Promotion, PromotionInput, PromotionType } from '../../shared/models/promotion.model';
import { Button } from '../../shared/ui/button/button';
import { Checkbox } from '../../shared/ui/checkbox/checkbox';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Select, SelectOption } from '../../shared/ui/select/select';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { ToastService } from '../../shared/ui/toast/toast.service';

type Status = 'Live' | 'Scheduled' | 'Expired' | 'Hidden';

const HTTP_URL = /^https?:\/\/\S+$/i;

// Same-site path (not protocol-relative) or an http(s) URL; matches the server rule.
const LINK = /^(\/(?!\/)[^\\\s]*|https?:\/\/\S+)$/i;

// <input type="datetime-local"> works in local time; the API stores UTC.
function toLocalInput(iso: string | null): string {

  if (!iso) {
    return '';
  }

  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function fromLocalInput(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

function endAfterStart(group: AbstractControl): ValidationErrors | null {

  const start = group.get('startsAt')?.value;
  const end = group.get('endsAt')?.value;

  return start && end && new Date(end) <= new Date(start) ? { dateRange: true } : null;
}

@Component({
  selector: 'app-admin-promotions',
  imports: [DatePipe, ReactiveFormsModule, Button, Checkbox, Dialog, Select, TextInput],
  templateUrl: './promotions.html',
  styleUrl: './promotions.css'
})
export class AdminPromotions implements OnInit {

  private readonly promotionService = inject(PromotionService);

  private readonly toast = inject(ToastService);

  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly promotions = signal<Promotion[]>([]);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly formOpen = signal(false);

  protected readonly saving = signal(false);

  protected readonly formError = signal('');

  protected readonly editing = signal<Promotion | null>(null);

  protected readonly deleteTarget = signal<Promotion | null>(null);

  protected readonly deleteOpen = signal(false);

  protected readonly deleting = signal(false);

  protected readonly typeOptions: SelectOption[] = [
    { value: 'Banner', label: 'Banner (home carousel)' },
    { value: 'Offer', label: 'Offer (discount / promo code)' },
    { value: 'Ad', label: 'Ad (sponsored card)' }
  ];

  protected readonly form = this.fb.group({
    type: ['Banner' as string, Validators.required],
    title: ['', [Validators.required, Validators.maxLength(120)]],
    subtitle: ['', Validators.maxLength(300)],
    imageUrl: ['', [Validators.maxLength(500), Validators.pattern(HTTP_URL)]],
    linkUrl: ['', [Validators.maxLength(500), Validators.pattern(LINK)]],
    discountText: ['', Validators.maxLength(50)],
    promoCode: ['', Validators.maxLength(50)],
    startsAt: [''],
    endsAt: [''],
    displayOrder: [0, [Validators.required, Validators.min(0), Validators.max(9999)]],
    isActive: [true]
  }, { validators: endAfterStart });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.promotionService.getAll().subscribe({
      next: promotions => {
        this.promotions.set(promotions);
        this.loading.set(false);
      },
      error: error => {
        this.errorMessage.set(error.error?.message ?? 'Unable to load promotions.');
        this.loading.set(false);
      }
    });
  }

  protected status(promotion: Promotion): Status {

    const now = Date.now();

    if (!promotion.isActive) {
      return 'Hidden';
    }

    if (promotion.startsAt && new Date(promotion.startsAt).getTime() > now) {
      return 'Scheduled';
    }

    if (promotion.endsAt && new Date(promotion.endsAt).getTime() < now) {
      return 'Expired';
    }

    return 'Live';
  }

  protected openCreate(): void {

    this.editing.set(null);
    this.formError.set('');
    this.form.reset({
      type: 'Banner', title: '', subtitle: '', imageUrl: '', linkUrl: '', discountText: '',
      promoCode: '', startsAt: '', endsAt: '', displayOrder: this.promotions().length, isActive: true
    });
    this.formOpen.set(true);
  }

  protected openEdit(promotion: Promotion): void {

    this.editing.set(promotion);
    this.formError.set('');
    this.form.reset({
      type: promotion.type,
      title: promotion.title,
      subtitle: promotion.subtitle,
      imageUrl: promotion.imageUrl,
      linkUrl: promotion.linkUrl,
      discountText: promotion.discountText,
      promoCode: promotion.promoCode,
      startsAt: toLocalInput(promotion.startsAt),
      endsAt: toLocalInput(promotion.endsAt),
      displayOrder: promotion.displayOrder,
      isActive: promotion.isActive
    });
    this.formOpen.set(true);
  }

  protected save(): void {

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    const body: PromotionInput = {
      type: value.type as PromotionType,
      title: value.title.trim(),
      subtitle: value.subtitle.trim(),
      imageUrl: value.imageUrl.trim(),
      linkUrl: value.linkUrl.trim(),
      discountText: value.discountText.trim(),
      promoCode: value.promoCode.trim(),
      startsAt: fromLocalInput(value.startsAt),
      endsAt: fromLocalInput(value.endsAt),
      isActive: value.isActive,
      displayOrder: Number(value.displayOrder)
    };

    const existing = this.editing();

    this.saving.set(true);
    this.formError.set('');

    const request = existing
      ? this.promotionService.update(existing.id, body)
      : this.promotionService.create(body);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.toast.success(existing ? 'Promotion updated.' : 'Promotion created.');
        this.load();
      },
      error: error => {
        this.saving.set(false);
        this.formError.set(this.messageFrom(error, 'Unable to save the promotion.'));
      }
    });
  }

  protected askDelete(promotion: Promotion): void {
    this.deleteTarget.set(promotion);
    this.deleteOpen.set(true);
  }

  protected confirmDelete(): void {

    const target = this.deleteTarget();

    if (!target || this.deleting()) {
      return;
    }

    this.deleting.set(true);

    this.promotionService.delete(target.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteOpen.set(false);
        this.promotions.update(list => list.filter(p => p.id !== target.id));
        this.toast.success('Promotion deleted.');
      },
      error: error => {
        this.deleting.set(false);
        this.deleteOpen.set(false);
        this.toast.error(this.messageFrom(error, 'Unable to delete the promotion.'));
      }
    });
  }

  // Server validation failures arrive as { errors: { Field: [messages] } }.
  private messageFrom(error: { error?: { message?: string; errors?: Record<string, string[]> } }, fallback: string): string {

    const first = error.error?.errors ? Object.values(error.error.errors).flat()[0] : undefined;

    return error.error?.message ?? first ?? fallback;
  }
}
