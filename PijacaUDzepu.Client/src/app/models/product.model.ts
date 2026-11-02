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
  // Da li tezga sada prima porudžbine (radno vreme, neradni dan, pauza)
  vendorOpen?: boolean;
  vendorClosedReason?: string | null;
  vendorOpensAt?: string | null;
  // Prosečna ocena prodavca i broj ocena
  vendorRating?: number | null;
  vendorRatingCount?: number;
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
