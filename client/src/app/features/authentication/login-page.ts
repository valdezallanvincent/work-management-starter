import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/authentication/auth.service';
import { errorMessage } from '../../shared/models/load-state';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="login">
      <section class="login-card" aria-labelledby="login-title">
        <h1 id="login-title">Work Management</h1>
        <p class="hint">Sign in with the account your administrator created for you.</p>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" required />
            @if (form.controls.email.hasError('email')) {
              <mat-error>Enter a valid email address.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="current-password" required />
          </mat-form-field>

          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }

          <button mat-flat-button type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Signing in...' : 'Sign in' }}
          </button>
        </form>
      </section>
    </main>
  `,
  styles: `
    .login { min-height: 100vh; display: grid; place-items: center; padding: 16px; background: var(--app-surface-alt); }
    .login-card { width: 100%; max-width: 380px; background: var(--app-surface); border: 1px solid var(--app-border); border-radius: 8px; padding: 32px 28px; }
    h1 { font-size: 20px; font-weight: 600; margin: 0 0 4px; }
    .hint { margin: 0 0 24px; color: var(--app-text-muted); font-size: 13px; }
    form { display: flex; flex-direction: column; gap: 4px; }
    .error { color: var(--app-error); margin: 0 0 12px; font-size: 13px; }
    button { height: 40px; }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        // Only follow in-app paths to avoid an open redirect through the returnUrl parameter.
        const target = returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/dashboard';
        void this.router.navigateByUrl(target);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.error.set(errorMessage(error) === 'Authentication failed.' ? 'Email or password is incorrect.' : errorMessage(error));
      },
    });
  }
}
