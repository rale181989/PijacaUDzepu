// Linkovi za Google mape po koordinatama (besplatno, otvara aplikaciju mapa na telefonu)

export interface GeoPoint { lat: number; lng: number; }

// Tačka tezge, a ako je prodavac nije postavio – centar pijace (ili ništa)
export function stallPoint(o: { stallLatitude?: number | null; stallLongitude?: number | null }): GeoPoint | null {
  return o.stallLatitude != null && o.stallLongitude != null ? { lat: o.stallLatitude, lng: o.stallLongitude } : null;
}

export function marketPoint(o: { marketLatitude?: number | null; marketLongitude?: number | null }): GeoPoint | null {
  return o.marketLatitude != null && o.marketLongitude != null ? { lat: o.marketLatitude, lng: o.marketLongitude } : null;
}

export function pointText(p: GeoPoint): string {
  return `${p.lat.toFixed(6)},${p.lng.toFixed(6)}`;
}

// Navigacija do tačke; kupac ide peške, kurir biciklom
export function directionsTo(p: GeoPoint, mode: 'walking' | 'bicycling' = 'walking'): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${pointText(p)}&travelmode=${mode}`;
}
