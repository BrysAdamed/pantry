import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { firstValueFrom } from 'rxjs';
import { CurrentUser, IdentityApi } from './identity-api';
import { authInterceptor, requireSignedIn, requireSignedOut, safeReturnUrl, Session } from './session';

const user: CurrentUser = { id: '1', displayName: 'Alex', role: 'admin', uiLanguage: null, theme: 'system' };

@Component({ template: '<p>screen</p>' })
class Screen {}

describe('Session guards', () => {
  let api: { me: ReturnType<typeof vi.fn>; signOut: ReturnType<typeof vi.fn>; refreshAntiforgeryToken: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    api = { me: vi.fn(), signOut: vi.fn().mockResolvedValue(undefined), refreshAntiforgeryToken: vi.fn().mockResolvedValue(undefined) };
    TestBed.configureTestingModule({
      providers: [
        { provide: IdentityApi, useValue: api },
        provideRouter([
          { path: 'planner', component: Screen, canActivate: [requireSignedIn] },
          { path: 'sign-in', component: Screen, canActivate: [requireSignedOut] },
          { path: '', component: Screen },
        ]),
      ],
    });
  });

  const url = () => TestBed.inject(Router).url;

  it('sends a signed-out user to sign-in and remembers where they were going', async () => {
    api.me.mockRejectedValue(new HttpErrorResponse({ status: 401 }));

    await RouterTestingHarness.create('/planner?week=2');

    expect(url()).toBe('/sign-in?returnUrl=%2Fplanner%3Fweek%3D2');
  });

  it('fails closed when the API cannot be reached', async () => {
    api.me.mockRejectedValue(new HttpErrorResponse({ status: 0 }));

    await RouterTestingHarness.create('/planner');

    expect(url()).toBe('/sign-in?returnUrl=%2Fplanner');
  });

  it('lets a signed-in user in and asks the API only once', async () => {
    api.me.mockResolvedValue(user);
    const harness = await RouterTestingHarness.create('/planner');

    await harness.navigateByUrl('/');
    await harness.navigateByUrl('/planner');

    expect(url()).toBe('/planner');
    expect(api.me).toHaveBeenCalledTimes(1);
  });

  it('asks the API again after sign-out', async () => {
    api.me.mockResolvedValueOnce(user).mockRejectedValue(new HttpErrorResponse({ status: 401 }));
    const harness = await RouterTestingHarness.create('/planner');

    await harness.navigateByUrl('/');
    await TestBed.inject(Session).signOut();
    await harness.navigateByUrl('/planner');

    expect(url()).toBe('/sign-in?returnUrl=%2Fplanner');
  });

  it('refreshes the antiforgery token before signing out, so a token from an ended session does not fail the call', async () => {
    const calls: string[] = [];
    api.refreshAntiforgeryToken.mockImplementation(async () => calls.push('token'));
    api.signOut.mockImplementation(async () => calls.push('signOut'));

    await TestBed.inject(Session).signOut();

    expect(calls).toEqual(['token', 'signOut']);
  });

  it('forgets the user even when the sign-out call fails', async () => {
    const session = TestBed.inject(Session);
    session.signedIn(user);
    api.signOut.mockRejectedValue(new HttpErrorResponse({ status: 400 }));

    await expect(session.signOut()).rejects.toBeInstanceOf(HttpErrorResponse);

    expect(session.user()).toBeNull();
  });

  it('sends a signed-in user from sign-in to the requested page', async () => {
    api.me.mockResolvedValue(user);

    await RouterTestingHarness.create('/sign-in?returnUrl=%2Fplanner');

    expect(url()).toBe('/planner');
  });

  it('shows sign-in when the API cannot be reached', async () => {
    api.me.mockRejectedValue(new HttpErrorResponse({ status: 0 }));

    await RouterTestingHarness.create('/sign-in');

    expect(url()).toBe('/sign-in');
  });
});

describe('safeReturnUrl', () => {
  it.each([
    ['/planner?week=2', '/planner?week=2'],
    [undefined, '/'],
    ['https://evil.example', '/'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    ['/sign-in?returnUrl=%2F', '/'],
  ])('%s -> %s', (input, expected) => {
    expect(safeReturnUrl(input)).toBe(expected);
  });
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Screen }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    await TestBed.inject(Router).navigateByUrl('/recipes');
  });

  it('sends the user to sign-in when the session is gone', async () => {
    const session = TestBed.inject(Session);
    session.signedIn(user);
    const request = firstValueFrom(http.get('/api/recipes'));

    backend.expectOne('/api/recipes').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(request).rejects.toBeInstanceOf(HttpErrorResponse);
    await new Promise((resolve) => setTimeout(resolve));
    expect(session.user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/sign-in?returnUrl=%2Frecipes&reason=expired');
  });

  it('leaves 401s of the sign-in calls to their callers', async () => {
    const result = TestBed.inject(IdentityApi).signIn({} as PublicKeyCredentialJSON);

    backend.expectOne('/api/auth/sign-in').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(TestBed.inject(Router).url).toBe('/recipes');
  });
});
