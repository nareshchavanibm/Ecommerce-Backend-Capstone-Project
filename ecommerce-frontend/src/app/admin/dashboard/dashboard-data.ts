import { Category } from '../../shared/models/category.model';
import { Order } from '../../shared/models/order.model';

export type RevenueRange = 'monthly' | 'quarterly' | 'yearly';

export interface RevenueBucket {
  label: string;
  // Revenue of every order that is not cancelled.
  revenue: number;
  // The part of that revenue from delivered orders.
  delivered: number;
  orders: number;
}

export interface TopProduct {
  productId: number;
  name: string;
  units: number;
  revenue: number;
  price: number;
}

export interface DonutSegment {
  name: string;
  count: number;
  percent: number;
  color: string;
  dash: string;
  offset: number;
}

const BUCKET_COUNT: Record<RevenueRange, number> = { monthly: 8, quarterly: 4, yearly: 3 };

export const DONUT_COLORS = ['#064b35', '#3fa77d', '#f5a524', '#6b7bf2', '#e5739b'];

export const DONUT_CIRCUMFERENCE = 2 * Math.PI * 70;

export function isCancelled(order: Order): boolean {
  return order.status === 'Cancelled';
}

function periodIndex(date: Date, range: RevenueRange): number {

  switch (range) {
    case 'monthly': return date.getFullYear() * 12 + date.getMonth();
    case 'quarterly': return date.getFullYear() * 4 + Math.floor(date.getMonth() / 3);
    default: return date.getFullYear();
  }
}

function periodLabel(index: number, range: RevenueRange): string {

  switch (range) {
    case 'monthly':
      return new Date(Math.floor(index / 12), index % 12, 1).toLocaleString('en', { month: 'short' });
    case 'quarterly':
      return `Q${(index % 4) + 1} '${String(Math.floor(index / 4)).slice(-2)}`;
    default:
      return String(index);
  }
}

export function buildRevenueBuckets(orders: Order[], range: RevenueRange, now = new Date()): RevenueBucket[] {

  const last = periodIndex(now, range);
  const first = last - BUCKET_COUNT[range] + 1;
  const buckets = new Map<number, RevenueBucket>();

  for (let index = first; index <= last; index++) {
    buckets.set(index, { label: periodLabel(index, range), revenue: 0, delivered: 0, orders: 0 });
  }

  for (const order of orders) {

    const date = new Date(order.orderDate);
    const bucket = Number.isNaN(date.getTime()) || isCancelled(order)
      ? undefined
      : buckets.get(periodIndex(date, range));

    if (bucket) {
      bucket.revenue += order.totalAmount;
      bucket.orders += 1;

      if (order.status === 'Delivered') {
        bucket.delivered += order.totalAmount;
      }
    }
  }

  return [...buckets.values()];
}

// Rounds up to 1, 2, 5 or 10 times a power of ten so the axis ticks are readable.
export function niceCeil(value: number): number {

  if (value <= 0) {
    return 1;
  }

  const power = Math.pow(10, Math.floor(Math.log10(value)));
  const fraction = value / power;
  const step = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;

  return step * power;
}

export function buildTopProducts(orders: Order[]): TopProduct[] {

  const products = new Map<number, TopProduct>();

  for (const order of orders) {

    if (isCancelled(order)) {
      continue;
    }

    for (const item of order.items ?? []) {

      const price = item.unitPrice ?? item.price;
      const entry = products.get(item.productId)
        ?? { productId: item.productId, name: item.productName, units: 0, revenue: 0, price };

      entry.units += item.quantity;
      entry.revenue += item.totalPrice ?? price * item.quantity;
      entry.price = price;

      products.set(item.productId, entry);
    }
  }

  return [...products.values()];
}

// Largest four categories by product count; everything else is grouped as "Other".
export function buildDonut(categories: Category[]): DonutSegment[] {

  const sorted = categories
    .filter(category => category.productCount > 0)
    .sort((a, b) => b.productCount - a.productCount);

  const top = sorted.slice(0, 4).map(category => ({ name: category.name, count: category.productCount }));
  const otherCount = sorted.slice(4).reduce((sum, category) => sum + category.productCount, 0);

  if (otherCount > 0) {
    top.push({ name: 'Other', count: otherCount });
  }

  const total = top.reduce((sum, entry) => sum + entry.count, 0);
  const gap = top.length > 1 ? 2 : 0;
  let consumed = 0;

  return top.map((entry, index) => {

    const length = (entry.count / total) * DONUT_CIRCUMFERENCE;
    const segment: DonutSegment = {
      name: entry.name,
      count: entry.count,
      percent: Math.round((entry.count / total) * 100),
      color: DONUT_COLORS[index % DONUT_COLORS.length],
      dash: `${Math.max(length - gap, 0)} ${DONUT_CIRCUMFERENCE}`,
      offset: -consumed
    };

    consumed += length;

    return segment;
  });
}
