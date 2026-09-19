import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api, getStoredToken, loginRequest, registerRequest, storeToken } from './api';
import type { EntitlementsDto, MeResponse } from './types';

type AuthContextValue = {
  token: string | null;
  user: MeResponse | null;
  ready: boolean;
  canWrite: boolean;
  canReview: boolean;
  entitlements: EntitlementsDto | null;
  login: (email: string, password: string) => Promise<void>;
  register: (input: {
    organisationName: string;
    companyNumber?: string;
    fullName: string;
    email: string;
    password: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
  request: <T>(path: string, options?: { method?: string; body?: unknown }) => Promise<T>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<MeResponse | null>(null);
  const [entitlements, setEntitlements] = useState<EntitlementsDto | null>(null);
  const [ready, setReady] = useState(false);

  const loadEntitlements = useCallback(async (nextToken: string) => {
    try {
      setEntitlements(await api<EntitlementsDto>('/api/billing/entitlements', { token: nextToken }));
    } catch {
      setEntitlements(null);
    }
  }, []);

  const applyAuth = useCallback(async (nextToken: string, nextUser?: MeResponse) => {
    await storeToken(nextToken);
    setToken(nextToken);
    if (nextUser) {
      setUser(nextUser);
    } else {
      const me = await api<MeResponse>('/api/me', { token: nextToken });
      setUser(me);
    }
    await loadEntitlements(nextToken);
  }, [loadEntitlements]);

    useEffect(() => {
    let cancelled = false;
    (async () => {
      const stored = await getStoredToken();
      if (!stored) {
        if (!cancelled) {
          setReady(true);
        }
        return;
      }
      try {
        const me = await api<MeResponse>('/api/me', { token: stored });
        if (!cancelled) {
          setToken(stored);
          setUser(me);
        }
        if (!cancelled) {
          await loadEntitlements(stored);
        }
      } catch {
        await storeToken(null);
      } finally {
        if (!cancelled) {
          setReady(true);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [loadEntitlements]);

  const login = useCallback(
    async (email: string, password: string) => {
      const result = await loginRequest(email, password);
      await applyAuth(result.token, result.user);
    },
    [applyAuth]
  );

  const register = useCallback(
    async (input: {
      organisationName: string;
      companyNumber?: string;
      fullName: string;
      email: string;
      password: string;
    }) => {
      const result = await registerRequest(input);
      await applyAuth(result.token, result.user);
    },
    [applyAuth]
  );

  const logout = useCallback(async () => {
    await storeToken(null);
    setToken(null);
    setUser(null);
    setEntitlements(null);
  }, []);

  const request = useCallback(
    async <T,>(path: string, options?: { method?: string; body?: unknown }) => {
      if (!token) {
        throw new Error('You are not signed in.');
      }
      try {
        return await api<T>(path, { ...options, token });
      } catch (error) {
        const message = error instanceof Error ? error.message : '';
        if (message.toLowerCase().includes('unauthorised') || message.includes('(401)')) {
          await logout();
        }
        throw error;
      }
    },
    [logout, token]
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      user,
      ready,
      canWrite: user?.role === 'owner' || user?.role === 'admin' || user?.role === 'contractsManager',
      canReview: user?.role === 'owner' || user?.role === 'admin' || user?.role === 'reviewer',
      entitlements,
      login,
      register,
      logout,
      request
    }),
    [token, user, ready, entitlements, login, register, logout, request]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
