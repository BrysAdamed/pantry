import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { CONTENT_LANGUAGE_URL, CONTENT_LANGUAGES } from '../household-api';
import { ContentLanguageSection } from './content-language-section';

describe('ContentLanguageSection', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContentLanguageSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(contentLanguage = 'pl') {
    const fixture = TestBed.createComponent(ContentLanguageSection);
    fixture.detectChanges();
    http.expectOne(CONTENT_LANGUAGE_URL).flush({ contentLanguage });
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const radios = () => [...page.querySelectorAll<HTMLInputElement>('input[type="radio"]')];
    const radio = (code: string) => radios().find((r) => r.value === code)!;
    return { page, settle, radios, radio };
  }

  it('lists every supported language by its own name and checks the household one', async () => {
    const { page, radios, radio } = await render();

    expect(radios().map((r) => r.value)).toEqual([...CONTENT_LANGUAGES]);
    expect(radios().filter((r) => r.checked).map((r) => r.value)).toEqual(['pl']);
    expect(radio('de').closest('label')?.textContent).toContain('Deutsch');
    expect(radio('de').closest('label')?.textContent).toContain('Recipes and lists in German');
    expect(page.querySelector('b[lang="uk"]')?.textContent).toBe('Українська');
  });

  it('explains that the content language is not the interface language', async () => {
    const { page } = await render();

    expect(page.textContent).toContain('Everyone picks the language of buttons and menus in their own account.');
    expect(page.textContent).toContain('Changing it does not translate existing recipes.');
  });

  it('saves a new language at once', async () => {
    const { radio, settle } = await render();

    radio('de').click();
    await settle();

    expect(radio('de').checked).toBe(true);
    const save = http.expectOne(CONTENT_LANGUAGE_URL);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ contentLanguage: 'de' });
    save.flush({ contentLanguage: 'de' });
  });

  it('reports a failed save and shows the saved language again', async () => {
    const { page, radio, settle } = await render();

    radio('en').click();
    await settle();
    http.expectOne({ method: 'PUT', url: CONTENT_LANGUAGE_URL }).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();

    http.expectOne({ method: 'GET', url: CONTENT_LANGUAGE_URL }).flush({ contentLanguage: 'pl' });
    await settle();
    expect(radio('pl').checked).toBe(true);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The change was not saved');
  });
});
