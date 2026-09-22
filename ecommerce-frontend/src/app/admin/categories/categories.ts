import {
  Component,
  OnInit,
  computed,
  signal
} from '@angular/core';
import { Router } from '@angular/router';

import { CategoryService } from '../../core/services/category';
import { Category } from '../../shared/models/category.model';

@Component({
  selector: 'app-categories',
  templateUrl: './categories.html',
  styleUrl: './categories.css'
})
export class Categories implements OnInit {

  categories = signal<Category[]>([]);

  readonly pageSize = 10;
  currentPage = signal(1);

  totalPages = computed(() => {
    const count = this.categories().length;
    return count === 0
      ? 1
      : Math.ceil(count / this.pageSize);
  });

  pagedCategories = computed(() => {
    const start =
      (this.currentPage() - 1) * this.pageSize;

    return this.categories().slice(
      start,
      start + this.pageSize
    );
  });

  loading = signal(true);

  errorMessage = signal('');

  constructor(
    private categoryService: CategoryService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.categoryService.getCategories().subscribe({

      next: (data) => {

        this.categories.set(data);
        this.currentPage.set(1);

        this.loading.set(false);
      },

      error: (error) => {

        console.error(
          'Error loading categories:',
          error
        );

        this.categories.set([]);

        this.errorMessage.set(
          error.error?.message ||
          'Unable to load categories.'
        );

        this.loading.set(false);
      }

    });
  }

  addCategory(): void {

    this.router.navigate([
      '/admin/categories/add'
    ]);
  }

  editCategory(id: number): void {

    this.router.navigate([
      '/admin/categories/edit',
      id
    ]);
  }

  deleteCategory(category: Category): void {

    const confirmed = confirm(
      `Are you sure you want to delete "${category.name}"?`
    );

    if (!confirmed) {
      return;
    }

    this.categoryService
      .deleteCategory(category.id)
      .subscribe({

        next: () => {

          this.categories.update(
            categories =>
              categories.filter(
                c => c.id !== category.id
              )
          );

          this.ensureValidCurrentPage();
        },

        error: (error) => {

          console.error(
            'Error deleting category:',
            error
          );

          this.errorMessage.set(
            error.error?.message ||
            'Unable to delete category.'
          );
        }

      });
  }

  goToPreviousPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(page => page - 1);
    }
  }

  goToNextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(page => page + 1);
    }
  }

  private ensureValidCurrentPage(): void {
    const totalPages = this.totalPages();

    if (this.currentPage() > totalPages) {
      this.currentPage.set(totalPages);
    }
  }
}
