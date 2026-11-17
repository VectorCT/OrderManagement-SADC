import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import App from './App';
import { AuthProvider } from './auth/AuthContext';

describe('App', () => {
  it('renders the application shell', () => {
    render(<MemoryRouter><AuthProvider><App /></AuthProvider></MemoryRouter>);
    expect(screen.getByRole('main')).toBeInTheDocument();
  });
});
