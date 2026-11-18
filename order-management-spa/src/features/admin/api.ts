import { apiFetch } from '../../api/client';
import type { AdminUser, MessagingDiagnostics } from '../../api/types';

export interface AdminUserPage {
  items: AdminUser[];
  page: number;
  pageSize: number;
}

export function listUsers(page = 1, pageSize = 100) {
  return apiFetch<AdminUserPage>(`/api/admin/users?page=${page}&pageSize=${pageSize}`);
}

export function getAdminUser(userId: string) {
  return apiFetch<AdminUser>(`/api/admin/users/${encodeURIComponent(userId)}`);
}

export function createAdminUser(input: { firstName: string; lastName: string; email: string; password: string }) {
  return apiFetch<AdminUser>('/api/admin/users', { method: 'POST', body: JSON.stringify(input) });
}

export function updateAdminUser(userId: string, input: { firstName: string; lastName: string; email: string }) {
  return apiFetch<AdminUser>(`/api/admin/users/${encodeURIComponent(userId)}`, {
    method: 'PUT',
    body: JSON.stringify(input),
  });
}

export function deleteAdminUser(userId: string) {
  return apiFetch<void>(`/api/admin/users/${encodeURIComponent(userId)}`, { method: 'DELETE' });
}

export function getAvailableRoles() {
  return apiFetch<string[]>('/api/roles');
}

export function assignRoles(userId: string, roles: string[]) {
  return apiFetch<string[]>(`/api/roles/users/${encodeURIComponent(userId)}`, {
    method: 'POST',
    body: JSON.stringify({ roles }),
  });
}

export function getMessagingDiagnostics() {
  return apiFetch<MessagingDiagnostics>('/api/admin/messaging');
}
