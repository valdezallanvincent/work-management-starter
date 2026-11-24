import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { StatusBadge } from '../../shared/components/status-badge';
import { HasRoleDirective } from '../../shared/directives/has-role.directive';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';
import { PagedResult } from '../../shared/models/api.models';
import { ProjectStatus, ProjectSummary } from '../../shared/models/project.models';
import { ProjectFormDialog } from './project-form-dialog';
import { ProjectQuery, ProjectsService } from './projects.service';

@Component({
  selector: 'app-project-list-page',
  imports: [
    ReactiveFormsModule, RouterLink, DatePipe, MatTableModule, MatSortModule, MatPaginatorModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, PageHeader, StatePanel, StatusBadge, HasRoleDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-list-page.html',
})
export class ProjectListPage {
  private readonly projects = inject(ProjectsService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly columns = ['key', 'name', 'owner', 'status', 'open', 'created'];
  protected readonly searchControl = new FormControl('', { nonNullable: true });

  protected readonly query = signal<ProjectQuery>({ page: 1, pageSize: 20, sortBy: 'name', sortDirection: 'Ascending', status: 'Active' });

  constructor() {
    // Typing is debounced in RxJS; the resulting value is written into the query signal.
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((search) => this.query.update((q) => ({ ...q, search: search.trim() || undefined, page: 1 })));
  }

  // switchMap cancels the previous request when filters change, so a slow response never overwrites a newer one.
  protected readonly state = toSignal(
    toObservable(this.query).pipe(switchMap((query) => withLoadState(this.projects.search(query)))),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<ProjectSummary>> },
  );
  protected readonly result = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));

  protected setStatus(status: ProjectStatus | ''): void {
    this.query.update((q) => ({ ...q, status: status || undefined, page: 1 }));
  }

  protected sort(sort: Sort): void {
    this.query.update((q) => ({
      ...q,
      sortBy: sort.active,
      sortDirection: sort.direction === 'desc' ? 'Descending' : 'Ascending',
      page: 1,
    }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected reload(): void {
    this.query.update((q) => ({ ...q }));
  }

  protected create(): void {
    this.dialog
      .open(ProjectFormDialog, { width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((project) => project && void this.router.navigate(['/projects', project.id]));
  }
}
