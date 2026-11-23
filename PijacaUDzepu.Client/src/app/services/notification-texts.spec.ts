import { TranslateService } from './translate.service';
import { rejectionText, vendorLateButtons, vendorLateText } from './notification.service';

describe('tekstovi obaveštenja kupcu', () => {
  let ts: TranslateService;

  beforeEach(() => {
    localStorage.clear();
    ts = new TranslateService();
  });

  it('prodavac kasni: korpa sa više tezgi, sa brojem prodavca i dugmetom "Pozovi"', () => {
    const data = { orderId: 12, vendorName: 'Marko Voće', vendorPhone: '064 123 4567', minutesLeft: 5, rest: true, pickup: false };
    const text = vendorLateText(ts, data);
    expect(text.startsWith('#12 Marko Voće')).toBe(true);
    expect(text).toContain('5 min');
    expect(text).toContain('ostala roba');
    expect(text).toContain('064 123 4567');
    expect(vendorLateButtons(ts, data).map(b => b.text)).toEqual([ts.t('order.callVendorShort'), 'OK']);
  });

  it('prodavac kasni bez telefona: rezervacija se otkazuje, nema dugmeta "Pozovi"', () => {
    const data = { orderId: 7, vendorName: 'Jovana', vendorPhone: null, minutesLeft: 3, rest: false, pickup: true };
    expect(vendorLateText(ts, data)).toContain('rezervacija');
    expect(vendorLateButtons(ts, data).map(b => b.text)).toEqual(['OK']);
  });

  it('odbijena porudžbina sa razlogom prodavca', () => {
    expect(rejectionText(ts, { orderId: 3, vendorName: 'Marko', message: 'Nestalo je robe' }))
      .toBe(`${ts.t('orders.order')} #3 ${ts.t('order.rejectedToast')}. Marko: Nestalo je robe`);
  });
});
