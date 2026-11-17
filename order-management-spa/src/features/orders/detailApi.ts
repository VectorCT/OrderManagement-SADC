import { apiFetch } from '../../api/client';
import type { Customer, OrderDetail } from '../../api/types';

export function getOrder(id: string) {
  return apiFetch<OrderDetail>(`/api/orders/${id}`);
}

export function getCustomer(id: string) {
  return apiFetch<Customer>(`/api/customers/${id}`);
}

export function transitionOrder(id: string, status: string, idempotencyKey: string) {
  return apiFetch<OrderDetail>(`/api/orders/${id}/status`, {
    method: 'PUT',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify({ status }),
  });
}
