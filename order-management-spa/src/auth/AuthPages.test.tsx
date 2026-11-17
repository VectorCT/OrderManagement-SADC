import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../test/server';
import { renderWithProviders } from '../test/helpers';
import { useAuth } from './AuthContext';
import { LoginPage } from './LoginPage';
import { RegisterPage } from './RegisterPage';

function AuthRoleProbe() {
  const { login, roles, isAdmin } = useAuth();
  return (
    <>
      <button type="button" onClick={() => void login('admin@example.com', 'password')}>Sign in</button>
      <p>{roles.join(', ')}</p>
      <p>{isAdmin ? 'Administrator' : 'Standard user'}</p>
    </>
  );
}

describe('Auth pages', () => {
  it('renders the redesigned login page and account option', () => {
    renderWithProviders(<LoginPage />);
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Development role')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create an account' })).toBeInTheDocument();
  });

  it('uses the roles returned by the API to identify administrators', async () => {
    let payload: unknown;
    server.use(http.post('/api/auth/login', async ({ request }) => {
      payload = await request.json();
      return HttpResponse.json({
        code: 200,
        message: 'Signed in',
        results: {
          accessToken: 'signed-token',
          tokenType: 'Bearer',
          expiresAtUtc: '2026-01-01T00:00:00Z',
          user: { id: 'user-id', email: 'admin@example.com', firstName: 'Admin', lastName: 'User' },
          roles: ['Orders.Admin'],
        },
        endpoint: '/api/auth/login',
      });
    }));
    renderWithProviders(<AuthRoleProbe />);

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => {
      expect(payload).toEqual({ email: 'admin@example.com', password: 'password' });
      expect(screen.getByText('Orders.Admin')).toBeInTheDocument();
      expect(screen.getByText('Administrator')).toBeInTheDocument();
    });
  });

  it('shows an actionable error when the API response is missing roles', async () => {
    server.use(http.post('/api/auth/login', () => HttpResponse.json({
      code: 200,
      message: 'Signed in',
      results: {
        accessToken: 'signed-token',
        tokenType: 'Bearer',
        expiresAtUtc: '2026-01-01T00:00:00Z',
        user: { id: 'user-id', email: 'user@example.com', firstName: 'Test', lastName: 'User' },
      },
      endpoint: '/api/auth/login',
    })));
    renderWithProviders(<LoginPage />);
    await userEvent.type(screen.getByLabelText('Email address'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The API did not return account roles. Restart the API with the latest version and sign in again.',
    );
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
  });

  it('submits account registration details', async () => {
    let payload: unknown;
    server.use(http.post('/api/auth/register', async ({ request }) => {
      payload = await request.json();
      return HttpResponse.json({ code: 201, message: 'Created', results: {}, endpoint: '/api/auth/register' }, { status: 201 });
    }));
    renderWithProviders(<RegisterPage />);
    await userEvent.type(screen.getByLabelText('First name'), 'Naledi');
    await userEvent.type(screen.getByLabelText('Last name'), 'Mokoena');
    await userEvent.type(screen.getByLabelText('Email address'), 'naledi@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'StrongPassword123!');
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }));
    await waitFor(() => expect(payload).toEqual({
      firstName: 'Naledi',
      lastName: 'Mokoena',
      email: 'naledi@example.com',
      password: 'StrongPassword123!',
    }));
  });
});
