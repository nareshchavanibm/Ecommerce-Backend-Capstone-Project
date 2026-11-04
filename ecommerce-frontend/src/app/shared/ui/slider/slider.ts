import {
  DestroyRef,
  Directive,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  computed,
  effect,
  inject,
  input,
  signal
} from '@angular/core';

const DRAG_THRESHOLD = 5;

// Turns a horizontally scrollable row into a slider: it advances right to left on its own,
// and can be swiped, dragged with the mouse, stepped with buttons or arrow keys.
// Pair it with `scroll-snap-type: x mandatory` and `scroll-snap-align: start` on the children.
//
// With `sliderClones`, that many trailing children must be visual copies of the first children (as
// many as can be on screen at once). Resting on a copy silently jumps back to the real slide it
// duplicates, so the row can always keep moving right to left, even with only a couple of slides.
@Directive({
  selector: '[appSlider]',
  exportAs: 'appSlider',
  host: {
    '[class.slider--dragging]': 'dragging()',
    '(scroll)': 'sync()',
    '(pointerdown)': 'onPointerDown($event)',
    '(mouseenter)': 'hovered.set(true)',
    '(mouseleave)': 'hovered.set(false)',
    '(focusin)': 'focused.set(true)',
    '(focusout)': 'focused.set(false)',
    '(dragstart)': '$event.preventDefault()',
    '(keydown)': 'onKeydown($event)'
  }
})
export class Slider {

  readonly sliderInterval = input(5000);

  readonly sliderAutoplay = input(true, { transform: booleanAttribute });

  readonly sliderClones = input(0);

  // Position of the slide at the start edge (never a copy).
  readonly index = signal(0);

  readonly count = signal(0);

  readonly scrollable = signal(false);

  readonly userPaused = signal(false);

  protected readonly dragging = signal(false);

  protected readonly hovered = signal(false);

  protected readonly focused = signal(false);

  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  private readonly reducedMotion =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  private pressed = false;

  private startX = 0;

  private startScroll = 0;

  private suppressClick = false;

  // Moving on its own is only offered when there is something to move to.
  readonly canAutoplay = computed(() => this.sliderAutoplay() && this.scrollable() && !this.reducedMotion);

  readonly playing = computed(() => this.canAutoplay() && !this.userPaused());

  constructor() {

    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {

      const resize = typeof ResizeObserver === 'function' ? new ResizeObserver(() => this.sync()) : null;
      const mutation = typeof MutationObserver === 'function' ? new MutationObserver(() => this.sync()) : null;

      resize?.observe(this.el);
      mutation?.observe(this.el, { childList: true });

      // Capture phase: a click that ends a drag must not reach the links inside a slide.
      const swallowClick = (event: Event) => {
        if (this.suppressClick) {
          event.preventDefault();
          event.stopPropagation();
        }
      };

      this.el.addEventListener('click', swallowClick, true);

      destroyRef.onDestroy(() => {
        resize?.disconnect();
        mutation?.disconnect();
        this.el.removeEventListener('click', swallowClick, true);
        this.stopTracking();
      });

      this.sync();
    });

