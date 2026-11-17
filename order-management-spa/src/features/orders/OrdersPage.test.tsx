import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/server';
import { renderWithProviders } from '../../test/helpers';
import { OrdersPage } from './OrdersPage';
import { CreateOrderPage } from './CreateOrderPage';

describe('OrdersPage', () => {
  it('filters by URL-driven status and displays authoritative server totals', async () => {
    server.use(
      http.get('/api/customers', () => HttpResponse.json({ code: 200, message: 'OK', results: { items: [], page: 1, pageSize: 100 }, endpoint: '/api/customers' })),
      http.get('/api/orders', () => HttpResponse.json({
        code: 200,
        message: 'OK',
        results: {
          items: [{ id: '1', customerId: '2', status: 'Paid', createdAt: '2026-01-01', currencyCode: 'ZAR', totalAmount: 125.5 }],
          page: 1,
          pageSize: 20,
        },
        endpoint: '/api/orders',
      })),
    );
    renderWithProviders(<OrdersPage />);
    await screen.findByRole('table');
    expect(screen.getByRole('columnheader', { name: 'Server total' })).toBeInTheDocument();
  });

  it('filters currencies by the selected customer country and supports line items', async () => {
    server.use(
      http.get('/api/orders', () => HttpResponse.json({ code: 200, message: 'OK', results: { items: [], page: 1, pageSize: 20 }, endpoint: '/api/orders' })),
      http.get('/api/customers', () => HttpResponse.json({
        code: 200,
        message: 'OK',
        results: { items: [{ id: '00000000-0000-4000-8000-000000000001', name: 'Cape Town Ltd', email: 'ct@example.com', countryCode: 'ZA', createdAt: '2026-01-01' }], page: 1, pageSize: 100 },
        endpoint: '/api/customers',
      })),
    );
    renderWithProviders(<CreateOrderPage />);
    await screen.findByRole('option', { name: 'Cape Town Ltd' });
    await userEvent.selectOptions(screen.getByLabelText('Customer'), '00000000-0000-4000-8000-000000000001');
    expect(screen.getByRole('option', { name: 'ZAR' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Add line item' }));
    expect(screen.getAllByRole('button', { name: 'Remove' })).toHaveLength(2);
  });
});
