import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Vendor, VendorInput } from '../models/vendor.model';

@Injectable({ providedIn: 'root' })
export class VendorService {
  private baseUrl = environment.apiUrl + '/vendors';

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<Vendor[]>(this.baseUrl);
  }

  getAllIncludingInactive() {
    return this.http.get<Vendor[]>(`${this.baseUrl}/all`);
  }

  getById(id: number) {
    return this.http.get<Vendor>(`${this.baseUrl}/${id}`);
  }

  create(dto: VendorInput) {
    return this.http.post<Vendor>(this.baseUrl, dto);
  }

  update(id: number, dto: VendorInput) {
    return this.http.put<Vendor>(`${this.baseUrl}/${id}`, dto);
  }

  getMyVendor() {
    return this.http.get<Vendor>(`${this.baseUrl}/my-vendor`);
  }

  updateMyVendor(dto: VendorInput) {
    return this.http.put<Vendor>(`${this.baseUrl}/my-vendor`, dto);
  }

  toggleActive(id: number) {
    return this.http.put<void>(`${this.baseUrl}/${id}/toggle-active`, {});
  }

  uploadImage(id: number, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ imageUrl: string }>(`${this.baseUrl}/${id}/image`, formData);
  }
}
