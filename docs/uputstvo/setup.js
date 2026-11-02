// Priprema demo podataka za snimke ekrana (lokalni API)
const API = 'http://localhost:5000/api';
const fs = require('fs');

async function call(method, path, body, token) {
  const res = await fetch(API + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
    body: body ? JSON.stringify(body) : undefined
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${text}`);
  return text ? JSON.parse(text) : null;
}

const login = (u, p) => call('POST', '/auth/login', { userName: u, password: p });

(async () => {
  const kupac = await login('kupac', 'kupac1234');
  const marko = await login('marko', 'vendor1234');
  const kurir = await login('kurir1', 'kurir1234');

  const products = (await call('GET', '/products?vendorId=1&take=20')).items;
  const p = products.filter(x => x.isAvailable);
  console.log('marko products', p.map(x => `${x.id} ${x.name} ${x.unit}`).join(' | '));

  await call('PUT', '/couriers/me/shift', { isOnShift: true }, kurir.token);

  // 1) Kupac: dostava kurirom -> prodavac potvrdi, kurir prihvati (kartica "Moje" kod kurira)
  const [o1] = await call('POST', '/orders', {
    items: [{ productId: p[0].id, quantity: 2 }, { productId: p[1].id, quantity: 1 }],
    fulfillment: 'Courier', contactPhone: '0641234567', deliveryAddress: 'Bulevar oslobođenja 12, Novi Sad', note: 'Zvono 3'
  }, kupac.token);
  await call('PUT', `/orders/${o1.id}/status`, { status: 'Confirmed', message: 'Spremamo, stiže za sat vremena' }, marko.token);
  await call('POST', `/couriers/orders/${o1.id}/accept`, {}, kurir.token);

  // 2) Kupac: rezervacija za preuzimanje na tezgi -> potvrđena
  const [o2] = await call('POST', '/orders', {
    items: [{ productId: p[2].id, quantity: 1 }],
    fulfillment: 'Pickup', contactPhone: '0641234567'
  }, kupac.token);
  await call('PUT', `/orders/${o2.id}/status`, { status: 'Confirmed' }, marko.token);

  // 3) Gost: dostava kurirom, ostaje na čekanju (zvoni prodavcu i kuriru)
  const guest = await call('POST', '/orders/guest', {
    items: [{ productId: p[0].id, quantity: 1 }, { productId: p[3].id, quantity: 3 }],
    guestName: 'Milica Petrović', guestPhone: '0657654321', guestAddress: 'Futoška 45, Novi Sad',
    fulfillment: 'Courier', note: 'Molim zrelije'
  });

  fs.writeFileSync(__dirname + '/demo.json', JSON.stringify({
    kupac, marko, kurir, trackingCode: guest[0].trackingCode, orders: [o1.id, o2.id, guest[0].id]
  }, null, 2));
  console.log('ok tracking', guest[0].trackingCode, 'orders', o1.id, o2.id, guest[0].id);
})().catch(e => { console.error(e.message); process.exit(1); });
