import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';

export const server = setupServer(
  http.post('/api/auth/refresh', () => HttpResponse.json({
    code: 401,
    message: 'The refresh session is missing, expired, or invalid.',
    results: null,
    endpoint: '/api/auth/refresh',
  }, { status: 401 })),
);
