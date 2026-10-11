export interface Profile {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
}

export interface UpdateProfile {
  firstName: string;
  lastName: string;
}

export interface AddressInput {
  fullName: string;
  phone: string;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  postalCode: string;
  country: string;
  isDefault: boolean;
}

export interface Address extends AddressInput {
  id: number;
}
