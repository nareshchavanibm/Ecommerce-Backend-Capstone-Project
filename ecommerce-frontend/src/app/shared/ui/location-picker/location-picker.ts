import { Component, ElementRef, afterNextRender, inject, output, signal, viewChild } from '@angular/core';

import { AddressSuggestion, GoogleMapsService } from '../../../core/services/google-maps';
import { Button } from '../button/button';

@Component({
  selector: 'app-location-picker',
  imports: [Button],
  templateUrl: './location-picker.html',
  styleUrl: './location-picker.css'
})
export class LocationPicker {

  protected readonly maps = inject(GoogleMapsService);

  readonly found = output<AddressSuggestion>();

  protected readonly locating = signal(false);

  protected readonly message = signal('');

  private readonly searchHost = viewChild<ElementRef<HTMLElement>>('searchHost');

  constructor() {

    afterNextRender(() => {

      const host = this.searchHost()?.nativeElement;

      if (!this.maps.enabled || !host) {
        return;
      }

      this.maps.createAutocomplete(address => {
        this.message.set('');
        this.found.emit(address);
      }).then(
        element => host.appendChild(element),
        () => this.message.set('Address search is not available right now.')
      );
    });
  }

  protected async useCurrentLocation(): Promise<void> {

    if (!navigator.geolocation) {
      this.message.set('Your browser does not support location.');
      return;
    }

    this.message.set('');
    this.locating.set(true);

    try {
      const position = await new Promise<GeolocationPosition>((resolve, reject) =>
        navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15000 })
      );

      const address = await this.maps.reverseGeocode(position.coords.latitude, position.coords.longitude);

      this.found.emit(address);
    } catch (error) {
      this.message.set(this.describe(error));
    } finally {
      this.locating.set(false);
    }
  }

  private describe(error: unknown): string {

    if (typeof error === 'object' && error !== null && 'code' in error) {
      const code = (error as GeolocationPositionError).code;

      if (code === 1) {
        return 'Location permission was denied. Allow location access in your browser, or type your address below.';
      }

      if (code === 2 || code === 3) {
        return 'We could not detect your location. Please try again or type your address.';
      }
    }

    return error instanceof Error && error.message
      ? error.message
      : 'We could not look up your location. Please type your address.';
  }
}
