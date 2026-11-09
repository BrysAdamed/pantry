import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { NewCredentials } from '../identity-api';
import {
  cleanUserName,
  isValidPassword,
  isValidUserName,
  PASSWORD_MAX_LENGTH,
  USER_NAME_MAX_LENGTH,
} from './password-rules';

let nextId = 0;

/**
 * A new password typed twice, and a username while the user has none. Emits the credentials whenever they become valid,
 * and `null` while they are not. A field shows its problem once the person has left it.
 */
@Component({
  selector: 'app-new-password-fields',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid w-full gap-3 text-left' },
  template: `
    <ng-container *transloco="let t; prefix: 'password'">
      <div class="grid gap-1.5">
        <label class="text-sm font-bold" [for]="id + '-user-name'">{{ t('userNameLabel') }}</label>
        @if (userName(); as fixed) {
          <input
            class="min-h-tap w-full min-w-0 rounded-control border-2 border-border bg-secondary px-3 py-2"
            [id]="id + '-user-name'"
            name="username"
            autocomplete="username"
            data-testid="new-user-name"
            readonly
            [value]="fixed"
          />
        } @else {
          <input
            class="min-h-tap w-full min-w-0 rounded-control border-2 border-outline bg-card px-3 py-2 disabled:opacity-45"
            [id]="id + '-user-name'"
            [attr.aria-describedby]="id + '-user-name-rule'"
            name="username"
            autocomplete="username"
            autocapitalize="none"
            spellcheck="false"
            data-testid="new-user-name"
            [maxLength]="userNameMaxLength"
            [disabled]="disabled()"
            [attr.aria-invalid]="userNameProblem()"
            (input)="typedUserName.set($any($event.target).value); emit()"
            (blur)="userNameLeft.set(true)"
          />
          <span class="text-xs" [id]="id + '-user-name-rule'" [class.text-danger]="userNameProblem()" [class.text-muted-foreground]="!userNameProblem()">
            {{ t('userNameRule') }}
          </span>
        }
      </div>

      <div class="grid gap-1.5">
        <label class="text-sm font-bold" [for]="id + '-password'">{{ t('passwordLabel') }}</label>
        <input
          class="min-h-tap w-full min-w-0 rounded-control border-2 border-outline bg-card px-3 py-2 disabled:opacity-45"
          [id]="id + '-password'"
          [attr.aria-describedby]="id + '-password-rule'"
          type="password"
          name="new-password"
          autocomplete="new-password"
          data-testid="new-password"
          [maxLength]="passwordMaxLength"
          [disabled]="disabled()"
          [attr.aria-invalid]="passwordProblem()"
          (input)="password.set($any($event.target).value); emit()"
          (blur)="passwordLeft.set(true)"
        />
        <span class="text-xs" [id]="id + '-password-rule'" [class.text-danger]="passwordProblem()" [class.text-muted-foreground]="!passwordProblem()">
          {{ t('passwordRule') }}
        </span>
      </div>

      <div class="grid gap-1.5">
        <label class="text-sm font-bold" [for]="id + '-confirmation'">{{ t('confirmationLabel') }}</label>
        <input
          class="min-h-tap w-full min-w-0 rounded-control border-2 border-outline bg-card px-3 py-2 disabled:opacity-45"
          [id]="id + '-confirmation'"
          [attr.aria-describedby]="confirmationProblem() ? id + '-mismatch' : null"
          type="password"
          name="new-password-confirmation"
          autocomplete="new-password"
          data-testid="new-password-confirmation"
          [maxLength]="passwordMaxLength"
          [disabled]="disabled()"
          [attr.aria-invalid]="confirmationProblem()"
          (input)="confirmation.set($any($event.target).value); emit()"
          (blur)="confirmationLeft.set(true)"
        />
        @if (confirmationProblem()) {
          <span class="text-xs text-danger" data-testid="new-password-mismatch" [id]="id + '-mismatch'">{{ t('mismatch') }}</span>
        }
      </div>
    </ng-container>
  `,
})
export class NewPasswordFields {
  /** The user's username when they already have one; shown read-only so a password manager saves it with the password. */
  readonly userName = input<string | null>(null);
  readonly disabled = input(false);
  readonly credentials = output<NewCredentials | null>();

  protected readonly id = `new-password-${nextId++}`;
  protected readonly userNameMaxLength = USER_NAME_MAX_LENGTH;
  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;
  protected readonly typedUserName = signal('');
  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly userNameLeft = signal(false);
  protected readonly passwordLeft = signal(false);
  protected readonly confirmationLeft = signal(false);

  protected readonly userNameProblem = computed(() => this.userNameLeft() && !isValidUserName(this.typedUserName()));
  protected readonly passwordProblem = computed(() => this.passwordLeft() && !isValidPassword(this.password()));
  protected readonly confirmationProblem = computed(
    () => this.confirmationLeft() && this.confirmation() !== this.password(),
  );

  private readonly value = computed<NewCredentials | null>(() => {
    const askedUserName = this.userName() === null;
    const valid =
      (!askedUserName || isValidUserName(this.typedUserName())) &&
      isValidPassword(this.password()) &&
      this.confirmation() === this.password();
    return valid ? { userName: askedUserName ? cleanUserName(this.typedUserName()) : null, password: this.password() } : null;
  });

  protected emit(): void {
    this.credentials.emit(this.value());
  }
}
