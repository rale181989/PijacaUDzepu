export interface Product {
  id: number;
  vendorId: number;
  vendorName: string;
  marketId: number;
  marketName: string;
  stallLabel?: string;
  name: string;
  price: number;
  unit: ProductUnit;
  imageUrl?: string;
  isAvailable: boolean;
}

export interface ProductInput {
  name: string;
  price: number;
  unit: ProductUnit;
  isAvailable: boolean;
}

export type ProductUnit = 'Kg' | 'Komad' | 'Veza';
