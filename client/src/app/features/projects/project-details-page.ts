import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { combineLatest, filter, switchMap } from 'rxjs';
import { FeedbackService } from '../../core/services/feedback.service';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/components/confirm-dialog';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { StatusBadge } from '../../shared/components/status-badge';
import { PagedResult } from '../../shared/models/api.models';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';
import { ProjectDetails } from '../../shared/models/project.models';
import { WORK_ITEM_STATUSES, WorkItemSummary } from '../../shared/models/work-item.models';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { WorkItemFormDialog } from '../work-items/work-item-form-dialog';
import { WorkItemTable } from '../work-items/work-item-table';
import { WorkItemQuery, WorkItemsService } from '../work-items/work-items.service';
import { ProjectFormDialog } from './project-form-dialog';
import { ProjectsService } from './projects.service';

@Component({
  selector: 'app-project-details-page',
  imports: [DatePipe, MatButtonModule, PageHeader, StatePanel, StatusBadge, EnumLabelPipe, WorkItemTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-details-page.html',
  styleUrl: './project-details-page.scss',
})
export class ProjectDetailsPage {
  private readonly projects = inject(ProjectsService);
  private readonly workItems = inject(WorkItemsService);
  private readonly dialog = inject(MatDialog);
  private readonly feedback = inject(FeedbackService);

  /** Bound from the route parameter via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly statuses = WORK_ITEM_STATUSES;
  private readonly reloadProject = signal(0);
  protected readonly itemQuery = signal<Omit<WorkItemQuery, 'projectId'>>({ page: 1, pageSize: 10 });

  protected readonly state = toSignal(
    combineLatest([toObservable(this.id), toObservable(this.reloadProject)]).pipe(
      switchMap(([id]) => withLoadState(this.projects.get(id))),
    ),
    { initialValue: { status: 'loading' } as LoadState<ProjectDetails> },
  );
  protected readonly project = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));

  protected readonly items = toSignal(
    combineLatest([toObservable(this.id), toObservable(this.itemQuery), toObservable(this.reloadProject)]).pipe(
      switchMap(([projectId, query]) => withLoadState(this.workItems.search({ ...query, projectId }))),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<WorkItemSummary>> },
  );
  protected readonly itemResult = computed(() => loadedData(this.items()));

  protected readonly breadcrumbs = computed(() => [
    { label: 'Projects', link: '/projects' },
    { label: this.project()?.key ?? '...' },
  ]);

  protected reload(): void {
    this.reloadProject.update((n) => n + 1);
  }

  protected edit(project: ProjectDetails): void {
    this.dialog
      .open(ProjectFormDialog, { data: project, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.feedback.success('Project updated.');
        this.reload();
      });
  }

  protected newWorkItem(project: ProjectDetails): void {
    this.dialog
      .open(WorkItemFormDialog, { data: { projectId: project.id }, width: '600px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.feedback.success('Work item created.');
        this.reload();
      });
  }

  protected toggleArchive(project: ProjectDetails): void {
    const archiving = project.status === 'Active';
    const data: ConfirmDialogData = archiving
      ? {
          title: `Archive ${project.key}?`,
          message: 'Open work items will be cancelled and the project becomes read-only. Items in progress or in review must be finished or cancelled first.',
          confirmLabel: 'Archive project',
          destructive: true,
        }
      : { title: `Restore ${project.key}?`, message: 'The project will accept new work items again.', confirmLabel: 'Restore project' };

    this.dialog
      .open(ConfirmDialog, { data, width: '440px' })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => (archiving ? this.projects.archive(project.id, project.rowVersion) : this.projects.restore(project.id, project.rowVersion))),
      )
      .subscribe({
        next: () => {
          this.feedback.success(archiving ? 'Project archived.' : 'Project restored.');
          this.reload();
        },
        error: (error: unknown) => this.feedback.error(error),
      });
  }

  protected sortItems(sort: Sort): void {
    this.itemQuery.update((q) => ({ ...q, sortBy: sort.active, sortDirection: sort.direction === 'desc' ? 'Descending' : 'Ascending', page: 1 }));
  }

  protected pageItems(event: PageEvent): void {
    this.itemQuery.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }
}
