import type { ApiResponse, ProblemDetailsError } from './types';

export class ApiError extends Error {
  public status: number;
  public details: ProblemDetailsError;

  constructor(
    message: string,
    status: number,
    details: ProblemDetailsError,
  ) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

let bearerToken: string | null = null;

export function setBearerToken(token: string | null) {
  bearerToken = token;
}

export function getBearerToken() {
  return bearerToken;
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  if (bearerToken) headers.set('Authorization', `Bearer ${bearerToken}`);
  if (!headers.has('X-Correlation-ID')) {
    headers.set('X-Correlation-ID', crypto.randomUUID());
  }
  if (init.method && init.method !== 'GET') {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(path, {
    ...init,
    headers,
    credentials: 'include',
    cache: init.method === 'GET' || !init.method ? 'no-store' : 'default',
  });

  let payload: ApiResponse<T> | ProblemDetailsError | null = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  if (!response.ok) {
    if (response.status === 401) setBearerToken(null);
    const details =
      payload && 'errors' in payload ? payload : { status: response.status };
    const message =
      (payload && 'message' in payload && payload.message) ||
      ('detail' in (payload ?? {}) ? (payload as ProblemDetailsError).detail : null) ||
      `Request failed with status ${response.status}`;
    throw new ApiError(message, response.status, details as ProblemDetailsError);
  }

  return (payload as ApiResponse<T>).results as T;
}

