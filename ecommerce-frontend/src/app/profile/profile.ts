import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';

import { AuthService } from '../auth/auth';
import { AccountService } from '../core/services/account';
import { AddressSuggestion } from '../core/services/google-maps';
import { Address, AddressInput, Profile as UserProfile } from '../shared/models/account.model';
import { SITE_INFO } from '../shared/site-info';
import { Alert } from '../shared/ui/alert/alert';
import { Badge } from '../shared/ui/badge/badge';
import { Button } from '../shared/ui/button/button';
import { Checkbox } from '../shared/ui/checkbox/checkbox';
import { Dialog } from '../shared/ui/dialog/dialog';
import { LocationPicker } from '../shared/ui/location-picker/location-picker';
import { TextInput } from '../shared/ui/text-input/text-input';
import { ToastService } from '../shared/ui/toast/toast.service';

const PHONE_PATTERN = /^[0-9]{10}$/;
const POSTAL_PATTERN = /^[A-Za-z0-9\- ]{3,10}$/;

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, Alert, Badge, Button, Checkbox, Dialog, LocationPicker, TextInput],
  templateUrl: './profile.html',
  styleUrl: './profile.css'
})
export class Profile {

  private readonly authService = inject(AuthService);

  private readonly accountService = inject(AccountService);

  private readonly router = inject(Router);

  private readonly route = inject(ActivatedRoute);

  private readonly toast = inject(ToastService);

  protected readonly isAdmin = this.authService.getRole() === 'Admin';

  protected readonly roleLabel = this.isAdmin ? 'Administrator' : 'Customer';

  protected readonly profile = signal<UserProfile | null>(null);

  protected readonly addresses = signal<Address[]>([]);

  // Set when another page (checkout) sent the user here to add an address.
  private returnTo: string | null = null;

  protected readonly loading = signal(true);

  protected readonly loadError = signal('');

  protected readonly email = computed(() => this.profile()?.email ?? this.authService.getEmail() ?? '');

  protected readonly displayName = computed(() => {

    const profile = this.profile();
    const name = profile ? `${profile.firstName} ${profile.lastName}`.trim() : '';

    return name || this.email();
  });

  protected readonly initial = computed(() => (this.displayName().charAt(0) || '?').toUpperCase());

  // Personal details
  protected readonly editingDetails = signal(false);

  protected readonly savingDetails = signal(false);

  protected readonly detailsError = signal('');

