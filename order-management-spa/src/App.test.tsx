import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import App from './App';

describe('App', () => {
  it('renders the application shell', () => {
    render(<MemoryRouter><App /></MemoryRouter>);
    expect(screen.getByRole('link', { name: 'Order Management · SADC' })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Primary navigation' })).toBeInTheDocument();
  });
});
