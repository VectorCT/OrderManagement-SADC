import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../../test/server';
import { renderWithProviders } from '../../test/helpers';
import { OrderDetailPage } from './OrderDetailPage';

describe('OrderDetailPage', () => {
  it('offers only valid actions and requires confirmation for cancellation', async () => {
    const order = {
      id: '00000000-0000-4000-8000-000000000010',
      customerId: '00000000-0000-4000-8000-000000000001',
      status: 'Pending',
      createdAt: '2026-01-01',
      currencyCode: 'ZAR',
      totalAmount: 100,
      lineItems: [{ id: '1', orderId: '10', productSku: 'SKU-1', quantity: 2, unitPrice: 50 }],
      rowVersion: '',
      notes: null,
      updatedAt: null,
    };
    server.use(
      http.get('/api/orders/:id', () => HttpResponse.json({ code: 200, message: 'OK', results: order, endpoint: '/api/orders/10' })),
      http.get('/api/customers/:id', () => HttpResponse.json({ code: 200, message: 'OK', results: { id: order.customerId, name: 'Acme', email: 'a@b.com', countryCode: 'ZA', createdAt: '2026-01-01' }, endpoint: '/api/customers/1' })),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    renderWithProviders(<OrderDetailPage id="00000000-0000-4000-8000-000000000010" />);
    await screen.findByRole('heading', { name: /Order 00000000/ });
    expect(screen.getByRole('button', { name: 'Mark paid' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Mark fulfilled' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel order' }));
    expect(window.confirm).toHaveBeenCalled();
  });
});
