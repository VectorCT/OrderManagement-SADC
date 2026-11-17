import { z } from 'zod';

export const SADC_COUNTRIES = [
  ['AO', 'Angola'], ['BW', 'Botswana'], ['KM', 'Comoros'], ['CD', 'Democratic Republic of the Congo'],
  ['SZ', 'Eswatini'], ['LS', 'Lesotho'], ['MG', 'Madagascar'], ['MW', 'Malawi'],
  ['MU', 'Mauritius'], ['MZ', 'Mozambique'], ['NA', 'Namibia'], ['SC', 'Seychelles'],
  ['ZA', 'South Africa'], ['TZ', 'Tanzania'], ['ZM', 'Zambia'], ['ZW', 'Zimbabwe'],
] as const;

export const customerSchema = z.object({
  name: z.string().trim().min(1, 'Customer name is required.').max(200, 'Customer name must not exceed 200 characters.'),
  email: z.email('A valid email address is required.').max(320, 'Email address must not exceed 320 characters.'),
  countryCode: z.enum(SADC_COUNTRIES.map(([code]) => code)),
});

export type CustomerFormData = z.infer<typeof customerSchema>;
