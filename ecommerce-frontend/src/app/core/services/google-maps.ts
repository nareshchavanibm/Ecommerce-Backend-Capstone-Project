import { Injectable } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface AddressSuggestion {
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

interface AddressPart {
  types: string[];
  text: string;
}

// The Google Maps SDK ships no bundled types here, so the browser global is accessed untyped.
type GoogleNamespace = any;

@Injectable({
  providedIn: 'root'
})
export class GoogleMapsService {

  // Location features stay hidden until an API key is set in the environment file.
  readonly enabled = !!environment.googleMapsApiKey;

  private loading?: Promise<void>;

  load(): Promise<void> {

    this.loading ??= new Promise<void>((resolve, reject) => {

      if (this.google()?.maps?.importLibrary) {
        resolve();
        return;
      }

      const script = document.createElement('script');

      script.src = 'https://maps.googleapis.com/maps/api/js'
        + `?key=${encodeURIComponent(environment.googleMapsApiKey)}&v=weekly&loading=async`;
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => {
        this.loading = undefined;
        reject(new Error('Google Maps could not be loaded.'));
      };

      document.head.appendChild(script);
    });

    return this.loading;
  }

  async reverseGeocode(latitude: number, longitude: number): Promise<AddressSuggestion> {

    await this.load();

    const { Geocoder } = await this.google().maps.importLibrary('geocoding');
    const { results } = await new Geocoder().geocode({ location: { lat: latitude, lng: longitude } });

    if (!results?.length) {
      throw new Error('We could not find an address for your location.');
    }

    return this.toSuggestion(
      results[0].address_components.map((part: any) => ({ types: part.types, text: part.long_name }))
    );
  }

  // Creates Google's Place Autocomplete input; onSelect receives the chosen place as address fields.
  async createAutocomplete(onSelect: (address: AddressSuggestion) => void): Promise<HTMLElement> {

    await this.load();

    const { PlaceAutocompleteElement } = await this.google().maps.importLibrary('places');
    const element: HTMLElement = new PlaceAutocompleteElement({});

    const handle = async (place: any): Promise<void> => {
      await place.fetchFields({ fields: ['addressComponents'] });

      onSelect(this.toSuggestion(
        (place.addressComponents ?? []).map((part: any) => ({ types: part.types, text: part.longText }))
      ));
    };

    // The selection event was renamed between Maps releases; listen for both.
    element.addEventListener('gmp-select', (event: any) => void handle(event.placePrediction.toPlace()));
    element.addEventListener('gmp-placeselect', (event: any) => void handle(event.place));

    return element;
  }

  private toSuggestion(parts: AddressPart[]): AddressSuggestion {

    const pick = (...types: string[]): string => {
      for (const type of types) {
        const match = parts.find(part => part.types.includes(type));

        if (match) {
          return match.text;
        }
      }

      return '';
    };

    const street = [pick('street_number'), pick('route')].filter(Boolean).join(' ')
      || pick('premise', 'point_of_interest', 'establishment');
    const area = pick('sublocality_level_1', 'sublocality', 'neighborhood');

    return {
      // Many addresses have no street; use the area as line 1 then.
      addressLine1: street || area,
      addressLine2: street ? area : '',
      city: pick('locality', 'administrative_area_level_3', 'administrative_area_level_2'),
      state: pick('administrative_area_level_1'),
      postalCode: pick('postal_code'),
      country: pick('country')
    };
  }

  private google(): GoogleNamespace {
    return (window as any).google;
  }
}
