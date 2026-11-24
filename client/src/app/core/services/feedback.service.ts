import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorMessage } from '../../shared/models/load-state';

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, undefined, { duration: 3000 });
  }

  error(error: unknown): void {
    this.snackBar.open(errorMessage(error), 'Dismiss', { duration: 8000, panelClass: 'snack-error' });
  }
}