    effect(onCleanup => {

      // Reading the index restarts the timer after every move, manual or automatic.
      this.index();

      if (!this.playing() || this.hovered() || this.focused() || this.dragging()) {
        return;
      }

      const timer = setInterval(() => this.next(), this.sliderInterval());

      onCleanup(() => clearInterval(timer));
    });
  }

  next(): void {

    const slides = this.slides();

    if (slides.length === 0) {
      return;
    }

    if (this.looping(slides)) {
      this.goTo(this.index() + 1);
      return;
    }

    // Past the last slide the row rewinds to the start.
    this.goTo(this.atEnd() ? 0 : this.index() + 1);
  }

  prev(): void {

    if (this.looping() && this.el.scrollLeft <= 1) {
      // Step back from the identical copy of the first slide, so the move is still a slide.
      this.scrollTo(this.slideLeft(this.slides()[this.realCount()]), 'instant');
      requestAnimationFrame(() => this.goTo(this.realCount() - 1));
      return;
    }

    this.goTo(this.el.scrollLeft <= 1 ? this.slides().length - 1 : this.index() - 1);
  }

  goTo(position: number): void {

    const target = this.slides()[position];

    if (target) {
      this.scrollTo(this.slideLeft(target), this.reducedMotion ? 'auto' : 'smooth');
    }
  }

  toggle(): void {
    this.userPaused.update(paused => !paused);
  }

  protected sync(): void {

    const slides = this.slides();
    const max = this.el.scrollWidth - this.el.clientWidth;

    this.count.set(this.realCount(slides));
    this.scrollable.set(max > 1);

    const nearest = this.nearest(slides);
    const real = this.realCount(slides);

    // Resting on a copy looks the same as the slide it duplicates, so continue from the real one.
    if (this.looping(slides) && nearest >= real && !this.dragging()
      && Math.abs(this.slideLeft(slides[nearest]) - this.el.scrollLeft) < 2) {
      this.scrollTo(this.slideLeft(slides[nearest - real]), 'instant');
      this.index.set(nearest - real);
      return;
    }

    this.index.set(this.looping(slides) && nearest >= real ? nearest - real : nearest);
  }

  protected onPointerDown(event: PointerEvent): void {

    // Fresh press: any earlier drag no longer needs its click swallowed.
    this.suppressClick = false;

    // Touch already scrolls natively; only the mouse needs drag emulation.
    if (event.pointerType !== 'mouse' || event.button !== 0 || !this.scrollable()) {
      return;
    }

    this.pressed = true;
    this.startX = event.clientX;
    this.startScroll = this.el.scrollLeft;

    // Followed on the window so the drag keeps working when the pointer leaves the row.
    window.addEventListener('pointermove', this.onPointerMove);
    window.addEventListener('pointerup', this.endDrag);
    window.addEventListener('pointercancel', this.endDrag);
  }

  private readonly onPointerMove = (event: PointerEvent): void => {

    if (!this.pressed) {
      return;
    }

    if (event.buttons === 0) {
      this.endDrag();
      return;
    }

    const distance = event.clientX - this.startX;

    if (!this.dragging()) {

      if (Math.abs(distance) < DRAG_THRESHOLD) {
        return;
      }

      this.dragging.set(true);
      // Applied directly, not via a class, so snapping never fights the very first drag step.
      this.el.style.scrollSnapType = 'none';
    }

    this.el.scrollLeft = this.startScroll - distance;
  };

  private readonly endDrag = (): void => {

    this.stopTracking();

    if (!this.pressed) {
      return;
    }

    this.pressed = false;

    if (this.dragging()) {
      this.dragging.set(false);
      this.el.style.scrollSnapType = '';
      this.suppressClick = true;
      this.goTo(this.nearest());
    }
  };

  private stopTracking(): void {
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.endDrag);
    window.removeEventListener('pointercancel', this.endDrag);
  }

  protected onKeydown(event: KeyboardEvent): void {

    // Only when the row itself has focus, never while typing in something inside it.
    if (event.target !== this.el) {
      return;
    }

    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.next();
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.prev();
    }
  }

  private slides(): HTMLElement[] {
    return Array.from(this.el.children) as HTMLElement[];
  }

  private looping(slides = this.slides()): boolean {
    return this.sliderClones() > 0 && slides.length > this.sliderClones();
  }

  private realCount(slides = this.slides()): number {
    return this.looping(slides) ? slides.length - this.sliderClones() : slides.length;
  }

  private atEnd(): boolean {
    return this.el.scrollLeft >= this.el.scrollWidth - this.el.clientWidth - 1;
  }

  private slideLeft(slide: HTMLElement): number {
    return slide.getBoundingClientRect().left - this.el.getBoundingClientRect().left + this.el.scrollLeft;
  }

  private nearest(slides = this.slides()): number {

    let best = 0;
    let bestDistance = Infinity;

    slides.forEach((slide, position) => {
      const distance = Math.abs(this.slideLeft(slide) - this.el.scrollLeft);

      if (distance < bestDistance) {
        bestDistance = distance;
        best = position;
      }
    });

    return best;
  }

  private scrollTo(left: number, behavior: ScrollBehavior): void {

    if (typeof this.el.scrollTo === 'function') {
      this.el.scrollTo({ left, behavior });
    } else {
      this.el.scrollLeft = left;
    }
  }
}
