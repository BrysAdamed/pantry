import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
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
  });

  const page = () => harness.routeNativeElement!.ownerDocument;
  const shell = () => harness.fixture.nativeElement as HTMLElement;
  const links = (testId: string) => Array.from(shell().querySelectorAll<HTMLAnchorElement>(`[data-testid="${testId}"] a`));

  it('has a main landmark and a skip link to it', () => {
    expect(shell().querySelector('main#main')).not.toBeNull();
    expect(shell().querySelector('a[href="#main"]')?.textContent).toContain('Skip to content');
  });

  it.each(['rail', 'tabs'])('lists the four destinations in the %s navigation', (testId) => {
    const nav = shell().querySelector(`[data-testid="${testId}"] nav, nav[data-testid="${testId}"]`);

    expect(nav?.getAttribute('aria-label')).toBe('Main navigation');
    expect(links(testId).map((link) => link.textContent?.trim())).toEqual(['Plan', 'Recipes', 'Shopping', 'Household']);
    expect(links(testId).map((link) => link.getAttribute('href'))).toEqual(['/planner', '/recipes', '/shopping', '/household']);
  });

  it('opens the planner first and marks the current destination', async () => {
    expect(links('tabs')[0].getAttribute('aria-current')).toBe('page');

    await harness.navigateByUrl('/shopping');

    expect(links('rail').map((link) => link.getAttribute('aria-current'))).toEqual([null, null, 'page', null]);
    expect(links('tabs')[2].getAttribute('aria-current')).toBe('page');
    expect(shell().querySelector('h1')?.textContent).toContain('Shopping');
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
