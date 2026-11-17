import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { apiFetch, setBearerToken } from '../api/client';

export type DevelopmentRole = 'Orders.Read' | 'Orders.Write' | 'Orders.Admin';

interface AccessToken {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: { id: string; email: string; firstName: string; lastName: string };
}

interface AuthValue {
  token: string | null;
  role: DevelopmentRole | null;
  login: (email: string, password: string, role: DevelopmentRole) => Promise<void>;
  register: (firstName: string, lastName: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<DevelopmentRole | null>(null);

  async function login(email: string, password: string, requestedRole: DevelopmentRole) {
    const response = await apiFetch<AccessToken>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    setBearerToken(response.accessToken);
    setToken(response.accessToken);
    setRole(requestedRole);
  }

  async function register(firstName: string, lastName: string, email: string, password: string) {
    await apiFetch<unknown>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ firstName, lastName, email, password }),
    });
  }

  function logout() {
    setBearerToken(null);
    setToken(null);
    setRole(null);
  }

  const value = useMemo(() => ({ token, role, login, register, logout }), [token, role]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
