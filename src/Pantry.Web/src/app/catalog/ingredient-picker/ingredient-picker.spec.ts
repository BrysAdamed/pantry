import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { Ingredient, IngredientChoice, INGREDIENTS_URL } from '../catalog-api';
import { INGREDIENT_PICKER_RESULTS, INGREDIENT_SEARCH_DELAY_MS, IngredientPicker } from './ingredient-picker';

function ingredient(id: string, name: string, aliases: string[] = [], extra: Partial<Ingredient> = {}): Ingredient {
  return { id, name, aliases, baseUnit: 'g', category: 'other', isStaple: false, needsReview: false, conversions: [], ...extra };
}

const onion = ingredient('onion', 'Cebula', ['cebulka']);
const redOnion = ingredient('red-onion', 'Cebula czerwona', [], { baseUnit: 'piece' });
const yellowCheese = ingredient('cheese', 'Ser żółty', ['gouda'], { needsReview: true });

@Component({
  imports: [IngredientPicker],
  template: `<app-ingredient-picker
    label="Ingredient"
    [allowNew]="allowNew()"
    [taken]="taken()"
    [exclude]="exclude()"
    (picked)="choices.push($event)"
  />`,
})
class Host {
  readonly allowNew = signal(true);
  readonly taken = signal<string[]>([]);
  readonly exclude = signal<string[]>([]);
  readonly choices: IngredientChoice[] = [];
}

