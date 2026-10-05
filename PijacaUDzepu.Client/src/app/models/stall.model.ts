export interface Stall {
  id: number;
  marketId: number;
  marketName: string;
  label: string;
  isActive: boolean;
}

export interface StallInput {
  marketId: number;
  label: string;
}
