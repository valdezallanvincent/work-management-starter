import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/** Consistent loading, empty and error placeholders for lists and detail pages. */
@Component({
  selector: 'app-state-panel',
  imports: [MatButtonModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (kind()) {
      @case ('loading') {
        <div class="panel" role="status" aria-live="polite">
          <mat-spinner diameter="28" />
          <span class="sr-only">Loading</span>
        </div>
      }
      @case ('empty') {
        <div class="panel">
          <p class="title">{{ message() || 'Nothing to show yet.' }}</p>
          <ng-content />
        </div>
      }
      @case ('error') {
        <div class="panel panel--error" role="alert">
          <p class="title">{{ message() || 'This could not be loaded.' }}</p>
          <button mat-stroked-button type="button" (click)="retry.emit()">Try again</button>
        </div>
      }
    }
  `,
  styles: `
    .panel { display: flex; flex-direction: column; align-items: center; gap: 12px; padding: 40px 16px; color: var(--app-text-muted); text-align: center; }
    .panel--error .title { color: var(--app-error); }
    .title { margin: 0; font-size: 14px; }
  `,
})
export class StatePanel {
  readonly kind = input.required<'loading' | 'empty' | 'error'>();
  readonly message = input<string | null>(null);
  readonly retry = output();
}
