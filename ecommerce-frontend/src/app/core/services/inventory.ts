import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  Inventory,
  InventoryUpdate
} from '../../shared/models/inventory.model';

@Injectable({
  providedIn: 'root'
})
export class InventoryService {

  private apiUrl =
    `${environment.apiUrl}/api/inventory`;

  constructor(private http: HttpClient) { }

  getAllInventory(): Observable<Inventory[]> {

    return this.http.get<Inventory[]>(
      this.apiUrl
    );
  }

  getInventory(
    productId: number
  ): Observable<Inventory> {

    return this.http.get<Inventory>(
      `${this.apiUrl}/${productId}`
    );
  }

  // First stock record for a product.
  createInventory(
    productId: number,
    request: InventoryUpdate
  ): Observable<Inventory> {

    return this.http.post<Inventory>(
      `${this.apiUrl}/${productId}`,
      request
    );
  }

  // Sets the stock and reorder level of an existing record.
  updateInventory(
    productId: number,
    request: InventoryUpdate
  ): Observable<Inventory> {

    return this.http.put<Inventory>(
      `${this.apiUrl}/${productId}`,
      request
    );
  }
}
