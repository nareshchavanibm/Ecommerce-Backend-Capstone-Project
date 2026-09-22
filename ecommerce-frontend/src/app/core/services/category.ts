import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  Category,
  CreateCategory,
  UpdateCategory
} from '../../shared/models/category.model';

@Injectable({
  providedIn: 'root'
})
export class CategoryService {

  private apiUrl = `${environment.apiUrl}/api/categories`;

  constructor(private http: HttpClient) { }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(this.apiUrl);
  }

  getCategoryById(id: number): Observable<Category> {
    return this.http.get<Category>(
      `${this.apiUrl}/${id}`
    );
  }

  createCategory(
    category: CreateCategory
  ): Observable<Category> {

    return this.http.post<Category>(
      this.apiUrl,
      category
    );
  }

  updateCategory(
    id: number,
    category: UpdateCategory
  ): Observable<void> {

    return this.http.put<void>(
      `${this.apiUrl}/${id}`,
      category
    );
  }

  deleteCategory(id: number): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
}
