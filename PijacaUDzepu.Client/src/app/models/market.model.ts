export interface Market {
  id: number;
  name: string;
  description?: string;
  address?: string;
  imageUrl?: string;
  isActive: boolean;
  vendorCount: number;
}

export interface MarketInput {
  name: string;
  description?: string;
  address?: string;
}
