import { TestBed } from '@angular/core/testing';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it('renders a readable label with a tone class', async () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('value', 'InReview');
    await fixture.whenStable();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge')!;
    expect(badge.textContent?.trim()).toBe('In review');
    expect(badge.classList).toContain('badge--warning');
  });
});