  protected readonly detailsForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] })
  });

  // Address book
  protected readonly addressDialogOpen = signal(false);

  protected readonly editingAddress = signal<Address | null>(null);

  protected readonly savingAddress = signal(false);

  protected readonly addressError = signal('');

  protected readonly busyAddressId = signal<number | null>(null);

  protected readonly deleteDialogOpen = signal(false);

  protected readonly deleteTarget = signal<Address | null>(null);

  protected readonly addressForm = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(PHONE_PATTERN)] }),
    addressLine1: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    addressLine2: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    state: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    postalCode: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(POSTAL_PATTERN)] }),
    country: new FormControl('India', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    isDefault: new FormControl(false, { nonNullable: true })
  });

  protected readonly messages = {
    fullName: { required: 'Enter the recipient name.', maxlength: 'Use 100 characters or fewer.' },
    phone: { required: 'Enter a phone number.', pattern: 'Enter a 10-digit phone number.' },
    line1: { required: 'Enter the street address.', maxlength: 'Use 200 characters or fewer.' },
    line2: { maxlength: 'Use 200 characters or fewer.' },
    city: { required: 'Enter the city.', maxlength: 'Use 100 characters or fewer.' },
    state: { required: 'Enter the state.', maxlength: 'Use 100 characters or fewer.' },
    postalCode: { required: 'Enter the postal code.', pattern: 'Enter a valid postal code.' },
    country: { required: 'Enter the country.', maxlength: 'Use 100 characters or fewer.' },
    firstName: { required: 'Enter your first name.', maxlength: 'Use 100 characters or fewer.' },
    lastName: { required: 'Enter your last name.', maxlength: 'Use 100 characters or fewer.' }
  };

  constructor() {

    inject(Title).setTitle(`My account | ${SITE_INFO.brand}`);

    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.loadError.set('');

    forkJoin({
      profile: this.accountService.getProfile(),
      addresses: this.isAdmin ? of<Address[]>([]) : this.accountService.getAddresses()
    }).subscribe({
      next: ({ profile, addresses }) => {
        this.profile.set(profile);
        this.addresses.set(addresses);
        this.loading.set(false);
        this.openAddressFromLink();
      },
      error: (error) => {
        this.loadError.set(this.messageFrom(error, 'We could not load your account details.'));
        this.loading.set(false);
      }
    });
  }

  // Personal details

  protected startEditingDetails(): void {

    const profile = this.profile();

    this.detailsForm.reset({
      firstName: profile?.firstName ?? '',
      lastName: profile?.lastName ?? ''
    });

    this.detailsError.set('');
    this.editingDetails.set(true);
  }

  protected cancelEditingDetails(): void {
    this.editingDetails.set(false);
  }

  protected saveDetails(): void {

    if (this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    this.detailsError.set('');
    this.savingDetails.set(true);

    this.accountService.updateProfile(this.detailsForm.getRawValue()).subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.savingDetails.set(false);
        this.editingDetails.set(false);
        this.toast.success('Your details were updated.');
      },
      error: (error) => {
        this.savingDetails.set(false);
        this.detailsError.set(this.messageFrom(error, 'We could not update your details.'));
      }
    });
  }

  // Addresses

  protected openAddressDialog(address: Address | null): void {

    this.editingAddress.set(address);
    this.addressError.set('');

    const profile = this.profile();
    const ownName = profile ? `${profile.firstName} ${profile.lastName}`.trim() : '';

    this.addressForm.reset({
      fullName: address?.fullName ?? ownName,
      phone: address?.phone ?? '',
      addressLine1: address?.addressLine1 ?? '',
      addressLine2: address?.addressLine2 ?? '',
      city: address?.city ?? '',
      state: address?.state ?? '',
      postalCode: address?.postalCode ?? '',
      country: address?.country ?? 'India',
      isDefault: address?.isDefault ?? this.addresses().length === 0
    });

    this.addressDialogOpen.set(true);
  }

  protected saveAddress(): void {

    if (this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }

    const value = this.addressForm.getRawValue();
    const input: AddressInput = { ...value, addressLine2: value.addressLine2.trim() || null };
    const editing = this.editingAddress();

    this.addressError.set('');
    this.savingAddress.set(true);

    const request = editing
      ? this.accountService.updateAddress(editing.id, input)
      : this.accountService.addAddress(input);

    request.subscribe({
      next: () => {
        this.savingAddress.set(false);
        this.addressDialogOpen.set(false);
        this.toast.success(editing ? 'Address updated.' : 'Address saved.');
        this.reloadAddresses();

        if (!editing && this.returnTo) {
          this.router.navigateByUrl(this.returnTo);
        }
      },
      error: (error) => {
        this.savingAddress.set(false);
        this.addressError.set(this.messageFrom(error, 'We could not save this address.'));
      }
    });
  }

  protected makeDefault(address: Address): void {

    this.busyAddressId.set(address.id);

    this.accountService.setDefaultAddress(address.id).subscribe({
      next: () => {
        this.busyAddressId.set(null);
        this.reloadAddresses();
      },
      error: (error) => {
        this.busyAddressId.set(null);
        this.toast.error(this.messageFrom(error, 'We could not change the default address.'));
      }
    });
  }

  protected askDelete(address: Address): void {
    this.deleteTarget.set(address);
    this.deleteDialogOpen.set(true);
  }

  protected confirmDelete(): void {

    const target = this.deleteTarget();

    if (!target) {
      return;
    }

    this.busyAddressId.set(target.id);

    this.accountService.deleteAddress(target.id).subscribe({
      next: () => {
        this.busyAddressId.set(null);
        this.deleteDialogOpen.set(false);
        this.toast.success('Address removed.');
        this.reloadAddresses();
      },
      error: (error) => {
        this.busyAddressId.set(null);
        this.deleteDialogOpen.set(false);
        this.toast.error(this.messageFrom(error, 'We could not remove this address.'));
      }
    });
  }

  // Fills the form from a Google location result; name and phone are left for the user.
  protected applyLocation(address: AddressSuggestion): void {
    this.addressForm.patchValue(address);
    this.addressForm.markAsUntouched();
  }

  protected logout(): void {
    this.authService.logout();
    this.router.navigate(['/home']);
  }

  private reloadAddresses(): void {
    this.accountService.getAddresses().subscribe({
      next: addresses => this.addresses.set(addresses),
      error: (error) => this.toast.error(this.messageFrom(error, 'We could not refresh your addresses.'))
    });
  }

  // Links here with ?addAddress (and optionally ?returnTo=/path) open the form straight away.
  private openAddressFromLink(): void {

    const params = this.route.snapshot.queryParamMap;

    if (!params.has('addAddress')) {
      return;
    }

    const returnTo = params.get('returnTo');

    // Only in-app paths, never another site.
    this.returnTo = returnTo && returnTo.startsWith('/') && !returnTo.startsWith('//') ? returnTo : null;

    this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });

    if (!this.isAdmin) {
      this.openAddressDialog(null);
    }
  }

  // Server messages: { message } or ASP.NET validation { errors: { field: [..] } }.
  private messageFrom(error: any, fallback: string): string {

    const body = error?.error;
    const firstValidation = body?.errors ? (Object.values(body.errors).flat()[0] as string | undefined) : undefined;

    return body?.message || firstValidation || fallback;
  }
}
