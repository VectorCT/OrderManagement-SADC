import { apiFetch } from '../../api/client';
import type { Customer } from '../../api/types';

export interface CustomerPage {
  items: Customer[];
  page: number;
  pageSize: number;
}

export async function searchCustomers(search: string, page: number, pageSize: number) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(Math.min(pageSize, 100)),
  });
  if (search) params.set('search', search);
  return apiFetch<CustomerPage>(`/api/customers?${params}`);
}

export function createCustomer(input: { name: string; email: string; countryCode: string }) {
  return apiFetch<Customer>('/api/customers', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}
