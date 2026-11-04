import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { Slider } from './slider';

@Component({
  imports: [Slider],
  template: `
    <div appSlider [sliderClones]="clones()" [sliderInterval]="1000">
      @for (n of slides(); track n) { <div class="s">{{ n }}</div> }
    </div>`
})
class Host {
  clones = signal(0);
  slides = signal([1, 2, 3]);
}

const SLIDE = 300;

// jsdom has no layout, so fake a 300px viewport over 300px slides.
function setup(clones = 0, count = 3) {

  const fixture: ComponentFixture<Host> = TestBed.createComponent(Host);
  fixture.componentInstance.clones.set(clones);
  fixture.componentInstance.slides.set(Array.from({ length: count }, (_, i) => i + 1));
  fixture.detectChanges();

  const debug = fixture.debugElement.query(By.directive(Slider));
  const el = debug.nativeElement as HTMLElement;
  const slider = debug.injector.get(Slider);

  let left = 0;

  Object.defineProperty(el, 'scrollLeft', { get: () => left, set: (v: number) => (left = v), configurable: true });
  Object.defineProperty(el, 'clientWidth', { value: SLIDE, configurable: true });
  Object.defineProperty(el, 'scrollWidth', { value: SLIDE * count, configurable: true });
  el.getBoundingClientRect = () => ({ left: 0 } as DOMRect);
  Array.from(el.children).forEach((child, i) => {
    (child as HTMLElement).getBoundingClientRect = () => ({ left: i * SLIDE - left } as DOMRect);
  });

  const scrollTo = vi.fn((options: ScrollToOptions) => { left = options.left ?? 0; });
  el.scrollTo = scrollTo as unknown as typeof el.scrollTo;

  const scrolled = (to: number) => {
    left = to;
    el.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
  };

  scrolled(0);

  return { fixture, el, slider, scrollTo, scrolled };
}

function pointer(type: string, clientX: number, buttons = 1): Event {
  return Object.assign(new MouseEvent(type, { bubbles: true, clientX, button: 0, buttons }), { pointerType: 'mouse' });
}

describe('Slider', () => {

  afterEach(() => vi.useRealTimers());

  it('only reports itself as scrollable when the content overflows', () => {
    const { slider } = setup();

    expect(slider.scrollable()).toBe(true);
    expect(slider.count()).toBe(3);
  });

  it('moves to the next slide, tracking the index as it scrolls', () => {
    const { slider, scrollTo, scrolled } = setup();

    slider.next();
    expect(scrollTo).toHaveBeenCalledWith({ left: SLIDE, behavior: 'smooth' });

    scrolled(SLIDE);
    expect(slider.index()).toBe(1);
  });

  it('rewinds to the first slide after the last one', () => {
    const { slider, scrollTo, scrolled } = setup();

    scrolled(SLIDE * 2);
    slider.next();

    expect(scrollTo).toHaveBeenLastCalledWith({ left: 0, behavior: 'smooth' });
  });

  it('with a copy, continues from the real first slide instead of rewinding', () => {
    const { slider, scrollTo, scrolled } = setup(1);

    expect(slider.count()).toBe(2);

    scrolled(SLIDE);
    slider.next();
    expect(scrollTo).toHaveBeenLastCalledWith({ left: SLIDE * 2, behavior: 'smooth' });

    // Arriving on the copy jumps back to the start without animation.
    scrolled(SLIDE * 2);
    expect(scrollTo).toHaveBeenLastCalledWith({ left: 0, behavior: 'instant' });
    expect(slider.index()).toBe(0);
  });

  it('with several copies, slides keep moving even when the real slides barely overflow', () => {
    // Two real slides plus two copies of them: 4 x 300px in a 300px viewport.
    const { slider, scrollTo, scrolled } = setup(2, 4);

    expect(slider.count()).toBe(2);
    expect(slider.scrollable()).toBe(true);

    slider.next();
    expect(scrollTo).toHaveBeenLastCalledWith({ left: SLIDE, behavior: 'smooth' });

    scrolled(SLIDE);
    slider.next();
    expect(scrollTo).toHaveBeenLastCalledWith({ left: SLIDE * 2, behavior: 'smooth' });

    // The first copy looks like the first real slide, so it jumps there without animation.
    scrolled(SLIDE * 2);
    expect(scrollTo).toHaveBeenLastCalledWith({ left: 0, behavior: 'instant' });
    expect(slider.index()).toBe(0);
  });

  it('advances on its own and stops while hovered or paused', () => {
    vi.useFakeTimers();
    const { fixture, el, slider, scrollTo } = setup();

    vi.advanceTimersByTime(1000);
    expect(scrollTo).toHaveBeenCalledTimes(1);

    el.dispatchEvent(new Event('mouseenter'));
    fixture.detectChanges();
    vi.advanceTimersByTime(3000);
    expect(scrollTo).toHaveBeenCalledTimes(1);

    el.dispatchEvent(new Event('mouseleave'));
    slider.toggle();
    fixture.detectChanges();
    vi.advanceTimersByTime(3000);
    expect(scrollTo).toHaveBeenCalledTimes(1);
  });

  it('drags with the mouse and swallows the click that ends the drag', () => {
    const { el, scrollTo, scrolled } = setup();
    const clicked = vi.fn();
    el.children[0].addEventListener('click', clicked);

    scrolled(SLIDE);

    el.dispatchEvent(pointer('pointerdown', 200));
    el.dispatchEvent(pointer('pointermove', 150));
    expect(el.scrollLeft).toBe(SLIDE + 50);

    el.dispatchEvent(pointer('pointerup', 150, 0));
    expect(scrollTo).toHaveBeenCalled();

    el.children[0].dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(clicked).not.toHaveBeenCalled();

    // A plain click afterwards works again.
    el.dispatchEvent(pointer('pointerdown', 200));
    el.dispatchEvent(pointer('pointerup', 200, 0));
    el.children[0].dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(clicked).toHaveBeenCalledTimes(1);
  });

  it('steps with the arrow keys only when the row itself has focus', () => {
    const { el, scrollTo } = setup();

    el.children[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(scrollTo).not.toHaveBeenCalled();

    el.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(scrollTo).toHaveBeenCalledWith({ left: SLIDE, behavior: 'smooth' });
  });
});
