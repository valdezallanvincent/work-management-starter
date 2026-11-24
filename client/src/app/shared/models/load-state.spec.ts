import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, of, throwError, toArray } from 'rxjs';
import { errorMessage, problemCode, withLoadState } from './load-state';

describe('load state helpers', () => {
  it('emits loading then the loaded value', async () => {
    const states = await firstValueFrom(withLoadState(of(42)).pipe(toArray()));
    expect(states).toEqual([{ status: 'loading' }, { status: 'loaded', data: 42 }]);
  });

  it('turns an error into an error state instead of erroring the stream', async () => {
    const error = new HttpErrorResponse({ status: 422, error: { detail: 'Archived projects are read-only.' } });
    const states = await firstValueFrom(withLoadState(throwError(() => error)).pipe(toArray()));
    expect(states.at(-1)).toEqual({ status: 'error', message: 'Archived projects are read-only.' });
  });

  it('joins validation errors from problem details', () => {
    const error = new HttpErrorResponse({ status: 400, error: { errors: { Name: ['Name is required.'], Key: ['Key is invalid.'] } } });
    expect(errorMessage(error)).toBe('Name is required. Key is invalid.');
  });

  it('explains network failures and exposes the stable problem code', () => {
    expect(errorMessage(new HttpErrorResponse({ status: 0 }))).toContain('could not be reached');
    expect(problemCode(new HttpErrorResponse({ status: 409, error: { code: 'concurrency_conflict' } }))).toBe('concurrency_conflict');
  });
});
