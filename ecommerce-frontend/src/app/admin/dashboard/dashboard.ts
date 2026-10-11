import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Observable, catchError, debounceTime, forkJoin, of } from 'rxjs';

import { CategoryService } from '../../core/services/category';
import { InventoryService } from '../../core/services/inventory';
import { NotificationService } from '../../core/services/notifications';
import { OrderService } from '../../core/services/order';
import { ProductService } from '../../core/services/product';
import { Category } from '../../shared/models/category.model';
import { Inventory } from '../../shared/models/inventory.model';
import { Order } from '../../shared/models/order.model';
import { Product } from '../../shared/models/product.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { productImageUrl } from '../../shared/utils/product-image';
import {
  RevenueRange,
  buildDonut,
  buildRevenueBuckets,
  buildTopProducts,
  isCancelled,
  niceCeil
} from './dashboard-data';

type BadgeVariant = 'neutral' | 'success' | 'warning' | 'danger';

interface ActivityItem {
  key: string;
  title: string;
  detail: string;
  date: string | null;
  badge: string;
  variant: BadgeVariant;
  link: string;
}

const RANGES: { value: RevenueRange; label: string }[] = [
  { value: 'monthly', label: 'Monthly' },
  { value: 'quarterly', label: 'Quarterly' },
  { value: 'yearly', label: 'Yearly' }
];

const compact = new Intl.NumberFormat('en-IN', { notation: 'compact', maximumFractionDigits: 1 });

@Component({
  selector: 'app-admin-dashboard',
  imports: [CurrencyPipe, DatePipe, RouterLink, Alert, Badge, Button, Skeleton],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class AdminDashboard {

  protected readonly ranges = RANGES;

  protected readonly loading = signal(true);

  protected readonly failedSources = signal<string[]>([]);

  protected readonly lastUpdated = signal<Date | null>(null);

  private readonly orders = signal<Order[]>([]);

  private readonly categories = signal<Category[]>([]);

  private readonly products = signal<Product[]>([]);

  private readonly inventory = signal<Inventory[]>([]);

  protected readonly totalProducts = signal(0);

  protected readonly range = signal<RevenueRange>('monthly');

  protected readonly activeBar = signal<number | null>(null);

  protected readonly sortKey = signal<'revenue' | 'units'>('revenue');

  // KPI cards

  protected readonly totalOrders = computed(() => this.orders().length);

  protected readonly revenue = computed(() =>
    this.orders().filter(order => !isCancelled(order)).reduce((sum, order) => sum + order.totalAmount, 0)
  );

  protected readonly pendingOrders = computed(() => this.orders().filter(order => order.status === 'Pending').length);

  // Revenue chart

  private readonly buckets = computed(() => buildRevenueBuckets(this.orders(), this.range()));

  private readonly niceMax = computed(() => niceCeil(Math.max(...this.buckets().map(bucket => bucket.revenue), 0)));

  protected readonly ticks = computed(() =>
    [1, 0.75, 0.5, 0.25, 0].map(fraction => compact.format(this.niceMax() * fraction))
  );

  protected readonly bars = computed(() =>
    this.buckets().map(bucket => ({
      ...bucket,
      height: (bucket.revenue / this.niceMax()) * 100,
      fill: bucket.revenue > 0 ? (bucket.delivered / bucket.revenue) * 100 : 0
    }))
  );

  protected readonly hasRevenue = computed(() => this.buckets().some(bucket => bucket.revenue > 0));

  // Categories donut

  protected readonly donut = computed(() => buildDonut(this.categories()));

  // Recent activity: stock alerts first, then the latest orders.

  protected readonly activity = computed<ActivityItem[]>(() => {

    const alerts = this.inventory()
      .filter(item => item.isLowStock)
      .slice(0, 2)
      .map<ActivityItem>(item => ({
        key: `stock-${item.productId}`,
        title: 'Low stock alert',
        detail: `${item.productName} - ${item.stockQuantity} left`,
        date: null,
        badge: 'Low stock',
        variant: 'danger',
        link: '/admin/inventory'
      }));

    const recent = [...this.orders()]
      .sort((a, b) => new Date(b.orderDate).getTime() - new Date(a.orderDate).getTime())
      .slice(0, 4)
      .map<ActivityItem>(order => ({
        key: `order-${order.orderNumber}`,
        title: `Order ${order.orderNumber}`,
        detail: `Customer #${order.userId}`,
        date: order.orderDate,
        badge: order.status,
        variant: this.variantFor(order.status),
        link: `/admin/orders/details/${order.orderNumber}`
      }));

    return [...alerts, ...recent];
  });

  // Top products

  protected readonly topProducts = computed(() => {

    const catalog = new Map(this.products().map(product => [product.id, product]));
    const stock = new Map(this.inventory().map(item => [item.productId, item.stockQuantity]));
    const key = this.sortKey();

    return buildTopProducts(this.orders())
      .sort((a, b) => b[key] - a[key])
      .slice(0, 5)
      .map(entry => {

        const product = catalog.get(entry.productId);

        return {
          ...entry,
          price: product?.price ?? entry.price,
          stock: stock.get(entry.productId) ?? null,
          image: product ? productImageUrl(product, 80) : ''
        };
      });
  });

  constructor(
    private orderService: OrderService,
    private categoryService: CategoryService,
    private productService: ProductService,
    private inventoryService: InventoryService
  ) {
    this.load();

    // New, cancelled and paid orders refresh the figures without a loading flash.
    inject(NotificationService).ordersChanged
      .pipe(debounceTime(500), takeUntilDestroyed())
      .subscribe(() => this.load(true));
  }

  protected load(silent = false): void {

    const failed: string[] = [];

    if (!silent) {
      this.loading.set(true);
    }

    forkJoin({
      orders: this.safe('orders', this.orderService.getAllOrders(), failed),
      categories: this.safe('categories', this.categoryService.getCategories(), failed),
      products: this.safe('products', this.productService.searchProducts({ pageSize: 100 }), failed),
      inventory: this.safe('inventory', this.inventoryService.getAllInventory(), failed)
    }).subscribe(({ orders, categories, products, inventory }) => {

      const items = products?.items ?? [];

      this.orders.set(orders ?? []);
      this.categories.set(categories ?? []);
      this.products.set(items);
      this.totalProducts.set(products?.totalItems ?? products?.totalCount ?? items.length);
      this.inventory.set(inventory ?? []);
      this.failedSources.set(failed);
      this.lastUpdated.set(new Date());
      this.loading.set(false);
    });
  }

  protected setRange(range: RevenueRange): void {
    this.range.set(range);
    this.activeBar.set(null);
  }

  protected hideImage(event: Event): void {
    (event.target as HTMLElement).style.display = 'none';
  }

  protected sortAria(key: 'revenue' | 'units'): 'descending' | 'none' {
    return this.sortKey() === key ? 'descending' : 'none';
  }

  private variantFor(status: string): BadgeVariant {

    switch (status) {
      case 'Pending': return 'warning';
      case 'Delivered': return 'success';
      case 'Cancelled': return 'danger';
      default: return 'neutral';
    }
  }

  // One failing source must not blank the whole dashboard.
  private safe<T>(label: string, source: Observable<T>, failed: string[]): Observable<T | null> {

    return source.pipe(
      catchError(() => {
        failed.push(label);
        return of(null);
      })
    );
  }
}
