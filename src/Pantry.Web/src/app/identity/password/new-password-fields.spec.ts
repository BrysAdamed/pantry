import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { NewCredentials } from '../identity-api';
import { NewPasswordFields } from './new-password-fields';
import { isValidPassword, isValidUserName } from './password-rules';

@Component({
  imports: [NewPasswordFields],
  template: `<app-new-password-fields [userName]="userName()" (credentials)="emitted.push($event)" />`,
})
class Host {
  readonly userName = signal<string | null>(null);
  readonly emitted: (NewCredentials | null)[] = [];
}

describe('password rules', () => {
  it.each(['abc', 'Ala.Kot_2', 'żółw-42', 'z'.repeat(32)])('accepts the username %s', (userName) => {
    expect(isValidUserName(userName)).toBe(true);
  });

  it.each(['ab', 'a'.repeat(33), 'with space', 'at@sign', '0123456789abcdef0123456789ABCDEF'])('rejects the username %s', (userName) => {
    expect(isValidUserName(userName)).toBe(false);
  });

  it('wants 12 to 128 characters of any kind', () => {
    expect(isValidPassword('a'.repeat(11))).toBe(false);
    expect(isValidPassword('a'.repeat(12))).toBe(true);
    expect(isValidPassword('x'.repeat(128))).toBe(true);
    expect(isValidPassword('x'.repeat(129))).toBe(false);
  });
});

describe('NewPasswordFields', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Host], providers: [provideTranslocoTesting()] }).compileComponents();
  });

  async function render(userName: string | null = null) {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.userName.set(userName);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const field = (id: string) => page.querySelector<HTMLInputElement>(`[data-testid="${id}"]`)!;
    const type = async (id: string, value: string) => {
      field(id).value = value;
      field(id).dispatchEvent(new Event('input'));
      field(id).dispatchEvent(new Event('blur'));
      await fixture.whenStable();
    };
    return { page, field, type, emitted: fixture.componentInstance.emitted };
  }

  it('emits the trimmed username and the password once both are valid and repeated', async () => {
    const { type, emitted } = await render();

    await type('new-user-name', '  alex ');
    await type('new-password', 'twelve chars');
    expect(emitted.at(-1)).toBeNull();
    await type('new-password-confirmation', 'twelve chars');

    expect(emitted.at(-1)).toEqual({ userName: 'alex', password: 'twelve chars' });
  });

  it('shows the rules a field breaks once the person leaves it', async () => {
    const { page, type, field } = await render();

    await type('new-user-name', 'a b');
    await type('new-password', 'short');
    await type('new-password-confirmation', 'other');

    expect(field('new-user-name').getAttribute('aria-invalid')).toBe('true');
    expect(field('new-password').getAttribute('aria-invalid')).toBe('true');
    expect(page.querySelector('[data-testid="new-password-mismatch"]')?.textContent).toContain('The passwords do not match.');
  });

  it('shows a known username read-only and does not send it again', async () => {
    const { type, field, emitted } = await render('alex');

    await type('new-password', 'a brand new password');
    await type('new-password-confirmation', 'a brand new password');

    expect(field('new-user-name').readOnly).toBe(true);
    expect(field('new-user-name').value).toBe('alex');
    expect(field('new-user-name').getAttribute('autocomplete')).toBe('username');
    expect(emitted.at(-1)).toEqual({ userName: null, password: 'a brand new password' });
  });
});
