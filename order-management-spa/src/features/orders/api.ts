import { apiFetch } from '../../api/client';
import type { Customer, OrderDetail, OrderSummary } from '../../api/types';

export interface OrderPage {
  items: OrderSummary[];
  page: number;
  pageSize: number;
}

export async function listOrders(params: URLSearchParams) {
  return apiFetch<OrderPage>(`/api/orders?${params}`);
}

export async function searchCustomers(search: string) {
  const params = new URLSearchParams({ search, page: '1', pageSize: '100' });
  return apiFetch<{ items: Customer[] }>(`/api/customers?${params}`);
}

export interface CreateOrderLineItem {
  productSku: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderInput {
  customerId: string;
  currencyCode: string;
  lineItems: CreateOrderLineItem[];
}

export function createOrder(input: CreateOrderInput) {
  return apiFetch<OrderDetail>('/api/orders', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}
