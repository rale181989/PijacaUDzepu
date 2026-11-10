export interface LoginDto {
  userName: string;
  password: string;
}

export interface RegisterDto {
  userName: string;
  password: string;
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber: string;
  address: string;
  floor?: string;
  apartment?: string;
}

export interface UpdateProfileDto {
  phoneNumber: string;
  address?: string;
  floor?: string;
  apartment?: string;
  // "" = ukloni e-mail; izostavljeno = bez promene
  email?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface User {
  id: number;
  userName: string;
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber?: string;
  address?: string;
  floor?: string;
  apartment?: string;
  isActive: boolean;
  roles: string[];
  vendors: VendorShort[];
}

// Adresa za dostavu u jednom redu ("Dunavska 5, sprat 3, stan 12"); čitaju je prodavac i kurir, pa je na srpskom
export function deliveryAddressOf(user: User | null | undefined): string {
  if (!user?.address) return '';
  return [user.address, user.floor && ('sprat ' + user.floor), user.apartment && ('stan ' + user.apartment)]
    .filter(Boolean).join(', ');
}

export interface VendorShort {
  id: number;
  name: string;
}

export interface CreateVendorAdminDto {
  userName: string;
  password: string;
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber?: string;
  vendorId: number;
}

export interface InviteVendorDto {
  vendorId: number;
  email: string;
}

export interface AcceptInvitationDto {
  token: string;
  userName: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface CreateCourierDto {
  userName: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
}
