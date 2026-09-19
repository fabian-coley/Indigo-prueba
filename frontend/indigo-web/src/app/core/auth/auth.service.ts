import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthSession, LoginRequest, RegisterRequest, Rol } from '../models/auth.model';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private readonly session = signal<AuthSession | null>(this.storage.read());

  readonly currentUser = this.session.asReadonly();
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.hasRole('Admin'));
  readonly token = computed(() => this.session()?.token ?? null);
  readonly rolPrincipal = computed<Rol | null>(() => this.session()?.roles[0] ?? null);

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${this.baseUrl}/login`, request)
      .pipe(tap((session) => this.setSession(session)));
  }

  register(request: RegisterRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/register`, request);
  }

  logout(): void {
    this.storage.clear();
    this.session.set(null);
  }

  hasRole(rol: Rol): boolean {
    return this.session()?.roles.includes(rol) ?? false;
  }

  private setSession(session: AuthSession): void {
    this.storage.write(session);
    this.session.set(session);
  }
}
