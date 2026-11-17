import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../test/server';
import { renderWithProviders } from '../test/helpers';
import { LoginPage } from './LoginPage';
import { RegisterPage } from './RegisterPage';

describe('Auth pages', () => {
  it('renders the redesigned login page and account option', () => {
    renderWithProviders(<LoginPage />);
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create an account' })).toBeInTheDocument();
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
