import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { apiFetch, setBearerToken } from '../api/client';

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
  login: (email: string, password: string) => Promise<void>;
  register: (firstName: string, lastName: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [roles, setRoles] = useState<string[]>([]);

  async function login(email: string, password: string) {
    const response = await apiFetch<AccessToken>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    setBearerToken(response.accessToken);
    setToken(response.accessToken);
    setRoles(response.roles);
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
    setRoles([]);
  }

  const isAdmin = roles.includes('Admin') || roles.includes('Orders.Admin');
  const value = useMemo(() => ({ token, roles, isAdmin, login, register, logout }), [token, roles, isAdmin]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
