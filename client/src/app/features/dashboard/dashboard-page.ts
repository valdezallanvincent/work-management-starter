import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { StatusBadge } from '../../shared/components/status-badge';
import { Dashboard } from '../../shared/models/dashboard.models';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';

@Component({
  selector: 'app-dashboard-page',
  imports: [PageHeader, StatePanel, StatusBadge, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  private readonly reload = signal(0);

  protected readonly state = toSignal(
    toObservable(this.reload).pipe(switchMap(() => withLoadState(this.http.get<Dashboard>('/api/v1/dashboard')))),
    { initialValue: { status: 'loading' } as LoadState<Dashboard> },
  );

  protected readonly data = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));
  protected readonly maxStatusCount = computed(() => Math.max(1, ...(this.data()?.byStatus.map((s) => s.count) ?? [])));

  protected readonly greeting = computed(() => `Signed in as ${this.auth.user()?.displayName ?? ''}`);

  protected retry(): void {
    this.reload.update((n) => n + 1);
  }

  protected barWidth(count: number): string {
    return `${Math.round((count / this.maxStatusCount()) * 100)}%`;
  }
}
