import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../../core/authentication/auth.service';
import { UserRole } from '../models/api.models';

/**
 * Renders its template only for the given roles: `<button *appHasRole="['Admin']">`.
 * Hiding controls is a usability aid; authorization is enforced by the API.
 */
@Directive({ selector: '[appHasRole]' })
export class HasRoleDirective {
  private readonly auth = inject(AuthService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);

  readonly appHasRole = input.required<UserRole[]>();

  constructor() {
    // An effect is appropriate here: it synchronises the view with signal state outside the template.
    effect(() => {
      const role = this.auth.user()?.role;
      const visible = role !== undefined && this.appHasRole().includes(role);
      this.container.clear();
      if (visible) {
        this.container.createEmbeddedView(this.template);
      }
    });
  }
}
