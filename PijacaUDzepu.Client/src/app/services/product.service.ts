import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Product, ProductCategory, ProductInput } from '../models/product.model';
import { PagedResult } from '../models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private baseUrl = environment.apiUrl + '/products';

  constructor(private http: HttpClient) {}

  getAll(search?: string, vendorId?: number, marketId?: number, category?: ProductCategory, skip = 0, take = 20) {
    let params = new HttpParams()
      .set('skip', skip.toString())
      .set('take', take.toString());
    if (search) params = params.set('search', search);
    if (vendorId) params = params.set('vendorId', vendorId.toString());
    if (marketId) params = params.set('marketId', marketId.toString());
    if (category) params = params.set('category', category);
    return this.http.get<PagedResult<Product>>(this.baseUrl, { params });
  }

  getById(id: number) {
    return this.http.get<Product>(`${this.baseUrl}/${id}`);
  }

  getMyProducts() {
    return this.http.get<Product[]>(`${this.baseUrl}/my-products`);
  }

  create(dto: ProductInput) {
    return this.http.post<Product>(this.baseUrl, dto);
  }

  update(id: number, dto: ProductInput) {
    return this.http.put<Product>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  uploadImage(id: number, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ imageUrl: string }>(`${this.baseUrl}/${id}/image`, formData);
  }
}
