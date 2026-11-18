import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError, apiFetch, setBearerToken } from '../api/client';

interface AccessToken {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: { id: string; email: string; firstName: string; lastName: string };
  roles: string[];
}

interface AuthValue {
  token: string | null;
  roles: string[];
  isAdmin: boolean;
  isInitializing: boolean;
  authError: string | null;
  restoreSession: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  register: (firstName: string, lastName: string, email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthValue | null>(null);
let pendingSessionRestore: Promise<AccessToken | null> | null = null;

function restoreAccessToken() {
  if (!pendingSessionRestore) {
    pendingSessionRestore = apiFetch<AccessToken | null>('/api/auth/refresh', { method: 'POST' })
      .then(validateAccessToken)
      .finally(() => {
        pendingSessionRestore = null;
      });
  }
  return pendingSessionRestore;
}

function validateAccessToken(response: AccessToken | null) {
  if (
    !response ||
    typeof response.accessToken !== 'string' ||
    !response.accessToken ||
    !Array.isArray(response.roles) ||
    !response.roles.every((role) => typeof role === 'string')
  ) {
    throw new Error('The API did not return account roles. Restart the API with the latest version and sign in again.');
  }
  return response;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [roles, setRoles] = useState<string[]>([]);
  const [isInitializing, setIsInitializing] = useState(true);
  const [authError, setAuthError] = useState<string | null>(null);

  const restoreSession = useCallback(async () => {
    setIsInitializing(true);
    setAuthError(null);
    try {
      const response = await restoreAccessToken();
      if (response) {
        setBearerToken(response.accessToken);
        setToken(response.accessToken);
        setRoles(response.roles);
      }
    } catch (error) {
      setBearerToken(null);
      setToken(null);
      setRoles([]);
      if (!(error instanceof ApiError && error.status === 401)) {
        setAuthError(error instanceof Error ? error.message : 'Unable to restore your session.');
      }
    } finally {
      setIsInitializing(false);
    }
  }, []);

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

  async function login(email: string, password: string) {
    const response = validateAccessToken(await apiFetch<AccessToken | null>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }));

    setBearerToken(response.accessToken);
    setToken(response.accessToken);
    setRoles(response.roles);
    setAuthError(null);
    setIsInitializing(false);
  }

  async function register(firstName: string, lastName: string, email: string, password: string) {
    await apiFetch<unknown>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ firstName, lastName, email, password }),
    });
  }

  async function logout() {
    try {
      await apiFetch<unknown>('/api/auth/logout', { method: 'POST' });
    } finally {
      setBearerToken(null);
      setToken(null);
      setRoles([]);
      setAuthError(null);
      setIsInitializing(false);
    }
  }

  const isAdmin = roles.includes('Admin') || roles.includes('Orders.Admin');
  const value = useMemo(() => ({
    token,
    roles,
    isAdmin,
    isInitializing,
    authError,
    restoreSession,
    login,
    register,
    logout,
  }), [token, roles, isAdmin, isInitializing, authError, restoreSession]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
