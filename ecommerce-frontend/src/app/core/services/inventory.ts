import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  Inventory,
  AddStock,
  RemoveStock
} from '../../shared/models/inventory.model';

@Injectable({
  providedIn: 'root'
})
export class InventoryService {

  private apiUrl =
    `${environment.apiUrl}/api/inventory`;

  constructor(private http: HttpClient) { }

  getInventory(
    productId: number
  ): Observable<Inventory> {

    return this.http.get<Inventory>(
      `${this.apiUrl}/${productId}`
    );
  }

  addStock(
    productId: number,
    request: AddStock
  ): Observable<void> {

    return this.http.post<void>(
      `${this.apiUrl}/${productId}/add`,
      request
    );
  }

  removeStock(
    productId: number,
    request: RemoveStock
  ): Observable<void> {

    return this.http.post<void>(
      `${this.apiUrl}/${productId}/remove`,
      request
    );
  }
}
