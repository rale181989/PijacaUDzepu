export type CourierVehicle = 'Bicikl' | 'Skuter' | 'Motor' | 'Automobil' | 'Kombi';

export interface CourierProfile {
  userId: number;
  userName: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  vehicle?: CourierVehicle | null;
  licensePlate?: string;
  isOnShift: boolean;
}

export interface CourierProfileInput {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  vehicle?: CourierVehicle | null;
  licensePlate?: string;
}
