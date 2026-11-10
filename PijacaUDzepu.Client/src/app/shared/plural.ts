// Srpska množina: 1 (one), 2–4 (few), 5+ i 11–14 (many)
export function srPlural(n: number, one: string, few: string, many: string): string {
  const lastTwo = Math.abs(n) % 100, last = Math.abs(n) % 10;
  if (last === 1 && lastTwo !== 11) return one;
  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few;
  return many;
}

// "porudžbina": 1 porudžbina, 2–4 porudžbine, 5+ porudžbina
export function ordersWord(n: number): string {
  return srPlural(n, 'porudžbina', 'porudžbine', 'porudžbina');
}

// "stavka": 1 stavka, 2–4 stavke, 5+ stavki
export function itemsWord(n: number): string {
  return srPlural(n, 'stavka', 'stavke', 'stavki');
}
