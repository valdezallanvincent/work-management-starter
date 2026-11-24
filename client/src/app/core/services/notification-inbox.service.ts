import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { NotificationList } from '../../shared/models/dashboard.models';

/**
 * Notifications are created asynchronously by the outbox consumer, so the header refreshes the
 * list when the menu is opened rather than holding a connection open.
 */
@Injectable({ providedIn: 'root' })
export class NotificationInboxService {
  private readonly http = inject(HttpClient);

  readonly inbox = signal<NotificationList>({ items: [], unreadCount: 0 });

  load(): void {
    this.http
      .get<NotificationList>('/api/v1/notifications')
      .pipe(catchError(() => of(null)))
      .subscribe((list) => list && this.inbox.set(list));
  }

  markAllRead(): void {
    this.http.post('/api/v1/notifications/read-all', {}).subscribe(() =>
      this.inbox.update((list) => ({
        unreadCount: 0,
        items: list.items.map((n) => ({ ...n, readAtUtc: n.readAtUtc ?? new Date().toISOString() })),
      })),
    );
  }
}
