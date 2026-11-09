import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let systemPrefersDark: boolean;
  let changeListener: ((event: MediaQueryListEvent) => void) | undefined;

  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset['theme'];
    systemPrefersDark = false;
    changeListener = undefined;
    window.matchMedia = vi.fn(
      (query: string) =>
        ({
          matches: systemPrefersDark,
          media: query,
          addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => {
            changeListener = listener;
          },
        }) as unknown as MediaQueryList,
    );
  });

  function create(): ThemeService {
    const service = TestBed.inject(ThemeService);
    service.init();
    return service;
  }

  it('follows the system theme by default', () => {
    systemPrefersDark = true;

    const service = create();

    expect(service.preference()).toBe('system');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('reacts when the system theme changes', () => {
    create();

    changeListener?.({ matches: true } as MediaQueryListEvent);

    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('keeps an explicit choice over the system theme and remembers it', () => {
    systemPrefersDark = true;
    const service = create();

    service.use('light');
    changeListener?.({ matches: true } as MediaQueryListEvent);

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
