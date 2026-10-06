export interface Vendor {
  id: number;
  marketId: number;
  marketName: string;
  stallId?: number;
  stallLabel?: string;
  name: string;
  description?: string;
  address?: string;
  phone?: string;
  imageUrl?: string;
  isActive: boolean;
  acceptsReservations: boolean;
  offersDelivery: boolean;
  minOrderAmount?: number;
  deliveryRadiusKm?: number;
  deliverySchedule?: DeliveryScheduleEntry[];
  createdAt: string;
}

export interface VendorInput {
  marketId: number;
  stallId?: number;
  name: string;
  description?: string;
  address?: string;
  phone?: string;
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
