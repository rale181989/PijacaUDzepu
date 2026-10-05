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
  createdAt: string;
}

export interface VendorInput {
  marketId: number;
  stallId?: number;
  name: string;
  description?: string;
  address?: string;
  phone?: string;
}
