import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { UiLanguageService } from '../core/i18n/ui-language.service';
import { ThemeService } from '../core/theme/theme.service';
import { CurrentUser, IdentityApi } from '../identity/identity-api';
import { Session } from '../identity/session';
import { fakeMatchMedia } from '../testing/match-media';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { AccountApi } from './account-api';
import { UserPreferences } from './user-preferences';

const user: CurrentUser = { id: '1', displayName: 'Alex', role: 'user', userName: null, uiLanguage: 'pl', theme: 'dark', signInMethods: ['passkey'] };

describe('UserPreferences', () => {
  let api: { changeUiLanguage: ReturnType<typeof vi.fn>; changeTheme: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    localStorage.clear();
    fakeMatchMedia();
    api = { changeUiLanguage: vi.fn().mockResolvedValue(undefined), changeTheme: vi.fn().mockResolvedValue(undefined) };
    TestBed.configureTestingModule({
      providers: [provideTranslocoTesting(), { provide: AccountApi, useValue: api }, { provide: IdentityApi, useValue: {} }],
    });
    TestBed.inject(ThemeService).init();
  });

  function create() {
    const preferences = TestBed.inject(UserPreferences);
    const session = TestBed.inject(Session);
    const language = TestBed.inject(UiLanguageService);
    const theme = TestBed.inject(ThemeService);
    return { preferences, session, language, theme };
  }

  it('applies the saved choice of the user who signs in and keeps it on the device', () => {
    const { session, language, theme } = create();

    session.signedIn(user);
    TestBed.tick();

    expect(language.current()).toBe('pl');
    expect(theme.preference()).toBe('dark');
    expect(localStorage.getItem('pantry.uiLanguage')).toBe('pl');
    expect(localStorage.getItem('pantry.theme')).toBe('dark');
  });

  it("keeps the device's language for a user without a saved one", () => {
    const { session, language } = create();
    language.use('en');

    session.signedIn({ ...user, uiLanguage: null });
    TestBed.tick();

    expect(language.current()).toBe('en');
  });

  it('does not undo a change when the same user is read again', async () => {
    const { preferences, session, language } = create();
    session.signedIn(user);
    TestBed.tick();

    await preferences.setUiLanguage('en');
    session.signedIn({ ...user });
    TestBed.tick();

    expect(language.current()).toBe('en');
  });

  it('applies a change at once and saves it on the user, one save after another', async () => {
    const { preferences, session, language, theme } = create();
    session.signedIn(user);
    TestBed.tick();
    const calls: string[] = [];
    let finishFirst!: () => void;
    api.changeUiLanguage.mockImplementationOnce(
      (value: string) => new Promise<void>((resolve) => (finishFirst = () => (calls.push(value), resolve()))),
    );
    api.changeTheme.mockImplementation(async (value: string) => calls.push(value));

    const first = preferences.setUiLanguage('en');
    const second = preferences.setTheme('light');
    await new Promise((resolve) => setTimeout(resolve));

    expect(language.current()).toBe('en');
    expect(theme.preference()).toBe('light');
    expect(calls).toEqual([]);
    finishFirst();
    await Promise.all([first, second]);
    expect(calls).toEqual(['en', 'light']);
    expect(preferences.saveFailed()).toBe(false);
  });

  it('only changes the device when nobody is signed in', async () => {
    const { preferences, language } = create();

    await preferences.setUiLanguage('pl');

    expect(language.current()).toBe('pl');
    expect(api.changeUiLanguage).not.toHaveBeenCalled();
  });

  it('tells when a change could not be saved and keeps it on the device', async () => {
    const { preferences, session, theme } = create();
    session.signedIn(user);
    TestBed.tick();
    api.changeTheme.mockRejectedValue(new HttpErrorResponse({ status: 0 }));

    await preferences.setTheme('system');

    expect(preferences.saveFailed()).toBe(true);
    expect(theme.preference()).toBe('system');
  });
});