describe('IngredientPicker', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(setup: (host: Host) => void = () => undefined) {
    const fixture = TestBed.createComponent(Host);
    setup(fixture.componentInstance);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const field = page.querySelector<HTMLInputElement>('input[role="combobox"]')!;
    const settle = () => fixture.whenStable();

    const type = async (text: string) => {
      field.focus();
      field.value = text;
      field.dispatchEvent(new Event('input'));
      await settle();
      await new Promise((resolve) => setTimeout(resolve, INGREDIENT_SEARCH_DELAY_MS + 20));
      TestBed.tick();
    };
    const search = async (text: string, items: Ingredient[]) => {
      await type(text);
      const request = http.expectOne((r) => r.url === INGREDIENTS_URL);
      request.flush({ items, total: items.length, page: 1, pageSize: INGREDIENT_PICKER_RESULTS });
      await settle();
      return request.request;
    };
    const press = async (key: string) => {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      field.dispatchEvent(event);
      await settle();
      return event;
    };
    const options = () => [...page.querySelectorAll<HTMLElement>('[role="option"]')];
    const listbox = () => page.querySelector<HTMLElement>('[role="listbox"]')!;
    const host = fixture.componentInstance;
    return { page, field, type, search, press, options, listbox, settle, host };
  }

  it('searches names and aliases once typing pauses', async () => {
    const { search, field } = await render();

    const request = await search('  ceb ', [onion, redOnion]);

    expect(request.params.get('search')).toBe('ceb');
    expect(request.params.get('pageSize')).toBe(String(INGREDIENT_PICKER_RESULTS));
    expect(field.getAttribute('aria-expanded')).toBe('true');
  });

  it('marks the matched part and shows the base unit', async () => {
    const { search, options } = await render();

    await search('cebu', [onion, redOnion]);

    expect(options()[0].querySelector('mark')?.textContent).toBe('Cebu');
    expect(options()[1].querySelector('[data-testid="option-name"]')?.textContent).toBe('Cebula czerwona');
    expect(options()[0].textContent).toContain('g');
    expect(options()[1].textContent).toContain('pc');
  });

  it('shows which alias matched, ignoring diacritics and letter case', async () => {
    const { search, options } = await render();

    await search('GOUDA', [yellowCheese]);

    const alias = options()[0].querySelector('[data-testid="option-alias"]')!;
    expect(alias.textContent).toContain('matches:');
    expect(alias.querySelector('mark')?.textContent).toBe('gouda');
    expect(options()[0].textContent).toContain('needs review');
  });

  it('marks a match typed without Polish letters', async () => {
    const { search, options } = await render();

    await search('zolt', [yellowCheese]);

    expect(options()[0].querySelector('mark')?.textContent).toBe('żółt');
    expect(options()[0].querySelector('[data-testid="option-alias"]')).toBeNull();
  });

  it('offers a new name when nothing in the catalog is called that', async () => {
    const { search, options, page } = await render();

    await search('cebula dymka', [onion]);

    const offer = page.querySelector('[data-testid="new-option"]')!;
    expect(options().at(-1)).toBe(offer);
    expect(offer.textContent).toContain('Add "cebula dymka"');
    expect(offer.textContent).toContain('New ingredient, added to the catalog when you save');
  });

  it('does not offer a name the catalog already has as a name or alias', async () => {
    const { search, page } = await render();

    await search('Cebulka', [onion]);

    expect(page.querySelector('[data-testid="new-option"]')).toBeNull();
  });

  it('only finds existing ingredients when the host turns new names off', async () => {
    const { search, page, options } = await render((host) => host.allowNew.set(false));

    await search('dymka', []);

    expect(options()).toHaveLength(0);
    expect(page.querySelector('[data-testid="new-option"]')).toBeNull();
    expect(page.textContent).toContain('No such ingredient in the catalog.');
  });

  it('moves through the options with the arrow keys and picks with Enter', async () => {
    const { search, press, field, options, host } = await render();
    await search('ceb', [onion, redOnion]);

    expect(field.getAttribute('aria-activedescendant')).toBe(options()[0].id);
    await press('ArrowDown');
    expect(field.getAttribute('aria-activedescendant')).toBe(options()[1].id);
    expect(options()[1].getAttribute('aria-selected')).toBe('true');
    await press('ArrowUp');
    await press('ArrowUp');
    expect(field.getAttribute('aria-activedescendant')).toBe(options()[2].id);

    await press('ArrowDown');
    await press('ArrowDown');
    const enter = await press('Enter');

    expect(enter.defaultPrevented).toBe(true);
    expect(host.choices).toEqual([{ kind: 'existing', ingredient: redOnion }]);
    expect(field.value).toBe('');
    expect(field.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(field);
  });

  it('picks a new name with a click', async () => {
    const { search, page, host } = await render();
    await search(' Cebula   dymka ', [onion]);

    page.querySelector<HTMLElement>('[data-testid="new-option"]')!.click();

    expect(host.choices).toEqual([{ kind: 'new', name: 'Cebula dymka' }]);
  });

  it('shows ingredients the host already has, but does not pick them', async () => {
    const { search, press, options, host } = await render((h) => h.taken.set(['onion']));
    await search('ceb', [onion, redOnion]);

    expect(options()[0].getAttribute('aria-disabled')).toBe('true');
    expect(options()[0].textContent).toContain('already on the list');
    options()[0].click();
    await press('Enter');

    expect(host.choices).toEqual([]);
  });

  it('leaves excluded ingredients out and asks for enough results to fill the list', async () => {
    const { search, options } = await render((h) => h.exclude.set(['onion']));

    const request = await search('ceb', [onion, redOnion]);

    expect(request.params.get('pageSize')).toBe(String(INGREDIENT_PICKER_RESULTS + 1));
    expect(options().map((o) => o.querySelector('[data-testid="option-name"]')?.textContent)).toEqual([
      'Cebula czerwona',
      undefined,
    ]);
    expect(options()[1].getAttribute('data-testid')).toBe('new-option');
  });

  it('closes with Escape without letting a surrounding dialog close too', async () => {
    const { search, press, field, listbox } = await render();
    await search('ceb', [onion]);

    const escape = await press('Escape');

    expect(escape.defaultPrevented).toBe(true);
    expect(field.getAttribute('aria-expanded')).toBe('false');
    expect(listbox().hidden).toBe(true);
    const second = await press('Escape');
    expect(second.defaultPrevented).toBe(false);
  });

  it('opens again with ArrowDown', async () => {
    const { search, press, field } = await render();
    await search('ceb', [onion]);
    await press('Escape');

    await press('ArrowDown');

    expect(field.getAttribute('aria-expanded')).toBe('true');
  });

  it('does not search for blank text', async () => {
    const { type, field } = await render();

    await type('   ');

    http.expectNone(() => true);
    expect(field.getAttribute('aria-expanded')).toBe('false');
  });

  it('says when the search fails', async () => {
    const { type, page, settle } = await render();
    await type('ceb');

    http.expectOne((r) => r.url === INGREDIENTS_URL).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Search is not working right now');
  });

  it('speaks Polish', async () => {
    TestBed.inject(TranslocoService).setActiveLang('pl');
    const { search, page, field, options } = await render((h) => h.taken.set(['cheese']));

    expect(field.placeholder).toBe('Szukaj składnika');
    await search('gouda', [yellowCheese]);

    expect(options()[0].textContent).toContain('pasuje:');
    expect(options()[0].textContent).toContain('już na liście');
    expect(options()[0].textContent).toContain('do sprawdzenia');
    expect(page.querySelector('[data-testid="new-option"]')).toBeNull();
  });
});
