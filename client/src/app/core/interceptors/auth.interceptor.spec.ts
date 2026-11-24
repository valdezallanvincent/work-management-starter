import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../authentication/auth.service';
import { authInterceptor } from './auth.interceptor';

const session = (token: string) => ({
  accessToken: token,
  expiresAtUtc: '2030-01-01T00:00:00Z',
  user: { id: 'u1', email: 'a@b.test', displayName: 'Ada', role: 'Member' },
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => backend.verify());

  function signIn(token = 'token-1'): void {
    auth.login('a@b.test', 'password').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session(token));
  }

  it('adds the bearer token to API requests', () => {
    signIn();
    http.get('/api/v1/projects').subscribe();
    expect(backend.expectOne('/api/v1/projects').request.headers.get('Authorization')).toBe('Bearer token-1');
  });

  it('never sends the token to other origins', () => {
    signIn();
    http.get('https://example.com/data').subscribe();
    expect(backend.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });

  it('refreshes once on 401 and replays the request with the new token', () => {
    signIn();
    let body: unknown;
    http.get('/api/v1/projects').subscribe((result) => (body = result));

    backend.expectOne('/api/v1/projects').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush(session('token-2'));
    const retry = backend.expectOne('/api/v1/projects');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
  });

  it('shares a single refresh between concurrent 401 responses', () => {
    signIn();
    http.get('/api/v1/projects').subscribe();
    http.get('/api/v1/dashboard').subscribe();

    backend.expectOne('/api/v1/projects').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/dashboard').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Rotating the same refresh token twice would trigger the server's reuse detection.
    backend.expectOne('/api/v1/auth/refresh').flush(session('token-2'));
    backend.expectOne('/api/v1/projects').flush({});
    backend.expectOne('/api/v1/dashboard').flush({});
  });

  it('ends the session when the refresh fails', () => {
    signIn();
    const endSession = vi.spyOn(auth, 'endSession');
    let failed = false;
    http.get('/api/v1/projects').subscribe({ error: () => (failed = true) });

    backend.expectOne('/api/v1/projects').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(endSession).toHaveBeenCalled();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('does not try to refresh when an auth endpoint itself returns 401', () => {
    let failed = false;
    auth.login('a@b.test', 'wrong').subscribe({ error: () => (failed = true) });
    backend.expectOne('/api/v1/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectNone('/api/v1/auth/refresh');
    expect(failed).toBe(true);
  });
});
