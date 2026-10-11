import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth';
import { CategoryService } from '../../core/services/category';
import { Category } from '../models/category.model';

const FOOTER_CATEGORY_LIMIT = 5;

@Component({
  selector: 'app-footer',
  imports: [RouterLink],
  templateUrl: './footer.html',
  styleUrl: './footer.css'
})
export class Footer implements OnInit {

  protected readonly authService = inject(AuthService);

  private readonly categoryService = inject(CategoryService);

  protected readonly categories = signal<Category[]>([]);

  protected readonly year = new Date().getFullYear();

  ngOnInit(): void {

    this.categoryService.getCategories().subscribe({
      next: categories => this.categories.set(categories.slice(0, FOOTER_CATEGORY_LIMIT)),
      error: () => { }
    });
  }
}
