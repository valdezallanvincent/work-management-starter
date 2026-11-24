import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from '../authentication/auth.service';
import { authGuard, roleGuard } from './auth.guards';

describe('route guards', () => {
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    auth = TestBed.inject(AuthService);
  });

  const run = (guard: typeof authGuard, url = '/projects') =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));

  it('redirects anonymous users to login and keeps the requested URL', () => {
    const result = run(authGuard) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Fprojects');
  });

  it('allows authenticated users', () => {
    vi.spyOn(auth, 'isAuthenticated').mockReturnValue(true);
    expect(run(authGuard)).toBe(true);
  });

  it('sends users without the required role back to the dashboard', () => {
    vi.spyOn(auth, 'hasRole').mockReturnValue(false);
    const result = run(roleGuard('Admin')) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/dashboard');
  });
});
