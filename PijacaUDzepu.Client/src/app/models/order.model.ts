import { ProductUnit } from './product.model';

export interface Order {
  id: number;
  customerId?: number;
  customerName: string;
  vendorId: number;
  vendorName: string;
  status: OrderStatus;
  totalAmount: number;
  fulfillment: FulfillmentType;
  deliveryFee: number;
  amountToPay: number;
  vendorMessage?: string;
  vendorMessageAt?: string;
  note?: string;
  createdAt: string;
  isGuestOrder: boolean;
  trackingCode?: string;
  contactPhone?: string;
  deliveryAddress?: string;
  marketName?: string;
  stallLabel?: string;
  vendorPhones: string[];
  courierId?: number;
  courierName?: string;
  courierPhone?: string;
  courierAssignedAt?: string;
  // Prodavac je odgovorio (potvrdio ili odbio)
  respondedAt?: string | null;
  pickedUpAt?: string;
  deliveredAt?: string;
  items: OrderItem[];
  // Sve porudžbine jedne korpe imaju isti checkoutId (kurir ih vozi zajedno)
  checkoutId?: string | null;
  marketAddress?: string | null;
  // Ocena kupca posle preuzimanja
  rating?: OrderRating | null;
  // Kurir nije dostavio: obavezan razlog
  notDeliveredReason?: string | null;
  notDeliveredAt?: string | null;
  // Trag kada je SuperAdmin ručno zatvorio porudžbinu koja je ostala otvorena
  closedByAdminName?: string | null;
  closedByAdminAt?: string | null;
}

export interface OrderRating {
  vendorScore: number;
  courierScore?: number | null;
  comment?: string | null;
  createdAt: string;
}

export interface OrderItem {
  id: number;
  productId?: number;
  productName: string;
  quantity: number;
  unit: ProductUnit;
  unitPrice: number;
  totalPrice: number;
}

export interface OrderInput {
  items: OrderItemInput[];
  note?: string;
  fulfillment: FulfillmentType;
  contactPhone: string;
  deliveryAddress?: string;
}

export interface GuestOrderInput {
  items: OrderItemInput[];
  note?: string;
  guestName: string;
  guestPhone: string;
  guestEmail?: string;
  guestAddress?: string;
  fulfillment: FulfillmentType;
}

export interface OrderItemInput {
  productId: number;
  quantity: number;
  // Cena iz korpe: ako je prodavac u međuvremenu promenio cenu, server odbija porudžbinu
  expectedUnitPrice?: number;
}

// Pickup = rezervacija, kupac sam preuzima kod prodavca; Courier = dostava preko Eko kurira
export type FulfillmentType = 'Pickup' | 'Courier';

// Kupci i gosti privremeno ne mogu da otkažu porudžbinu; mora da se poklapa sa
// CustomerCancelEnabled u OrderService na serveru
export const CUSTOMER_CANCEL_ENABLED = false;

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'ReadyForPickup' | 'InDelivery' | 'Completed' | 'Cancelled' | 'NotDelivered';
