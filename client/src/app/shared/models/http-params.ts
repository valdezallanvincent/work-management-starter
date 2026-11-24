import { HttpParams } from '@angular/common/http';

type ParamValue = string | number | boolean | readonly string[] | null | undefined;

/** Builds query parameters, skipping empty values so URLs stay short and cache-friendly. */
export function toHttpParams(values: Record<string, ParamValue>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined || value === '' || value === false) {
      continue;
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        params = params.append(key, item);
      }
    } else {
      params = params.set(key, String(value));
    }
  }
  return params;
}
