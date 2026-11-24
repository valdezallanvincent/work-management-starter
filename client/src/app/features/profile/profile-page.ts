import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../core/authentication/auth.service';
import { FeedbackService } from '../../core/services/feedback.service';
import { LookupService } from '../../core/services/lookup.service';
import { PageHeader } from '../../shared/components/page-header';
import { StatePanel } from '../../shared/components/state-panel';
import { LoadState, errorMessage, loadedData, withLoadState } from '../../shared/models/load-state';
import { User } from '../../shared/models/user.models';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

@Component({
  selector: 'app-profile-page',
  imports: [DatePipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, PageHeader, StatePanel, EnumLabelPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly feedback = inject(FeedbackService);
  private readonly lookups = inject(LookupService);
  private readonly fb = inject(NonNullableFormBuilder);

  private readonly state = toSignal(withLoadState(this.http.get<User>('/api/v1/users/me')), {
    initialValue: { status: 'loading' } as LoadState<User>,
  });
  protected readonly profile = computed(() => loadedData(this.state()));
  protected readonly savingProfile = signal(false);
  protected readonly savingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);

  protected readonly profileForm = this.fb.group({
    displayName: [this.auth.user()?.displayName ?? '', [Validators.required, Validators.maxLength(100)]],
  });

  protected readonly passwordForm = this.fb.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(12)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  protected saveProfile(): void {
    if (this.profileForm.invalid) {
      return;
    }
    this.savingProfile.set(true);
    const { displayName } = this.profileForm.getRawValue();
    this.http.put<User>('/api/v1/users/me', { displayName }).subscribe({
      next: (user) => {
        this.savingProfile.set(false);
        this.auth.updateDisplayName(user.displayName);
        this.lookups.invalidateUsers();
        this.feedback.success('Profile updated.');
      },
      error: (error: unknown) => {
        this.savingProfile.set(false);
        this.feedback.error(error);
      },
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    this.savingPassword.set(true);
    this.passwordError.set(null);
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.http.put('/api/v1/users/me/password', { currentPassword, newPassword }).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.passwordForm.reset();
        this.feedback.success('Password changed. Other sessions have been signed out.');
      },
      error: (error: unknown) => {
        this.savingPassword.set(false);
        this.passwordError.set(errorMessage(error));
      },
    });
  }
}
