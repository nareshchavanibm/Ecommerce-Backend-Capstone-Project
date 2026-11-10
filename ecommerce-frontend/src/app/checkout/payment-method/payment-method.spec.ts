import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { PaymentSelection } from '../../core/services/payment-selection';
import { PaymentMethodPage } from './payment-method';

@Component({ template: '' })
class Blank { }

describe('PaymentMethodPage', () => {

  let harness: RouterTestingHarness;
  let component: PaymentMethodPage;

  const el = (): HTMLElement => harness.routeNativeElement!;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const open = async (): Promise<void> => {
    localStorage.setItem('token', 't');

    TestBed.configureTestingModule({
      providers: [provideRouter([
        { path: 'checkout/payment', component: PaymentMethodPage },
        { path: '**', component: Blank }
      ])]
    });

    harness = await RouterTestingHarness.create();
    component = await harness.navigateByUrl('/checkout/payment', PaymentMethodPage);
    await settle();
  };

  const radio = (label: string): HTMLInputElement =>
    Array.from(el().querySelectorAll<HTMLLabelElement>('label'))
      .find(item => item.querySelector('input[type="radio"]')
        && (item.querySelector('.option__label, .upi__name') ?? item.querySelector('span'))?.textContent?.trim() === label)!
      .querySelector<HTMLInputElement>('input')!;

  const choose = async (label: string): Promise<void> => {
    radio(label).click();
    await settle();
  };

  const type = async (id: string, value: string): Promise<void> => {
    const input = el().querySelector<HTMLInputElement>(`#${id}`)!;

    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle();
  };

  const submit = async (): Promise<void> => {
    el().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();
  };

  afterEach(() => localStorage.clear());

  it('shows the card form only once Card is chosen', async () => {

    await open();

    expect(el().querySelector('#card-number')).toBeNull();

    await choose('Card');

    expect(el().querySelector('#card-number')).not.toBeNull();
    expect(el().querySelector('.preview')).not.toBeNull();
  });

  it('requires a method before continuing', async () => {

    await open();
    await submit();

    expect(el().textContent).toContain('Choose a payment method to continue.');
    expect(TestBed.inject(Router).url).toBe('/checkout/payment');
  });

  it('formats the card number, updates the preview and detects the brand', async () => {

    await open();
    await choose('Card');
    await type('card-number', '4242424242424242');

    expect(el().querySelector<HTMLInputElement>('#card-number')!.value).toBe('4242 4242 4242 4242');
    expect(el().querySelector('.preview__number')!.textContent).toContain('4242 4242 4242 4242');
    expect(el().querySelector('.preview__brand')!.textContent).toContain('Visa');

    await type('card-name', 'Asha Rao');

    expect(el().querySelector('.preview')!.textContent).toContain('Asha Rao');
  });

  it('blocks an invalid card and shows errors', async () => {

    await open();
    await choose('Card');
    await submit();

    expect(el().textContent).toContain('Enter the card number.');
    expect(el().textContent).toContain('Enter the name on the card.');
    expect(TestBed.inject(Router).url).toBe('/checkout/payment');

    await type('card-number', '4242424242424241');
    await type('card-name', 'Asha Rao');
    await type('card-expiry', '1299');
    await type('card-cvv', '123');
    await submit();

    expect(el().textContent).toContain('Enter a valid card number.');
    expect(TestBed.inject(Router).url).toBe('/checkout/payment');
  });

  it('keeps only the method and a masked summary when the card is valid', async () => {

    await open();
    await choose('Card');
    await type('card-number', '4242424242424242');
    await type('card-name', 'Asha Rao');
    await type('card-expiry', '1299');
    await type('card-cvv', '123');
    await submit();
    await settle();

    const selection = TestBed.inject(PaymentSelection);

    expect(selection.method()).toBe('Card');
    expect(selection.summary()).toBe('Visa ending 4242');
    expect(JSON.stringify(selection)).not.toContain('4242424242424242');
    expect(TestBed.inject(Router).url).toBe('/checkout');
  });

  it('offers several UPI options and needs a valid UPI ID only for Other UPI ID', async () => {

    await open();
    await choose('UPI');

    ['Google Pay', 'PhonePe', 'Paytm', 'BHIM', 'Other UPI ID'].forEach(label =>
      expect(radio(label)).toBeTruthy());

    await choose('Other UPI ID');
    await submit();

    expect(el().textContent).toContain('Enter your UPI ID.');
    expect(TestBed.inject(Router).url).toBe('/checkout/payment');

    await type('upi-id', 'asha@okbank');
    await submit();
    await settle();

    const selection = TestBed.inject(PaymentSelection);

    expect(selection.method()).toBe('UPI');
    expect(selection.upiApp()).toBe('UpiId');
    expect(TestBed.inject(Router).url).toBe('/checkout');
  });

  it('asks for a mobile number with a UPI app and says the user can pay on their phone', async () => {

    await open();
    await choose('UPI');

    expect(el().textContent).toContain('You can pay on your phone');
    expect(el().querySelectorAll('.upi__option .logo').length).toBe(5);

    await choose('PhonePe');
    await submit();

    expect(el().textContent).toContain('Enter your mobile number.');
    expect(TestBed.inject(Router).url).toBe('/checkout/payment');

    await type('upi-phone', '12345');
    await submit();

    expect(el().textContent).toContain('Enter a valid 10 digit mobile number.');

    await type('upi-phone', '98765 43210');

    expect(el().querySelector<HTMLInputElement>('#upi-phone')!.value).toBe('9876543210');

    await submit();
    await settle();

    const selection = TestBed.inject(PaymentSelection);

    expect(selection.summary()).toBe('UPI \u00b7 PhonePe \u00b7 +91 \u2022\u2022\u2022\u2022\u2022\u20223210');
    expect(JSON.stringify(selection)).not.toContain('9876543210');
    expect(TestBed.inject(Router).url).toBe('/checkout');
  });

  it('explains pay on delivery', async () => {

    await open();
    await choose('Pay on delivery');

    expect(el().textContent).toContain('in cash or by UPI');

    await submit();
    await settle();

    expect(TestBed.inject(PaymentSelection).method()).toBe('CashOnDelivery');
    expect(component).toBeTruthy();
  });
});
