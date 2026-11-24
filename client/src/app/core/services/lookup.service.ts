import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, shareReplay, throwError } from 'rxjs';
import { ProjectLookup } from '../../shared/models/project.models';
import { UserLookup } from '../../shared/models/user.models';

/**
 * Reference data for pickers. The server caches these lists; the client keeps one copy per
 * session and drops it when the underlying data is changed from this client.
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private projects$?: Observable<ProjectLookup[]>;
  private users$?: Observable<UserLookup[]>;

  projects(): Observable<ProjectLookup[]> {
    return (this.projects$ ??= this.http.get<ProjectLookup[]>('/api/v1/projects/lookup').pipe(
      shareReplay(1),
      // shareReplay would otherwise replay a failure for the rest of the session.
      catchError((error: unknown) => {
        this.projects$ = undefined;
        return throwError(() => error);
      }),
    ));
  }

  users(): Observable<UserLookup[]> {
    return (this.users$ ??= this.http.get<UserLookup[]>('/api/v1/users/lookup').pipe(
      shareReplay(1),
      catchError((error: unknown) => {
        this.users$ = undefined;
        return throwError(() => error);
      }),
    ));
  }

  invalidateProjects(): void {
    this.projects$ = undefined;
  }

  invalidateUsers(): void {
    this.users$ = undefined;
  }
}
