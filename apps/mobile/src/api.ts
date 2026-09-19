import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
import type { AuthResponse } from './types';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5151').replace(/\/$/, '');
const TOKEN_KEY = 'subclear.token';

type RequestOptions = {
  method?: string;
  body?: unknown;
  token?: string | null;
};

function problemMessage(payload: unknown, fallback: string): string {
  if (payload && typeof payload === 'object') {
    const record = payload as { detail?: string; title?: string; message?: string };
    return record.detail || record.title || record.message || fallback;
  }
  return fallback;
}

export async function getStoredToken(): Promise<string | null> {
  try {
    if (Platform.OS === 'web') {
      return globalThis.localStorage?.getItem(TOKEN_KEY) ?? null;
    }
    return await SecureStore.getItemAsync(TOKEN_KEY);
  } catch {
    return null;
  }
}

export async function storeToken(token: string | null): Promise<void> {
  try {
    if (Platform.OS === 'web') {
      if (token) {
        globalThis.localStorage?.setItem(TOKEN_KEY, token);
      } else {
        globalThis.localStorage?.removeItem(TOKEN_KEY);
      }
      return;
    }
    if (token) {
      await SecureStore.setItemAsync(TOKEN_KEY, token);
    } else {
      await SecureStore.deleteItemAsync(TOKEN_KEY);
    }
  } catch {
    // SecureStore is unavailable on some simulators; in-memory auth still works for the session.
  }
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json'
  };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (options.token) {
    headers.Authorization = `Bearer ${options.token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body)
    });
  } catch {
    throw new Error(`Cannot reach the SubClear API at ${API_URL}. Is it running?`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload = text ? (JSON.parse(text) as unknown) : null;

  if (!response.ok) {
    throw new Error(problemMessage(payload, `Request failed (${response.status})`));
  }

  return payload as T;
}

export async function loginRequest(email: string, password: string): Promise<AuthResponse> {
  return api<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: { email, password }
  });
}

export async function registerRequest(input: {
  organisationName: string;
  companyNumber?: string;
  fullName: string;
  email: string;
  password: string;
}): Promise<AuthResponse> {
  return api<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: input
  });
}
