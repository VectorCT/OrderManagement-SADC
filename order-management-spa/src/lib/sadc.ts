export const CURRENCY_BY_COUNTRY: Record<string, string[]> = {
  AO: ['AOA'], BW: ['BWP'], KM: ['KMF'], CD: ['CDF'], SZ: ['SZL'],
  LS: ['LSL'], MG: ['MGA'], MW: ['MWK'], MU: ['MUR'], MZ: ['MZN'],
  NA: ['NAD'], SC: ['SCR'], ZA: ['ZAR'], TZ: ['TZS'], ZM: ['ZMW'],
  ZW: ['ZWL', 'USD'],
};

export function formatMoney(amount: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
}
