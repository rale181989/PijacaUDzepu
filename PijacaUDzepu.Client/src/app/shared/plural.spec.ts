import { itemsWord, ordersWord, srPlural } from './plural';

describe('srpska množina', () => {
  it('1, 21, 101 su jednina; 2–4 i 22–24 "few"; 5+, 11–14 i 0 "many"', () => {
    const f = (n: number) => srPlural(n, 'one', 'few', 'many');
    expect([1, 21, 101].map(f)).toEqual(['one', 'one', 'one']);
    expect([2, 3, 4, 22, 34].map(f)).toEqual(['few', 'few', 'few', 'few', 'few']);
    expect([0, 5, 11, 12, 13, 14, 15, 111].map(f)).toEqual(Array(8).fill('many'));
  });

  it('porudžbina i stavka', () => {
    expect(`1 ${ordersWord(1)}, 3 ${ordersWord(3)}, 5 ${ordersWord(5)}`).toBe('1 porudžbina, 3 porudžbine, 5 porudžbina');
    expect(`1 ${itemsWord(1)}, 2 ${itemsWord(2)}, 7 ${itemsWord(7)}`).toBe('1 stavka, 2 stavke, 7 stavki');
  });
});
