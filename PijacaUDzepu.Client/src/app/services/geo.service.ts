import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class GeoService {
  private geocodeCache = new Map<string, { lat: number; lng: number } | null>();

  getCurrentPosition(): Promise<{ lat: number; lng: number } | null> {
    return new Promise(resolve => {
      if (!navigator.geolocation) { resolve(null); return; }
      navigator.geolocation.getCurrentPosition(
        pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
        () => resolve(null),
        { timeout: 10000, maximumAge: 300000 }
      );
    });
  }

  async geocode(address: string): Promise<{ lat: number; lng: number } | null> {
    const key = address.trim().toLowerCase();
    if (!key) return null;
    if (this.geocodeCache.has(key)) return this.geocodeCache.get(key)!;
    try {
      const resp = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(address)}&limit=1`,
        { headers: { 'User-Agent': 'PijacaUDzepu/1.0' } }
      );
      const data = await resp.json();
      if (data.length > 0) {
        const result = { lat: parseFloat(data[0].lat), lng: parseFloat(data[0].lon) };
        this.geocodeCache.set(key, result);
        return result;
      }
    } catch {}
    this.geocodeCache.set(key, null);
    return null;
  }

  distanceKm(lat1: number, lng1: number, lat2: number, lng2: number): number {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2 +
              Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
              Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }
}
