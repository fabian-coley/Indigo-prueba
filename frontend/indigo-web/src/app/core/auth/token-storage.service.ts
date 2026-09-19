import { Injectable } from '@angular/core';
import { AuthSession } from '../models/auth.model';

const SESSION_KEY = 'indigo.session';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  read(): AuthSession | null {
    try {
      const raw = localStorage.getItem(SESSION_KEY);
      if (!raw) {
        return null;
      }
      const parsed = JSON.parse(raw) as Partial<AuthSession>;
      if (!parsed.token || !parsed.email || !Array.isArray(parsed.roles)) {
        return null;
      }
      return {
        token: parsed.token,
        expiresAt: parsed.expiresAt ?? '',
        email: parsed.email,
        roles: parsed.roles,
      };
    } catch {
      return null;
    }
  }

  write(session: AuthSession): void {
    try {
      localStorage.setItem(SESSION_KEY, JSON.stringify(session));
    } catch {
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(SESSION_KEY);
    } catch {
    }
  }
}
