import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { INGREDIENTS_URL } from '../catalog/catalog-api';
import { NeedsReviewCount } from '../catalog/needs-review-count';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { DESTINATIONS } from './destinations';
import { Shell } from './shell';

@Component({ template: '<p>screen</p>' })
class Screen {}

describe('Shell', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTranslocoTesting(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          {
            path: '',
            component: Shell,
            children: [
              { path: '', pathMatch: 'full', redirectTo: 'planner' },
              ...DESTINATIONS.map((path) => ({ path, component: Screen })),
            ],
          },
          { path: 'sign-in', component: Screen },
        ]),
      ],
    });
    harness = await RouterTestingHarness.create('/');
    flushNeedsReview(0);
    await harness.fixture.whenStable();
  });

  function flushNeedsReview(total: number) {
    TestBed.inject(HttpTestingController)
      .expectOne((request) => request.url === INGREDIENTS_URL && request.params.get('needsReview') === 'true')
      .flush({ items: [], total, page: 1, pageSize: 1 });
  }

  const page = () => harness.routeNativeElement!.ownerDocument;
  const shell = () => harness.fixture.nativeElement as HTMLElement;
  const links = (testId: string) => Array.from(shell().querySelectorAll<HTMLAnchorElement>(`[data-testid="${testId}"] a`));

  it('has a main landmark and a skip link to it', () => {
    expect(shell().querySelector('main#main')).not.toBeNull();
    expect(shell().querySelector('a[href="#main"]')?.textContent).toContain('Skip to content');
  });

  it.each(['rail', 'tabs'])('lists the five destinations in the %s navigation', (testId) => {
    const nav = shell().querySelector(`[data-testid="${testId}"] nav, nav[data-testid="${testId}"]`);

    expect(nav?.getAttribute('aria-label')).toBe('Main navigation');
    expect(links(testId).map((link) => link.textContent?.trim())).toEqual(['Plan', 'Recipes', 'Shopping', 'Ingredients', 'Household']);
    expect(links(testId).map((link) => link.getAttribute('href'))).toEqual(['/planner', '/recipes', '/shopping', '/ingredients', '/household']);
  });

  it('opens the planner first and marks the current destination', async () => {
    expect(links('tabs')[0].getAttribute('aria-current')).toBe('page');

    await harness.navigateByUrl('/shopping');

    expect(links('rail').map((link) => link.getAttribute('aria-current'))).toEqual([null, null, 'page', null, null]);
    expect(links('tabs')[2].getAttribute('aria-current')).toBe('page');
    expect(shell().querySelector('h1')?.textContent).toContain('Shopping');
  });

  it('counts the ingredients to review on the Ingredients destination only', async () => {
    expect(shell().querySelector('[data-testid="needs-review-badge"]')).toBeNull();

    TestBed.inject(NeedsReviewCount).reload();
    TestBed.tick();
    flushNeedsReview(3);
    await harness.fixture.whenStable();

    const badges = [...shell().querySelectorAll('[data-testid="needs-review-badge"]')];
    expect(badges.map((badge) => badge.textContent?.trim())).toEqual(['3', '3']);
    expect(badges.map((badge) => badge.closest('a')?.getAttribute('href'))).toEqual(['/ingredients', '/ingredients']);
    expect(badges[0].closest('a')?.textContent).toContain('3 need review');
  });

  it('shows the saved sync state', () => {
    const indicators = shell().querySelectorAll('[data-testid="sync-indicator"]');

    expect(indicators.length).toBe(2);
    expect(indicators[0].getAttribute('role')).toBe('status');
    expect(indicators[0].textContent).toContain('Saved');
  });

  it('opens the avatar menu with language, theme and sign-out, and closes it on Escape', async () => {
    const trigger = shell().querySelector<HTMLButtonElement>('[data-testid="avatar-menu-trigger"]')!;

    trigger.click();
    await harness.fixture.whenStable();

    const dialog = page().querySelector('[role="dialog"]')!;
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(trigger.getAttribute('aria-controls')).toBe(dialog.id);
    expect(dialog.querySelector('[data-testid="language-pl"]')).not.toBeNull();
    expect(dialog.querySelector('[data-testid="theme-dark"]')).not.toBeNull();
    expect(dialog.textContent).toContain('Sign out');

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await harness.fixture.whenStable();

    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(page().querySelector('[role="dialog"]')).toBeNull();
  });

  it('signs out to the sign-in screen even when the session had already ended', async () => {
    const http = TestBed.inject(HttpTestingController);
    shell().querySelector<HTMLButtonElement>('[data-testid="avatar-menu-trigger"]')!.click();
    await harness.fixture.whenStable();

    page().querySelector<HTMLButtonElement>('[data-testid="sign-out"]')!.click();
    http.expectOne('/api/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne('/api/auth/sign-out').flush(null, { status: 400, statusText: 'Bad Request' });
    await new Promise((resolve) => setTimeout(resolve));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    expect(page().querySelector('[role="dialog"]')).toBeNull();
  });
});
