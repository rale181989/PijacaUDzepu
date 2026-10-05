import { ProductUnit } from './product.model';

export interface Order {
  id: number;
  customerId?: number;
  customerName: string;
  vendorId: number;
  vendorName: string;
  status: OrderStatus;
  totalAmount: number;
  note?: string;
  createdAt: string;
  isGuestOrder: boolean;
  guestPhone?: string;
  guestAddress?: string;
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
}

export interface GuestOrderInput {
  items: OrderItemInput[];
  note?: string;
  guestName: string;
  guestPhone: string;
  guestAddress?: string;
}

export interface OrderItemInput {
  productId: number;
  quantity: number;
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'ReadyForPickup' | 'Completed' | 'Cancelled';
