import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { LookupService } from '../../core/services/lookup.service';
import { errorMessage } from '../../shared/models/load-state';
import { WORK_ITEM_PRIORITIES, WorkItemDetails, WorkItemPriority } from '../../shared/models/work-item.models';
import { WorkItemsService } from './work-items.service';

export interface WorkItemFormData {
  projectId?: string;
  existing?: WorkItemDetails;
}

/** Creates a work item, or edits title, description, priority and due date of an existing one. */
@Component({
  selector: 'app-work-item-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ existing ? 'Edit work item' : 'New work item' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (!existing) {
          <mat-form-field appearance="outline">
            <mat-label>Project</mat-label>
            <mat-select formControlName="projectId">
              @for (project of projects(); track project.id) {
                <mat-option [value]="project.id">{{ project.key }} - {{ project.name }}</mat-option>
              }
            </mat-select>
            @if (form.controls.projectId.hasError('required')) {
              <mat-error>Choose a project.</mat-error>
            }
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="200" />
          @if (form.controls.title.hasError('required')) {
            <mat-error>Title is required.</mat-error>
          }
        </mat-form-field>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              @for (priority of priorities; track priority) {
                <mat-option [value]="priority">{{ priority }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Due date</mat-label>
            <input matInput type="date" formControlName="dueDate" />
          </mat-form-field>
        </div>
        @if (!existing) {
          <mat-form-field appearance="outline">
            <mat-label>Assignee</mat-label>
            <mat-select formControlName="assigneeId">
              <mat-option value="">Unassigned</mat-option>
              @for (user of users(); track user.id) {
                <mat-option [value]="user.id">{{ user.displayName }}</mat-option>
              }
            </mat-select>
            <mat-hint>Only the project owner can assign other people.</mat-hint>
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="5" maxlength="8000"></textarea>
        </mat-form-field>
        @if (error()) {
          <p class="form-error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ existing ? 'Save changes' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class WorkItemFormDialog {
  private readonly workItems = inject(WorkItemsService);
  private readonly dialogRef = inject(MatDialogRef<WorkItemFormDialog, WorkItemDetails>);
  private readonly data = inject<WorkItemFormData>(MAT_DIALOG_DATA, { optional: true }) ?? {};
  private readonly lookups = inject(LookupService);

  protected readonly existing = this.data.existing;
  protected readonly priorities = WORK_ITEM_PRIORITIES;
  protected readonly projects = toSignal(this.lookups.projects(), { initialValue: [] });
  protected readonly users = toSignal(this.lookups.users(), { initialValue: [] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    projectId: [this.data.projectId ?? this.existing?.projectId ?? '', Validators.required],
    title: [this.existing?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    priority: [this.existing?.priority ?? ('Medium' as WorkItemPriority)],
    dueDate: [this.existing?.dueDate ?? ''],
    assigneeId: [''],
    description: [this.existing?.description ?? ''],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const common = {
      title: value.title,
      description: value.description.trim() || null,
      priority: value.priority,
      dueDate: value.dueDate || null,
    };

    const request$: Observable<WorkItemDetails> = this.existing
      ? this.workItems.update(this.existing.id, { ...common, rowVersion: this.existing.rowVersion })
      : this.workItems.create({ ...common, projectId: value.projectId, assigneeId: value.assigneeId || null });

    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (item) => this.dialogRef.close(item),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(errorMessage(error));
      },
    });
  }
}
