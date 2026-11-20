import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, User } from './models';

const SESSION_KEY = 'claims-portal.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentSession = signal<AuthResponse | null>(this.readSession());
  readonly session = this.currentSession.asReadonly();
  readonly user = signal<User | null>(this.currentSession()?.user ?? null);

  constructor(private readonly http: HttpClient) {}

  get accessToken(): string | null {
    const session = this.currentSession();
    if (!session || new Date(session.expiresAt).getTime() <= Date.now()) {
      this.logout();
      return null;
    }

    return session.accessToken;
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, { email, password })
      .pipe(tap((response) => this.saveSession(response)));
  }

  register(fullName: string, email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/register`, { fullName, email, password })
      .pipe(tap((response) => this.saveSession(response)));
  }

  logout(): void {
    this.currentSession.set(null);
    this.user.set(null);
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(SESSION_KEY);
    }
  }

  private saveSession(session: AuthResponse): void {
    this.currentSession.set(session);
    this.user.set(session.user);
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    }
  }

  private readSession(): AuthResponse | null {
    if (typeof sessionStorage === 'undefined') {
      return null;
    }

    try {
      const value = sessionStorage.getItem(SESSION_KEY);
      if (!value) {
        return null;
      }

      const session = JSON.parse(value) as AuthResponse;
      return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
  }
}
