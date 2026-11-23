import { directionsTo, marketPoint, pointText, showPickupWay, stallPoint } from './maps';

describe('mapa tezge', () => {
  it('tačka tezge i centar pijace postoje samo kad su obe koordinate poznate', () => {
    expect(stallPoint({ stallLatitude: 45.2531, stallLongitude: 19.8368 })).toEqual({ lat: 45.2531, lng: 19.8368 });
    expect(stallPoint({ stallLatitude: 45.2531, stallLongitude: null })).toBeNull();
    expect(stallPoint({})).toBeNull();
    expect(marketPoint({ marketLatitude: 45.2529, marketLongitude: 19.8371 })).toEqual({ lat: 45.2529, lng: 19.8371 });
    expect(marketPoint({ marketLatitude: null, marketLongitude: 19.8 })).toBeNull();
  });

  it('kupac dobija put do tezge samo dok je rezervacija otvorena', () => {
    for (const status of ['Pending', 'Confirmed', 'ReadyForPickup'])
      expect(showPickupWay({ fulfillment: 'Pickup', status })).toBe(true);
    for (const status of ['Rejected', 'Cancelled', 'Completed', 'NotDelivered'])
      expect(showPickupWay({ fulfillment: 'Pickup', status })).toBe(false);
    // kurirska porudžbina: kupac ne ide na pijacu
    expect(showPickupWay({ fulfillment: 'Courier', status: 'Confirmed' })).toBe(false);
  });

  it('navigacija ide na koordinate (6 decimala), peške za kupca i biciklom za kurira', () => {
    const p = { lat: 45.2531, lng: 19.8368 };
    expect(pointText(p)).toBe('45.253100,19.836800');
    expect(directionsTo(p)).toBe('https://www.google.com/maps/dir/?api=1&destination=45.253100,19.836800&travelmode=walking');
    expect(directionsTo(p, 'bicycling')).toContain('travelmode=bicycling');
  });
});
