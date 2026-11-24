import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EnumLabelPipe } from '../pipes/enum-label.pipe';

/** Small text badge for statuses and priorities; colour only reinforces the text, never replaces it. */
@Component({
  selector: 'app-status-badge',
  imports: [EnumLabelPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge badge--' + tone()">{{ value() | enumLabel }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      line-height: 18px;
      white-space: nowrap;
      border: 1px solid transparent;
    }
    .badge--neutral { background: var(--app-badge-neutral-bg); color: var(--app-badge-neutral-fg); }
    .badge--info { background: var(--app-badge-info-bg); color: var(--app-badge-info-fg); }
    .badge--warning { background: var(--app-badge-warning-bg); color: var(--app-badge-warning-fg); }
    .badge--success { background: var(--app-badge-success-bg); color: var(--app-badge-success-fg); }
    .badge--danger { background: var(--app-badge-danger-bg); color: var(--app-badge-danger-fg); }
    .badge--muted { background: transparent; color: var(--app-text-muted); border-color: var(--app-border); }
  `,
})
export class StatusBadge {
  readonly value = input.required<string>();

  protected readonly tone = computed(() => {
    switch (this.value()) {
      case 'InProgress':
      case 'Medium':
        return 'info';
      case 'InReview':
      case 'High':
        return 'warning';
      case 'Completed':
      case 'Active':
        return 'success';
      case 'Critical':
        return 'danger';
      case 'Cancelled':
      case 'Archived':
        return 'muted';
      default:
        return 'neutral';
    }
  });
}
