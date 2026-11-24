import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface Breadcrumb {
  label: string;
  link?: string;
}

@Component({
  selector: 'app-page-header',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (breadcrumbs().length) {
      <nav class="breadcrumbs" aria-label="Breadcrumb">
        <ol>
          @for (crumb of breadcrumbs(); track crumb.label; let last = $last) {
            <li>
              @if (crumb.link && !last) {
                <a [routerLink]="crumb.link">{{ crumb.label }}</a>
              } @else {
                <span [attr.aria-current]="last ? 'page' : null">{{ crumb.label }}</span>
              }
            </li>
          }
        </ol>
      </nav>
    }
    <div class="header-row">
      <div>
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p class="subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="actions"><ng-content /></div>
    </div>
  `,
  styles: `
    :host { display: block; margin-bottom: 16px; }
    .breadcrumbs ol { display: flex; flex-wrap: wrap; gap: 4px; list-style: none; margin: 0 0 4px; padding: 0; font-size: 13px; }
    .breadcrumbs li + li::before { content: '/'; margin-right: 4px; color: var(--app-text-muted); }
    .breadcrumbs a { color: var(--app-link); text-decoration: none; }
    .breadcrumbs a:hover { text-decoration: underline; }
    .breadcrumbs span { color: var(--app-text-muted); }
    .header-row { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
    h1 { font-size: 20px; font-weight: 600; margin: 0; line-height: 28px; }
    .subtitle { margin: 2px 0 0; color: var(--app-text-muted); font-size: 13px; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly breadcrumbs = input<Breadcrumb[]>([]);
}
