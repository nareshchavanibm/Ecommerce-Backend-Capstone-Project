import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProductSpecification } from '../../../shared/models/product.model';
import { SpecEditor } from './spec-editor';

@Component({
  imports: [SpecEditor],
  template: '<app-spec-editor [(specifications)]="specs" />'
})
class Host {
  specs = signal<ProductSpecification[]>([]);
}

describe('SpecEditor', () => {
  let fixture: ComponentFixture<Host>;

  const root = () => fixture.nativeElement as HTMLElement;
  const rows = () => root().querySelectorAll('li.row');
  const click = (label: string) =>
    (Array.from(root().querySelectorAll('button')).find(b => b.textContent?.includes(label)) as HTMLButtonElement).click();
  const type = (selector: string, value: string) => {
    const input = root().querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };

  beforeEach(() => {
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
  });

  it('adds a row and writes typed values back to the parent', () => {
    click('Add specification');
    fixture.detectChanges();

    type('input[id$="-group-0"]', 'Display');
    type('input[id$="-name-0"]', 'Size');
    type('input[id$="-value-0"]', '6 inch');
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(fixture.componentInstance.specs()).toEqual([{ group: 'Display', name: 'Size', value: '6 inch' }]);
  });

  it('starts a new row in the previous row group', () => {
    fixture.componentInstance.specs.set([{ group: 'Display', name: 'Size', value: '6' }]);
    fixture.detectChanges();

    click('Add specification');

    expect(fixture.componentInstance.specs()[1].group).toBe('Display');
  });

  it('removes a row', () => {
    fixture.componentInstance.specs.set([
      { group: null, name: 'A', value: '1' },
      { group: null, name: 'B', value: '2' }
    ]);
    fixture.detectChanges();

    click('Remove');
    fixture.detectChanges();

    expect(fixture.componentInstance.specs().map(s => s.name)).toEqual(['B']);
  });

  it('shows an inline error once a half-filled row loses focus', () => {
    fixture.componentInstance.specs.set([{ group: null, name: 'Brand', value: '' }]);
    fixture.detectChanges();

    expect(root().querySelector('.row__error')).toBeNull();

    (root().querySelector('input[id$="-value-0"]') as HTMLInputElement).dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(root().querySelector('.row__error')?.textContent).toContain('value');
  });
});
