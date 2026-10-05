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
  phoneNumber?: string;
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
  isActive: boolean;
  roles: string[];
  vendors: VendorShort[];
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
