import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { filter, switchMap } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';
import { FeedbackService } from '../../core/services/feedback.service';
import { LookupService } from '../../core/services/lookup.service';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { PagedResult, UserRole } from '../../shared/models/api.models';
import { toHttpParams } from '../../shared/models/http-params';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';
import { User } from '../../shared/models/user.models';
import { CreateUserDialog } from './create-user-dialog';

/** Administrators provision accounts here; there is no self-registration. */
@Component({
  selector: 'app-user-list-page',
  imports: [DatePipe, MatTableModule, MatPaginatorModule, MatSelectModule, MatSlideToggleModule, MatButtonModule, PageHeader, StatePanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Users" subtitle="Accounts are created by administrators." [breadcrumbs]="[{ label: 'Administration' }, { label: 'Users' }]">
      <button mat-flat-button type="button" (click)="create()">New user</button>
    </app-page-header>

    @if (error(); as message) {
      <app-state-panel kind="error" [message]="message" (retry)="reload()" />
    } @else if (result(); as result) {
      <div class="table-scroll">
        <table mat-table [dataSource]="result.items">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let u">{{ u.displayName }}<div class="secondary">{{ u.email }}</div></td>
          </ng-container>
          <ng-container matColumnDef="role">
            <th mat-header-cell *matHeaderCellDef>Role</th>
            <td mat-cell *matCellDef="let u">
              <mat-select class="role-select" [value]="u.role" [disabled]="u.id === currentUserId()" (selectionChange)="save(u, $event.value, u.isActive)" aria-label="Role">
                @for (role of roles; track role) {
                  <mat-option [value]="role">{{ role }}</mat-option>
                }
              </mat-select>
            </td>
          </ng-container>
          <ng-container matColumnDef="active">
            <th mat-header-cell *matHeaderCellDef>Active</th>
            <td mat-cell *matCellDef="let u">
              <mat-slide-toggle [checked]="u.isActive" [disabled]="u.id === currentUserId()" (change)="save(u, u.role, $event.checked)" aria-label="Active" />
            </td>
          </ng-container>
          <ng-container matColumnDef="lastLogin">
            <th mat-header-cell *matHeaderCellDef>Last sign-in</th>
            <td mat-cell *matCellDef="let u">{{ u.lastLoginAtUtc ? (u.lastLoginAtUtc | date: 'medium') : 'Never' }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>
      <mat-paginator [length]="result.totalCount" [pageIndex]="result.page - 1" [pageSize]="result.pageSize"
                     [pageSizeOptions]="[20, 50]" (page)="page($event)" aria-label="Select page" />
    } @else {
      <app-state-panel kind="loading" />
    }
  `,
  styles: `
    .secondary { font-size: 12px; color: var(--app-text-muted); }
    .role-select { width: 120px; }
  `,
})
export class UserListPage {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);
  private readonly feedback = inject(FeedbackService);
  private readonly lookups = inject(LookupService);
  private readonly auth = inject(AuthService);

  protected readonly columns = ['name', 'role', 'active', 'lastLogin'];
  protected readonly roles: UserRole[] = ['Member', 'Manager', 'Admin'];
  protected readonly currentUserId = computed(() => this.auth.user()?.id);
  private readonly query = signal({ page: 1, pageSize: 20, reload: 0 });

  private readonly state = toSignal(
    toObservable(this.query).pipe(
      switchMap(({ page, pageSize }) =>
        withLoadState(this.http.get<PagedResult<User>>('/api/v1/users', { params: toHttpParams({ page, pageSize }) })),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<User>> },
  );
  protected readonly result = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected reload(): void {
    this.query.update((q) => ({ ...q, reload: q.reload + 1 }));
  }

  protected save(user: User, role: UserRole, isActive: boolean): void {
    this.http.put<User>(`/api/v1/users/${user.id}`, { role, isActive }).subscribe({
      next: () => {
        this.lookups.invalidateUsers();
        this.feedback.success(`${user.displayName} updated.`);
        this.reload();
      },
      error: (error: unknown) => {
        this.feedback.error(error);
        this.reload();
      },
    });
  }

  protected create(): void {
    this.dialog
      .open(CreateUserDialog, { width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.lookups.invalidateUsers();
        this.feedback.success('User created.');
        this.reload();
      });
  }
}
