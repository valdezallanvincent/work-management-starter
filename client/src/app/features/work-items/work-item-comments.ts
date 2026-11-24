import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { combineLatest, switchMap } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';
import { FeedbackService } from '../../core/services/feedback.service';
import { StatePanel } from '../../shared/components/state-panel';
import { LoadState, loadError, loadedData, withLoadState } from '../../shared/models/load-state';
import { WorkItemComment } from '../../shared/models/work-item.models';
import { WorkItemsService } from './work-items.service';

@Component({
  selector: 'app-work-item-comments',
  imports: [DatePipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, StatePanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="section-title">Comments</h2>
    @if (error(); as message) {
      <app-state-panel kind="error" [message]="message" (retry)="reload()" />
    } @else if (comments(); as comments) {
      <ol class="comments">
        @for (comment of comments; track comment.id) {
          <li class="comment">
            <div class="meta">
              <strong>{{ comment.authorName }}</strong>
              <span>{{ comment.createdAtUtc | date: 'medium' }}{{ comment.isEdited ? ' (edited)' : '' }}</span>
              @if (editingId() !== comment.id) {
                <span class="comment-actions">
                  @if (comment.authorId === currentUserId() && !readOnly()) {
                    <button mat-button type="button" (click)="startEdit(comment)">Edit</button>
                  }
                  @if (comment.authorId === currentUserId() || isAdmin()) {
                    <button mat-button type="button" (click)="remove(comment)">Delete</button>
                  }
                </span>
              }
            </div>
            @if (editingId() === comment.id) {
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
                <textarea matInput rows="3" [formControl]="editControl" aria-label="Edit comment"></textarea>
              </mat-form-field>
              <div class="edit-actions">
                <button mat-button type="button" (click)="editingId.set(null)">Cancel</button>
                <button mat-flat-button type="button" [disabled]="editControl.invalid" (click)="saveEdit(comment)">Save</button>
              </div>
            } @else {
              <p class="body">{{ comment.body }}</p>
            }
          </li>
        } @empty {
          <li class="empty">No comments yet.</li>
        }
      </ol>
    } @else {
      <app-state-panel kind="loading" />
    }

    @if (!readOnly()) {
      <form class="new-comment" [formGroup]="newCommentForm" (ngSubmit)="add()">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>Add a comment</mat-label>
          <textarea matInput rows="3" [formControl]="newControl" maxlength="4000"></textarea>
        </mat-form-field>
        <button mat-flat-button type="submit" [disabled]="newControl.invalid || posting()">Comment</button>
      </form>
    }
  `,
  styles: `
    .comments { list-style: none; margin: 0 0 16px; padding: 0; }
    .comment { padding: 12px 0; border-bottom: 1px solid var(--app-border); }
    .meta { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; font-size: 13px; }
    .meta span { color: var(--app-text-muted); font-size: 12px; }
    .comment-actions { margin-left: auto; }
    .body { margin: 6px 0 0; white-space: pre-line; font-size: 14px; line-height: 1.5; }
    .empty { color: var(--app-text-muted); font-size: 13px; padding: 8px 0; }
    .full { width: 100%; }
    .new-comment { display: flex; flex-direction: column; align-items: flex-end; gap: 8px; }
    .edit-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
  `,
})
export class WorkItemComments {
  private readonly workItems = inject(WorkItemsService);
  private readonly feedback = inject(FeedbackService);
  private readonly auth = inject(AuthService);

  readonly workItemId = input.required<string>();
  readonly readOnly = input(false);

  protected readonly currentUserId = computed(() => this.auth.user()?.id);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly editingId = signal<string | null>(null);
  protected readonly posting = signal(false);
  protected readonly newControl = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  protected readonly newCommentForm = new FormGroup({ body: this.newControl });
  protected readonly editControl = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });

  private readonly reloadCount = signal(0);
  private readonly state = toSignal(
    combineLatest([toObservable(this.workItemId), toObservable(this.reloadCount)]).pipe(
      switchMap(([id]) => withLoadState(this.workItems.comments(id))),
    ),
    { initialValue: { status: 'loading' } as LoadState<WorkItemComment[]> },
  );
  protected readonly comments = computed(() => loadedData(this.state()));
  protected readonly error = computed(() => loadError(this.state()));

  protected reload(): void {
    this.reloadCount.update((n) => n + 1);
  }

  protected add(): void {
    const body = this.newControl.value.trim();
    if (!body) {
      return;
    }
    this.posting.set(true);
    this.workItems.addComment(this.workItemId(), body).subscribe({
      next: () => {
        this.posting.set(false);
        this.newControl.reset();
        this.reload();
      },
      error: (error: unknown) => {
        this.posting.set(false);
        this.feedback.error(error);
      },
    });
  }

  protected startEdit(comment: WorkItemComment): void {
    this.editControl.setValue(comment.body);
    this.editingId.set(comment.id);
  }

  protected saveEdit(comment: WorkItemComment): void {
    this.workItems.editComment(this.workItemId(), comment.id, this.editControl.value.trim()).subscribe({
      next: () => {
        this.editingId.set(null);
        this.reload();
      },
      error: (error: unknown) => this.feedback.error(error),
    });
  }

  protected remove(comment: WorkItemComment): void {
    this.workItems.deleteComment(this.workItemId(), comment.id).subscribe({
      next: () => this.reload(),
      error: (error: unknown) => this.feedback.error(error),
    });
  }
}
