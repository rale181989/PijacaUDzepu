export interface Market {
  id: number;
  name: string;
  description?: string;
  address?: string;
  imageUrl?: string;
  latitude?: number | null;
  longitude?: number | null;
  isActive: boolean;
  vendorCount: number;
}

export interface MarketInput {
  name: string;
  description?: string;
  address?: string;
  latitude?: number | null;
  longitude?: number | null;
}
