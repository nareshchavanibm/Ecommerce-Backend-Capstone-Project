import { Category } from '../../shared/models/category.model';
import { Order } from '../../shared/models/order.model';
import { buildDonut, buildRevenueBuckets, buildTopProducts, niceCeil } from './dashboard-data';

const order = (id: number, date: string, total: number, status = 'Delivered', items: Order['items'] = []): Order =>
  ({ orderNumber: `ORD-${id}`, userId: 1, orderDate: date, totalAmount: total, status, items });

describe('dashboard data', () => {

  const now = new Date(2026, 9, 15);

  it('buckets monthly revenue, splits delivered, and ignores cancelled orders', () => {

    const buckets = buildRevenueBuckets([
      order(1, '2026-10-02T10:00:00', 1000, 'Delivered'),
      order(2, '2026-10-05T10:00:00', 500, 'Pending'),
      order(3, '2026-10-06T10:00:00', 9999, 'Cancelled'),
      order(4, '2026-09-20T10:00:00', 200, 'Delivered'),
      order(5, '2025-01-01T10:00:00', 777, 'Delivered')
    ], 'monthly', now);

    expect(buckets.length).toBe(8);

    const october = buckets[buckets.length - 1];

    expect(october.label).toBe('Oct');
    expect(october.revenue).toBe(1500);
    expect(october.delivered).toBe(1000);
    expect(october.orders).toBe(2);
    expect(buckets[buckets.length - 2].revenue).toBe(200);
    expect(buckets.reduce((sum, bucket) => sum + bucket.revenue, 0)).toBe(1700);
  });

  it('groups by quarter and year', () => {

    const orders = [order(1, '2026-10-02T10:00:00', 100), order(2, '2026-07-02T10:00:00', 50), order(3, '2025-03-02T10:00:00', 25)];

    const quarters = buildRevenueBuckets(orders, 'quarterly', now);
    const years = buildRevenueBuckets(orders, 'yearly', now);

    expect(quarters.map(bucket => bucket.revenue)).toEqual([0, 0, 50, 100]);
    expect(years.map(bucket => bucket.label)).toEqual(['2024', '2025', '2026']);
    expect(years[2].revenue).toBe(150);
    expect(years[1].revenue).toBe(25);
  });

  it('rounds the axis maximum up to a readable number', () => {

    expect(niceCeil(0)).toBe(1);
    expect(niceCeil(1500)).toBe(2000);
    expect(niceCeil(31000)).toBe(50000);
    expect(niceCeil(80000)).toBe(100000);
  });

  it('aggregates top products from non-cancelled orders', () => {

    const items = (productId: number, quantity: number) =>
      [{ productId, productName: `P${productId}`, price: 10, quantity }];

    const top = buildTopProducts([
      order(1, '2026-10-01', 0, 'Delivered', items(1, 2)),
      order(2, '2026-10-02', 0, 'Pending', items(1, 3)),
      order(3, '2026-10-03', 0, 'Cancelled', items(2, 50))
    ]);

    expect(top.length).toBe(1);
    expect(top[0].units).toBe(5);
    expect(top[0].revenue).toBe(50);
  });

  it('builds a donut with the top four categories and an Other group', () => {

    const categories: Category[] = [10, 8, 6, 4, 2, 1].map((count, i) => ({ id: i, name: `C${i}`, productCount: count }));
    const segments = buildDonut(categories);

    expect(segments.map(segment => segment.name)).toEqual(['C0', 'C1', 'C2', 'C3', 'Other']);
    expect(segments[4].count).toBe(3);
    expect(segments.reduce((sum, segment) => sum + segment.percent, 0)).toBeGreaterThanOrEqual(99);
    expect(buildDonut([{ id: 1, name: 'Empty', productCount: 0 }])).toEqual([]);
  });
});
