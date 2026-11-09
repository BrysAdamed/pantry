import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { FeaturePlaceholder, PlaceholderDestination } from './feature-placeholder';

describe('FeaturePlaceholder', () => {
  function render(placeholder: PlaceholderDestination): HTMLElement {
    TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] });
    const fixture = TestBed.createComponent(FeaturePlaceholder);
    fixture.componentRef.setInput('placeholder', placeholder);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the empty state with an agent hint where the agent can help', () => {
    const page = render('planner');

    expect(page.querySelector('h2')?.textContent).toContain('Plan this week');
    expect(page.textContent).toContain('Ask Claude');
    expect(page.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('leaves the agent hint out where the agent has nothing to do', () => {
    const page = render('shopping');

    expect(page.querySelector('h2')?.textContent).toContain('Everything is in the cart');
    expect(page.textContent).not.toContain('Ask Claude');
  });
});
