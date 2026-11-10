export interface Vendor {
  id: number;
  marketId: number;
  marketName: string;
  stallId?: number;
  stallLabel?: string;
  name: string;
  description?: string;
  address?: string;
  phones: string[];
  imageUrl?: string;
  isActive: boolean;
  acceptsReservations: boolean;
  offersDelivery: boolean;
  minOrderAmount?: number;
  deliveryRadiusKm?: number;
  deliverySchedule?: DeliveryScheduleEntry[];
  // Radno vreme i dostupnost (day: 1 = ponedeljak ... 7 = nedelja)
  ordersPaused: boolean;
  openingHours: DeliveryScheduleEntry[];
  closedDates: string[];
  earlyCloseToday?: string | null;
  isOpen: boolean;
  closedReason?: 'Paused' | 'ClosedDay' | 'Hours' | 'Inactive' | null;
  opensAt?: string | null;
  closesAt?: string | null;
  createdAt: string;
}

export interface VendorAvailabilityInput {
  ordersPaused: boolean;
  openingHours: DeliveryScheduleEntry[];
  closedDates: string[];
  // "HH:mm": danas se zatvara ranije (samo danas); null = po rasporedu
  earlyCloseToday?: string | null;
}

export interface VendorInput {
  marketId: number;
  stallId?: number;
  name: string;
  description?: string;
  address?: string;
  phones: string[];
  acceptsReservations?: boolean;
  offersDelivery?: boolean;
  minOrderAmount?: number;
  deliveryRadiusKm?: number;
  deliverySchedule?: DeliveryScheduleEntry[];
}

export interface DeliveryScheduleEntry {
  day: number;
  from: string;
  to: string;
}
