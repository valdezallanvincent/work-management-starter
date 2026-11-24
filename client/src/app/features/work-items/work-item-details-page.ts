import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Observable, combineLatest, filter, switchMap } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';
import { FeedbackService } from '../../core/services/feedback.service';
import { LookupService } from '../../core/services/lookup.service';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { StatusBadge } from '../../shared/components/status-badge';
import { LoadState, loadError, loadedData, problemCode, withLoadState } from '../../shared/models/load-state';
import { WORK_ITEM_PRIORITIES, WorkItemDetails, WorkItemPriority, WorkItemStatus } from '../../shared/models/work-item.models';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { WorkItemComments } from './work-item-comments';
import { WorkItemFormDialog } from './work-item-form-dialog';
import { WorkItemsService } from './work-items.service';

const TRANSITION_LABELS: Record<WorkItemStatus, string> = {
  Open: 'Move back to open',
  InProgress: 'Start work',
  InReview: 'Send to review',
  Completed: 'Complete',
  Cancelled: 'Cancel item',
};

@Component({
  selector: 'app-work-item-details-page',
  imports: [
    DatePipe, RouterLink, MatButtonModule, MatFormFieldModule, MatSelectModule, PageHeader, StatePanel, StatusBadge,
    EnumLabelPipe, WorkItemComments,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-item-details-page.html',
  styleUrl: './work-item-details-page.scss',
})
export class WorkItemDetailsPage {
  private readonly workItems = inject(WorkItemsService);
  private readonly dialog = inject(MatDialog);
  private readonly feedback = inject(FeedbackService);
  private readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly priorities = WORK_ITEM_PRIORITIES;
  protected readonly users = toSignal(inject(LookupService).users(), { initialValue: [] });
  protected readonly busy = signal(false);

  private readonly reloadCount = signal(0);
  // Successful mutations return the updated item, which replaces the loaded one without a refetch.
  private readonly updated = signal<WorkItemDetails | null>(null);

  private readonly state = toSignal(
    combineLatest([toObservable(this.id), toObservable(this.reloadCount)]).pipe(
      switchMap(([id]) => withLoadState(this.workItems.get(id))),
    ),
    { initialValue: { status: 'loading' } as LoadState<WorkItemDetails> },
  );

  protected readonly item = computed(() => {
    const loaded = loadedData(this.state());
    const updated = this.updated();
    return updated && loaded && updated.id === loaded.id ? updated : loaded;
  });
  protected readonly error = computed(() => loadError(this.state()));

  protected readonly readOnly = computed(() => {
    const item = this.item();
    return !item || item.projectIsArchived;
  });
  protected readonly isClosed = computed(() => ['Completed', 'Cancelled'].includes(this.item()?.status ?? ''));

  protected readonly breadcrumbs = computed(() => {
    const item = this.item();
    return [
      { label: 'Work items', link: '/work-items' },
      ...(item ? [{ label: item.projectKey, link: `/projects/${item.projectId}` }] : []),
      { label: item?.title ?? '...' },
    ];
  });

  protected transitionLabel(status: WorkItemStatus): string {
    return status === 'Open' && this.isClosed() ? 'Reopen' : TRANSITION_LABELS[status];
  }

  protected changeStatus(item: WorkItemDetails, status: WorkItemStatus): void {
    this.run(this.workItems.changeStatus(item, status), `Moved to ${status.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()}.`);
  }

  protected changePriority(item: WorkItemDetails, priority: WorkItemPriority): void {
    this.run(this.workItems.changePriority(item, priority), 'Priority updated.');
  }

  protected assign(item: WorkItemDetails, assigneeId: string | null): void {
    this.run(this.workItems.assign(item, assigneeId), assigneeId ? 'Assignee updated.' : 'Unassigned.');
  }

  protected assignToMe(item: WorkItemDetails): void {
    this.assign(item, this.auth.user()?.id ?? null);
  }

  protected edit(item: WorkItemDetails): void {
    this.dialog
      .open(WorkItemFormDialog, { data: { existing: item }, width: '600px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe((saved) => {
        this.updated.set(saved);
        this.feedback.success('Work item updated.');
      });
  }

  protected reload(): void {
    this.updated.set(null);
    this.reloadCount.update((n) => n + 1);
  }

  private run(request: Observable<WorkItemDetails>, successMessage: string): void {
    this.busy.set(true);
    request.subscribe({
      next: (saved) => {
        this.busy.set(false);
        this.updated.set(saved);
        this.feedback.success(successMessage);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.feedback.error(error);
        // Someone else changed the item: show the current version rather than the stale one.
        if (problemCode(error) === 'concurrency_conflict') {
          this.reload();
        }
      },
    });
  }
}
