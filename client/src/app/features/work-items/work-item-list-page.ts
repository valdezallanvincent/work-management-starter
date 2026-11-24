import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { combineLatest, debounceTime, distinctUntilChanged, filter, map, switchMap } from 'rxjs';
import { LookupService } from '../../core/services/lookup.service';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { PagedResult } from '../../shared/models/api.models';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';
import {
  WORK_ITEM_PRIORITIES,
  WORK_ITEM_STATUSES,
  WorkItemPriority,
  WorkItemStatus,
  WorkItemSummary,
} from '../../shared/models/work-item.models';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { WorkItemFormDialog } from './work-item-form-dialog';
import { WorkItemTable } from './work-item-table';
import { WorkItemQuery, WorkItemsService } from './work-items.service';

/** The URL is the single source of truth for filters, so views can be bookmarked and shared. */
function toQuery(params: ParamMap): WorkItemQuery {
  const statuses = params.getAll('status').filter((s): s is WorkItemStatus => (WORK_ITEM_STATUSES as readonly string[]).includes(s));
  const priority = params.get('priority');
  return {
    page: Number(params.get('page')) || 1,
    pageSize: Number(params.get('pageSize')) || 20,
    sortBy: params.get('sortBy') ?? undefined,
    sortDirection: params.get('sortDirection') === 'Ascending' ? 'Ascending' : 'Descending',
    search: params.get('search') ?? undefined,
    projectId: params.get('projectId') ?? undefined,
    status: statuses.length ? statuses : undefined,
    priority: (WORK_ITEM_PRIORITIES as readonly string[]).includes(priority ?? '') ? (priority as WorkItemPriority) : undefined,
    assignedToMe: params.get('assignedToMe') === 'true',
    overdue: params.get('overdue') === 'true',
  };
}

@Component({
  selector: 'app-work-item-list-page',
  imports: [
    ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, MatButtonModule,
    PageHeader, StatePanel, EnumLabelPipe, WorkItemTable,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-item-list-page.html',
})
export class WorkItemListPage {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly workItems = inject(WorkItemsService);
  private readonly dialog = inject(MatDialog);

  protected readonly statuses = WORK_ITEM_STATUSES;
  protected readonly priorities = WORK_ITEM_PRIORITIES;
  protected readonly projects = toSignal(inject(LookupService).projects(), { initialValue: [] });

  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(toQuery)), { requireSync: true });
  protected readonly searchControl = new FormControl(this.query().search ?? '', { nonNullable: true });

  private readonly reloadCount = signal(0);
  protected readonly state = toSignal(
    combineLatest([toObservable(this.query), toObservable(this.reloadCount)]).pipe(
      switchMap(([query]) => withLoadState(this.workItems.search(query))),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<WorkItemSummary>> },
  );
  protected readonly result = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));
  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!(q.search || q.projectId || q.status || q.priority || q.assignedToMe || q.overdue);
  });

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), map((v) => v.trim()), distinctUntilChanged())
      .subscribe((search) => this.update({ search: search || null }));
  }

  protected update(changes: Params): void {
    // Any filter change returns to the first page; paging itself passes an explicit page.
    void this.router.navigate([], { queryParams: { page: null, ...changes }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected sort(sort: Sort): void {
    this.update({ sortBy: sort.active, sortDirection: sort.direction === 'asc' ? 'Ascending' : 'Descending' });
  }

  protected page(event: PageEvent): void {
    this.update({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    void this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  protected reload(): void {
    this.reloadCount.update((n) => n + 1);
  }

  protected create(): void {
    this.dialog
      .open(WorkItemFormDialog, { data: { projectId: this.query().projectId }, width: '600px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe((item) => void this.router.navigate(['/work-items', item.id]));
  }
}
