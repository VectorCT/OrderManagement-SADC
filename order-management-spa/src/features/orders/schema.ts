import { z } from 'zod';

export const orderLineSchema = z.object({
  productSku: z.string().trim().min(1, 'Product SKU is required.'),
  quantity: z.number().int('Quantity must be a whole number.').positive('Quantity must be greater than zero.'),
  unitPrice: z.number().nonnegative('Unit price cannot be negative.'),
});

export const orderSchema = z.object({
  customerId: z.string().uuid('Select a customer.'),
  currencyCode: z.string().min(1, 'Select a currency.'),
  lineItems: z.array(orderLineSchema).min(1, 'At least one line item is required.'),
});

export type OrderFormData = z.infer<typeof orderSchema>;
