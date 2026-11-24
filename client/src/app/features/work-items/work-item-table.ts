import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { StatusBadge } from '../../shared/components/status-badge';
import { PagedResult } from '../../shared/models/api.models';
import { WorkItemSummary } from '../../shared/models/work-item.models';

/** Presentational table shared by the work item list and project details pages. */
@Component({
  selector: 'app-work-item-table',
  imports: [MatTableModule, MatSortModule, MatPaginatorModule, RouterLink, DatePipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-scroll">
      <table mat-table [dataSource]="result().items" matSort [matSortActive]="sortBy()" [matSortDirection]="sortDirection()"
             (matSortChange)="sortChange.emit($event)" matSortDisableClear>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Title</th>
          <td mat-cell *matCellDef="let item">
            <a class="row-link" [routerLink]="['/work-items', item.id]">{{ item.title }}</a>
            @if (showProject()) {
              <div class="secondary">{{ item.projectKey }}</div>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
          <td mat-cell *matCellDef="let item"><app-status-badge [value]="item.status" /></td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
          <td mat-cell *matCellDef="let item"><app-status-badge [value]="item.priority" /></td>
        </ng-container>
        <ng-container matColumnDef="assignee">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Assignee</th>
          <td mat-cell *matCellDef="let item">{{ item.assigneeName ?? 'Unassigned' }}</td>
        </ng-container>
        <ng-container matColumnDef="dueDate">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Due</th>
          <td mat-cell *matCellDef="let item">{{ item.dueDate ? (item.dueDate | date: 'mediumDate') : '' }}</td>
        </ng-container>
        <ng-container matColumnDef="modifiedAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Updated</th>
          <td mat-cell *matCellDef="let item">{{ (item.modifiedAtUtc ?? item.createdAtUtc) | date: 'mediumDate' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    </div>
    <mat-paginator [length]="result().totalCount" [pageIndex]="result().page - 1" [pageSize]="result().pageSize"
                   [pageSizeOptions]="[10, 20, 50]" (page)="pageChange.emit($event)" aria-label="Select page" />
  `,
  styles: `
    .secondary { font-size: 12px; color: var(--app-text-muted); }
    .mat-column-title { min-width: 260px; }
  `,
})
export class WorkItemTable {
  readonly result = input.required<PagedResult<WorkItemSummary>>();
  readonly sortBy = input('');
  readonly sortDirection = input<'asc' | 'desc' | ''>('');
  readonly showProject = input(true);
  readonly sortChange = output<Sort>();
  readonly pageChange = output<PageEvent>();

  protected readonly columns = ['title', 'status', 'priority', 'assignee', 'dueDate', 'modifiedAt'];
}
