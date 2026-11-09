import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { SignInMethod } from '../identity-api';

const METHODS: readonly SignInMethod[] = ['passkey', 'password'];

/** Picks how a new account signs in: a passkey or a username and password. */
@Component({
  selector: 'app-sign-in-method-choice',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *transloco="let t; prefix: 'password'" role="group" class="grid w-full grid-cols-2 gap-1" [attr.aria-label]="t('method.label')">
      @for (method of methods; track method) {
        <button
          hlmBtn
          size="sm"
          type="button"
          [variant]="method === value() ? 'default' : 'outline'"
          [attr.aria-pressed]="method === value()"
          [attr.data-testid]="'method-' + method"
          [disabled]="disabled()"
          (click)="value.set(method)"
        >
          {{ t('method.' + method) }}
        </button>
      }
    </div>
  `,
})
export class SignInMethodChoice {
  readonly value = model.required<SignInMethod>();
  readonly disabled = input(false);

  protected readonly methods = METHODS;
}
