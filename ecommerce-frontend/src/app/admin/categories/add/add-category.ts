import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { CategoryService } from '../../../core/services/category';

@Component({
  selector: 'app-add-category',
  imports: [FormsModule],
  templateUrl: './add-category.html',
  styleUrl: './add-category.css'
})
export class AddCategory {

  categoryName = '';

  loading = false;

  errorMessage = '';

  constructor(
    private categoryService: CategoryService,
    private router: Router
  ) { }

  addCategory(): void {

    this.errorMessage = '';

    const name = this.categoryName.trim();

    if (!name) {
      this.errorMessage =
        'Category name is required.';
      return;
    }

    this.loading = true;

    this.categoryService
      .createCategory({ name })
      .subscribe({

        next: () => {

          this.loading = false;

          this.router.navigate([
            '/admin/categories'
          ]);
        },

        error: (error) => {

          console.error(
            'Error creating category:',
            error
          );

          this.loading = false;

          this.errorMessage =
            error.error?.message ||
            'Unable to create category.';
        }

      });
  }

  cancel(): void {

    this.router.navigate([
      '/admin/categories'
    ]);
  }
}
