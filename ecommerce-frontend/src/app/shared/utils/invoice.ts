import { Order, OrderItem } from '../models/order.model';
import { Payment } from '../models/payment.model';
import { paymentSummary } from './payment';

const money = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });

function escapeHtml(value: string | number): string {
  return String(value).replace(/[&<>"']/g, ch => (
    { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch] as string
  ));
}

function unitPrice(item: OrderItem): number {
  return item.unitPrice ?? item.price;
}

function buildInvoiceHtml(order: Order, payment: Payment | null): string {

  const rows = order.items.map(item => `
    <tr>
      <td>${escapeHtml(item.productName)}</td>
      <td class="num">${item.quantity}</td>
      <td class="num">${money.format(unitPrice(item))}</td>
      <td class="num">${money.format(item.totalPrice ?? unitPrice(item) * item.quantity)}</td>
    </tr>`).join('');

  const date = new Date(order.orderDate).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' });

  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Invoice ${escapeHtml(order.orderNumber)}</title>
<style>
  body { font-family: Arial, Helvetica, sans-serif; color: #1a1a1a; margin: 40px; }
  h1 { margin: 0 0 4px; font-size: 24px; }
  .muted { color: #555; font-size: 13px; margin: 2px 0; }
  table { width: 100%; border-collapse: collapse; margin-top: 24px; }
  th, td { padding: 10px 8px; border-bottom: 1px solid #ddd; text-align: left; font-size: 14px; }
  th { background: #f4f4f4; }
  .num { text-align: right; }
  .total td { font-weight: bold; font-size: 16px; border-bottom: none; }
</style>
</head>
<body>
  <h1>Invoice</h1>
  <p class="muted">Order number: ${escapeHtml(order.orderNumber)}</p>
  <p class="muted">Order date: ${escapeHtml(date)}</p>
  <p class="muted">Status: ${escapeHtml(order.status)}</p>
  ${payment ? `<p class="muted">Payment: ${escapeHtml(paymentSummary(payment))}</p>` : ''}
  ${payment?.transactionId ? `<p class="muted">Transaction: ${escapeHtml(payment.transactionId)}</p>` : ''}
  <table>
    <thead>
      <tr><th>Item</th><th class="num">Qty</th><th class="num">Unit price</th><th class="num">Amount</th></tr>
    </thead>
    <tbody>${rows}</tbody>
    <tfoot>
      <tr class="total"><td colspan="3" class="num">Total</td><td class="num">${money.format(order.totalAmount)}</td></tr>
    </tfoot>
  </table>
</body>
</html>`;
}

// Opens the browser print dialog for the invoice (customers can choose "Save as PDF").
export function printInvoice(order: Order, payment: Payment | null = null): void {

  const frame = document.createElement('iframe');

  frame.setAttribute('aria-hidden', 'true');
  frame.style.cssText = 'position:fixed;width:0;height:0;border:0;visibility:hidden';

  const cleanup = () => frame.remove();

  frame.onload = () => {
    const win = frame.contentWindow;

    if (!win) {
      cleanup();
      return;
    }

    win.addEventListener('afterprint', cleanup);
    win.focus();
    win.print();

    // Fallback for browsers that do not fire afterprint.
    setTimeout(cleanup, 60_000);
  };

  frame.srcdoc = buildInvoiceHtml(order, payment);
  document.body.appendChild(frame);
}
