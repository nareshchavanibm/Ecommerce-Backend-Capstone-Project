import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Address, AddressInput, Profile, UpdateProfile } from '../../shared/models/account.model';

@Injectable({
  providedIn: 'root'
})
export class AccountService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/api/auth`;

  // Number of saved addresses; null until it has been loaded for the signed-in user.
  readonly addressCount = signal<number | null>(null);

  getProfile(): Observable<Profile> {
    return this.http.get<Profile>(`${this.apiUrl}/profile`);
  }

  updateProfile(profile: UpdateProfile): Observable<Profile> {
    return this.http.put<Profile>(`${this.apiUrl}/profile`, profile);
  }

  getAddresses(): Observable<Address[]> {
    return this.http.get<Address[]>(`${this.apiUrl}/addresses`).pipe(
      tap(addresses => this.addressCount.set(addresses.length))
    );
  }

  addAddress(address: AddressInput): Observable<Address> {
    return this.http.post<Address>(`${this.apiUrl}/addresses`, address);
  }

  updateAddress(id: number, address: AddressInput): Observable<Address> {
    return this.http.put<Address>(`${this.apiUrl}/addresses/${id}`, address);
  }

  setDefaultAddress(id: number): Observable<Address> {
    return this.http.put<Address>(`${this.apiUrl}/addresses/${id}/default`, {});
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/addresses/${id}`);
  }
}
