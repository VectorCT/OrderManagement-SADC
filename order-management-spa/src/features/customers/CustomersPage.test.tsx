import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/server';
import { renderWithProviders } from '../../test/helpers';
import { CustomersPage } from './CustomersPage';
import { CreateCustomerPage } from './CreateCustomerPage';

describe('CustomersPage', () => {
  it('renders loading, populated, and debounced URL search states', async () => {
    server.use(http.get('/api/customers', ({ request }) => {
      const url = new URL(request.url);
      return HttpResponse.json({
        code: 200,
        message: 'Customers retrieved successfully.',
        results: {
          items: url.searchParams.get('search') === 'Acme'
            ? [{ id: '1', name: 'Acme', email: 'acme@example.com', countryCode: 'ZA', createdAt: '2026-01-01' }]
            : [],
          page: 1,
          pageSize: 20,
        },
        endpoint: '/api/customers',
      });
    }));

    renderWithProviders(<CustomersPage />);
    expect(screen.getByRole('link', { name: 'Add new customer' })).toHaveAttribute('href', '/customers/new');
    const search = screen.getByRole('textbox', { name: 'Search' });
    await userEvent.type(search, 'Acme');
    await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  });

  it('renders an empty state', async () => {
    server.use(http.get('/api/customers', () =>
      HttpResponse.json({ code: 200, message: 'OK', results: { items: [], page: 1, pageSize: 20 }, endpoint: '/api/customers' }),
    ));
    renderWithProviders(<CustomersPage />);
    expect(await screen.findByText('No customers found.')).toBeInTheDocument();
  });

  it('shows customer validation errors', async () => {
    renderWithProviders(<CreateCustomerPage />);
    await userEvent.clear(screen.getByLabelText('Name'));
    await userEvent.clear(screen.getByLabelText('Email'));
    await userEvent.click(screen.getByRole('button', { name: 'Create customer' }));
    expect(await screen.findByText('Customer name is required.')).toBeInTheDocument();
    expect(screen.getByText('A valid email address is required.')).toBeInTheDocument();
  });
});
