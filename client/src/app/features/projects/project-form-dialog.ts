import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';
import { LookupService } from '../../core/services/lookup.service';
import { errorMessage } from '../../shared/models/load-state';
import { ProjectDetails } from '../../shared/models/project.models';
import { ProjectsService } from './projects.service';

/** Creates a project, or edits one when opened with existing details. Closes with the saved project. */
@Component({
  selector: 'app-project-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ existing ? 'Edit project' : 'New project' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <mat-form-field appearance="outline">
          <mat-label>Key</mat-label>
          <input matInput formControlName="key" maxlength="10" placeholder="OPS" />
          <mat-hint>2-10 letters or digits, starting with a letter. Cannot be changed later.</mat-hint>
          @if (form.controls.key.hasError('pattern')) {
            <mat-error>Use letters and digits only, starting with a letter.</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="120" />
          @if (form.controls.name.hasError('required')) {
            <mat-error>Name is required.</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Owner</mat-label>
          <mat-select formControlName="ownerId">
            @for (user of users(); track user.id) {
              <mat-option [value]="user.id">{{ user.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="4" maxlength="2000"></textarea>
        </mat-form-field>
        @if (error()) {
          <p class="form-error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ existing ? 'Save changes' : 'Create project' }}</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class ProjectFormDialog {
  private readonly projects = inject(ProjectsService);
  private readonly dialogRef = inject(MatDialogRef<ProjectFormDialog, ProjectDetails>);
  protected readonly existing = inject<ProjectDetails | null>(MAT_DIALOG_DATA, { optional: true });

  protected readonly users = toSignal(inject(LookupService).users(), { initialValue: [] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    key: [this.existing?.key ?? '', [Validators.required, Validators.pattern(/^[A-Za-z][A-Za-z0-9]{1,9}$/)]],
    name: [this.existing?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    ownerId: [this.existing?.ownerId ?? inject(AuthService).user()?.id ?? '', Validators.required],
    description: [this.existing?.description ?? ''],
  });

  constructor() {
    if (this.existing) {
      this.form.controls.key.disable();
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const description = value.description.trim() || null;
    const request$: Observable<ProjectDetails> = this.existing
      ? this.projects.update(this.existing.id, {
          name: value.name,
          description,
          ownerId: value.ownerId,
          rowVersion: this.existing.rowVersion,
        })
      : this.projects.create({ key: value.key.toUpperCase(), name: value.name, description, ownerId: value.ownerId });

    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (project) => this.dialogRef.close(project),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(errorMessage(error));
      },
    });
  }
}
