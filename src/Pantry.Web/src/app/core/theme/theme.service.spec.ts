import { TestBed } from '@angular/core/testing';
import { fakeMatchMedia, FakeMediaQueries } from '../../testing/match-media';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let systemTheme: FakeMediaQueries;

  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset['theme'];
    systemTheme = fakeMatchMedia(false);
  });

  function create(): ThemeService {
    const service = TestBed.inject(ThemeService);
    service.init();
    return service;
  }

  it('follows the system theme by default', () => {
    systemTheme.setMatches(true);

    const service = create();

    expect(service.preference()).toBe('system');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('reacts when the system theme changes', () => {
    create();

    systemTheme.setMatches(true);

    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('keeps an explicit choice over the system theme and remembers it', () => {
    systemTheme.setMatches(true);
    const service = create();

    service.use('light');
    systemTheme.setMatches(true);

    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('pantry.theme')).toBe('light');
  });

  it('restores the stored choice', () => {
    localStorage.setItem('pantry.theme', 'dark');

    const service = create();

    expect(service.preference()).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('ignores an unknown stored value', () => {
    localStorage.setItem('pantry.theme', 'sepia');

    expect(create().preference()).toBe('system');
  });
});
