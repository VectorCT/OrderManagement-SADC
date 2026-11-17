import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '../../test/helpers';
import { server } from '../../test/server';
import { AdminUsersPage } from './AdminUsersPage';

describe('AdminUsersPage', () => {
  it('allows an administrator to grant a role to a registered user', async () => {
    let assignedRoles: unknown;
    server.use(
      http.get('/api/admin/users', () => HttpResponse.json({
        code: 200,
        message: 'Users retrieved',
        results: {
          items: [{
            id: 'user-id',
            email: 'new.user@example.com',
            firstName: 'New',
            lastName: 'User',
            createdAt: '2026-01-01T00:00:00Z',
            roles: [],
          }],
          page: 1,
          pageSize: 100,
        },
        endpoint: '/api/admin/users',
      })),
      http.get('/api/roles', () => HttpResponse.json({
        code: 200,
        message: 'Roles retrieved',
        results: ['Orders.Read', 'Orders.Write'],
        endpoint: '/api/roles',
      })),
      http.post('/api/roles/users/user-id', async ({ request }) => {
        assignedRoles = await request.json();
        return HttpResponse.json({
          code: 200,
          message: 'Roles assigned',
          results: ['Orders.Read'],
          endpoint: '/api/roles/users/user-id',
        });
      }),
    );
    renderWithProviders(<AdminUsersPage />);

    const roleSelect = await screen.findByRole('combobox', { name: 'Role for new.user@example.com' });
    await userEvent.selectOptions(roleSelect, 'Orders.Read');
    await userEvent.click(screen.getByRole('button', { name: 'Grant' }));

    await waitFor(() => expect(assignedRoles).toEqual({ roles: ['Orders.Read'] }));
    expect(await screen.findByText('Role assigned successfully.')).toBeInTheDocument();
  });
});
