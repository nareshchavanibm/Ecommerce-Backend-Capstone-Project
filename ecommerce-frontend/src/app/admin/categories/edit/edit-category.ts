import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import { FormsModule } from '@angular/forms';

import {
  ActivatedRoute,
  Router
} from '@angular/router';

import { CategoryService } from '../../../core/services/category';

import { Category } from '../../../shared/models/category.model';

@Component({
  selector: 'app-edit-category',
  imports: [FormsModule],
  templateUrl: './edit-category.html',
  styleUrl: './edit-category.css'
})
export class EditCategory implements OnInit {

  categoryId = 0;

  category: Category = {
    id: 0,
    name: '',
    productCount: 0
  };

  loading = false;

  loadingCategory = true;

  errorMessage = '';

  constructor(
    private categoryService: CategoryService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {

    const id = Number(
      this.route.snapshot.paramMap.get('id')
    );

    if (!id || id <= 0) {

      this.errorMessage =
        'Invalid category ID.';

      this.loadingCategory = false;

      return;
    }

    this.categoryId = id;

    this.loadCategory();
  }

  loadCategory(): void {

    this.categoryService
      .getCategoryById(this.categoryId)
      .subscribe({

        next: (data) => {

          this.category = data;

          this.loadingCategory = false;

          this.cdr.detectChanges();
        },

        error: (error) => {

          console.error(
            'Error loading category:',
            error
          );

          this.errorMessage =
            error.error?.message ||
            'Unable to load category.';

          this.loadingCategory = false;

          this.cdr.detectChanges();
        }

      });
  }

  updateCategory(): void {

    this.errorMessage = '';

    const name = this.category.name.trim();

    if (!name) {

      this.errorMessage =
        'Category name is required.';

      return;
    }

    this.loading = true;

    this.categoryService
      .updateCategory(
        this.categoryId,
        { name }
      )
      .subscribe({

        next: () => {

          this.loading = false;

          this.router.navigate([
            '/admin/categories'
          ]);
        },

        error: (error) => {

          console.error(
            'Error updating category:',
            error
          );

          this.loading = false;

          this.errorMessage =
            error.error?.message ||
            'Unable to update category.';
        }

      });
  }

  cancel(): void {

    this.router.navigate([
      '/admin/categories'
    ]);
  }
}
