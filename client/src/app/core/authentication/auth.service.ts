import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { UserRole } from '../../shared/models/api.models';
import { AccessTokenResponse, CurrentUser } from './auth.models';

/**
 * Holds the session in memory only. The access token is never written to localStorage, so an
 * XSS payload cannot read it from storage after the fact. The refresh token is an HttpOnly cookie
 * managed by the browser; this service never sees it. A page reload restores the session by
 * calling the refresh endpoint once at startup.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<AccessTokenResponse | null>(null);
  private refreshInFlight: Observable<string | null> | null = null;

  readonly user = computed<CurrentUser | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http.post<AccessTokenResponse>('/api/v1/auth/login', { email, password }).pipe(
      tap((response) => this.session.set(response)),
      map((response) => response.user),
    );
  }

  /**
   * Concurrent 401s share one refresh request; rotating the same refresh token twice would trip
   * the server's reuse detection and end the session.
   */
  refresh(): Observable<string | null> {
    this.refreshInFlight ??= this.http.post<AccessTokenResponse>('/api/v1/auth/refresh', {}).pipe(
      tap((response) => this.session.set(response)),
      map((response) => response.accessToken),
      catchError(() => {
        this.session.set(null);
        return of(null);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshInFlight;
  }

  /** Used at startup: a missing or expired cookie simply means "not signed in". */
  restoreSession(): Observable<unknown> {
    return this.refresh();
  }

  logout(): void {
    this.http.post('/api/v1/auth/logout', {}).pipe(catchError(() => of(null))).subscribe();
    this.endSession();
  }

  endSession(returnUrl?: string): void {
    this.session.set(null);
    void this.router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
  }

  updateDisplayName(displayName: string): void {
    this.session.update((current) => (current ? { ...current, user: { ...current.user, displayName } } : current));
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.user()?.role;
    return role !== undefined && roles.includes(role);
  }
}
