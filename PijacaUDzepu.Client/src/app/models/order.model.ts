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
  pickedUpAt?: string;
  deliveredAt?: string;
  items: OrderItem[];
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
}

// Pickup = rezervacija, kupac sam preuzima kod prodavca; Courier = dostava preko Eko kurira
export type FulfillmentType = 'Pickup' | 'Courier';

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'ReadyForPickup' | 'InDelivery' | 'Completed' | 'Cancelled';
