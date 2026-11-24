import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/authentication/auth.service';
import { NotificationInboxService } from '../core/services/notification-inbox.service';
import { HasRoleDirective } from '../shared/directives/has-role.directive';
import { EnumLabelPipe } from '../shared/pipes/enum-label.pipe';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, DatePipe, MatSidenavModule, MatToolbarModule, MatListModule,
    MatButtonModule, MatIconModule, MatMenuModule, MatBadgeModule, HasRoleDirective, EnumLabelPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly notifications = inject(NotificationInboxService);

  protected readonly isCompact = toSignal(
    inject(BreakpointObserver).observe('(max-width: 900px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly initials = computed(() =>
    (this.auth.user()?.displayName ?? '')
      .split(' ')
      .map((part) => part.charAt(0))
      .join('')
      .slice(0, 2)
      .toUpperCase(),
  );

  constructor() {
    this.notifications.load();
  }
}
