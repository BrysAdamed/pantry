import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { COOLDOWN_URL } from '../household-api';
import { CooldownSetting } from './cooldown-setting';

describe('CooldownSetting', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CooldownSetting],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(cooldownDays = 21) {
    const fixture = TestBed.createComponent(CooldownSetting);
    fixture.detectChanges();
    http.expectOne(COOLDOWN_URL).flush({ cooldownDays });
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const input = () => page.querySelector<HTMLInputElement>('input[type="number"]')!;
    const enter = (value: string) => {
      input().value = value;
      input().dispatchEvent(new Event('change'));
    };
    return { page, settle, input, enter };
  }

  it('shows the household cooldown in days and what it does', async () => {
    const { page, input } = await render();

    expect(input().value).toBe('21');
    expect(page.textContent).toContain('Break before a repeat');
    expect(page.textContent).toContain('0 turns the break off.');
  });

  it('saves a new number of days when the field is left', async () => {
    const { enter, settle } = await render();

    enter('14');
    await settle();

    const save = http.expectOne({ method: 'PUT', url: COOLDOWN_URL });
    expect(save.request.body).toEqual({ cooldownDays: 14 });
    save.flush({ cooldownDays: 14 });
  });

  it('does not save the same number again', async () => {
    const { enter, settle } = await render();

    enter('21');
    await settle();

    http.expectNone({ method: 'PUT', url: COOLDOWN_URL });
  });

  it('rejects days outside zero to a year without saving', async () => {
    const { page, enter, settle, input } = await render();

    for (const value of ['-1', '366', '2.5', '']) {
      enter(value);
      await settle();

      expect(page.querySelector('[role="alert"]')?.textContent).toContain('Enter a whole number of days from 0 to 365.');
      expect(input().getAttribute('aria-invalid')).toBe('true');
    }
    http.expectNone({ method: 'PUT', url: COOLDOWN_URL });
  });

  it('reports a failed save and shows the saved days again', async () => {
    const { page, enter, settle, input } = await render();

    enter('7');
    await settle();
    http.expectOne({ method: 'PUT', url: COOLDOWN_URL }).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();

    http.expectOne({ method: 'GET', url: COOLDOWN_URL }).flush({ cooldownDays: 21 });
    await settle();
    expect(input().value).toBe('21');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The change was not saved');
  });
});
