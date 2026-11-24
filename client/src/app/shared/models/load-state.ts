import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, startWith } from 'rxjs';
import { ProblemDetails } from './api.models';

/**
 * One value that describes a request in flight, its result or its failure, so templates
 * never juggle separate loading / error / data flags that can disagree with each other.
 */
export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'loaded'; data: T }
  | { status: 'error'; message: string };

export function withLoadState<T>(source: Observable<T>): Observable<LoadState<T>> {
  return source.pipe(
    map((data) => ({ status: 'loaded', data }) as const),
    catchError((error: unknown) => of({ status: 'error', message: errorMessage(error) } as const)),
    startWith({ status: 'loading' } as const),
  );
}

export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'The server could not be reached. Check your connection and try again.';
    }
    const problem = error.error as ProblemDetails | null;
    if (problem?.errors) {
      return Object.values(problem.errors).flat().join(' ');
    }
    return problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`;
  }
  return 'Something went wrong.';
}

export function problemCode(error: unknown): string | undefined {
  return error instanceof HttpErrorResponse ? (error.error as ProblemDetails | null)?.code : undefined;
}

export function loadedData<T>(state: LoadState<T>): T | null {
  return state.status === 'loaded' ? state.data : null;
}

export function loadError<T>(state: LoadState<T>): string | null {
  return state.status === 'error' ? state.message : null;
}
