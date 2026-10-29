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
  category: ProductCategory;
  productClass?: ProductClass | null;
  note?: string;
  imageUrl?: string;
  isAvailable: boolean;
}

export interface ProductInput {
  name: string;
  price: number;
  unit: ProductUnit;
  category: ProductCategory;
  productClass?: ProductClass | null;
  note?: string;
  isAvailable: boolean;
}

export type ProductUnit = 'Kg' | 'Komad' | 'Veza' | 'Litar' | 'Gram' | 'Pakovanje' | 'Kutija' | 'Flasa';

export type ProductCategory = 'Voce' | 'Povrce' | 'MlecniProizvodi' | 'MesniProizvodi' | 'Konditori' | 'KucnaHemija' | 'Ostalo';

export type ProductClass = 'Prva' | 'Druga' | 'Treca';

export type ProductSort = 'NameAsc' | 'NameDesc' | 'PriceAsc' | 'PriceDesc' | 'Newest';
