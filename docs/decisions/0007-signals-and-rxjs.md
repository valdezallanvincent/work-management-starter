# 0007. Use Angular Signals for state and RxJS for asynchronous streams

Status: accepted

## Context

Angular 21 applications are zoneless by default, and Signals are the primitive change detection
is built on. HTTP calls, debounced input and router events are still naturally expressed as
Observables. Both are needed; the question is where each belongs.

## Decision

- **Signals** hold state that templates read: the current session, the current query on a list
  page, a dialog's saving flag, derived values with `computed`.
- **RxJS** handles anything over time: HTTP requests, debouncing search input, cancelling a
  superseded request with `switchMap`, sharing one in-flight token refresh between concurrent
  401 responses with `shareReplay`.
- They meet at the edges: `toObservable(querySignal).pipe(switchMap(...))` turns state changes
  into requests, and `toSignal(...)` turns the result back into state for the template.
- `effect` is used only to synchronise with something outside Angular's templates (the
  `*appHasRole` directive creating and clearing views), not to copy one signal into another.
- No NgRx store. State is local to features or held in a few root services.

## Alternatives considered

- **RxJS only** with the `async` pipe. Works, and is how most existing Angular code is written,
  but derived state needs `combineLatest` chains and subscription management, and it fits less
  naturally with zoneless change detection.
- **Signals only**, including `resource` or `httpResource` for data. Attractive and likely the
  future direction. Cancellation, debouncing and request sharing are still clearer with RxJS
  operators today, and RxJS is what `HttpClient` returns.
- **NgRx Store.** Valuable when many distant components share and mutate the same state, or
  when time-travel debugging and strict action logs matter. Here each screen owns its data, so a
  global store would add boilerplate without solving a problem.

## Consequences

- Templates read signals only, which keeps them simple and makes the zoneless change detection
  predictable.
- Developers need to be comfortable with both models and with the interop functions.
- If the Signals-based resource APIs mature further, the list pages are the natural place to
  adopt them; their state is already a single `LoadState` signal.
