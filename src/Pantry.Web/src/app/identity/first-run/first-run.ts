import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { Session } from '../session';
import { SetupStatusService } from '../setup-status';

export type FirstRunState = 'form' | 'creating' | 'cancelled' | 'failed' | 'unsupported' | 'alreadyDone' | 'done';

export const DISPLAY_NAME_MAX_LENGTH = 50;

@Component({
  selector: 'app-first-run',
  imports: [TranslocoDirective, HlmButton],
  templateUrl: './first-run.html',
})
export class FirstRun {
  private readonly api = inject(IdentityApi);
  private readonly passkeys = inject(Passkeys);
  private readonly setupStatus = inject(SetupStatusService);
  private readonly router = inject(Router);
  private readonly session = inject(Session);
  private readonly uiLanguage = inject(UiLanguageService).current;

  protected readonly maxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly displayName = signal('');
  protected readonly state = signal<FirstRunState>(this.passkeys.isSupported() ? 'form' : 'unsupported');
  protected readonly canCreate = computed(
    () => this.displayName().trim().length > 0 && (this.state() === 'form' || this.isRetryable()),
  );
  protected readonly isRetryable = computed(() => this.state() === 'cancelled' || this.state() === 'failed');
  protected readonly showsForm = computed(() => this.state() !== 'done' && this.state() !== 'alreadyDone');

  protected async createPasskey(): Promise<void> {
    if (!this.canCreate()) {
      return;
    }
    this.state.set('creating');
    try {
      const options = await this.api.firstRunPasskeyOptions(this.displayName().trim());
      const credential = await this.passkeys.create(options);
      this.session.signedIn(await this.api.completeFirstRun(credential, this.uiLanguage()));
      this.setupStatus.markDone();
      this.state.set('done');
    } catch (error) {
      this.state.set(stateFor(error));
    }
  }

  protected openApp(): void {
    this.setupStatus.markDone();
    void this.router.navigateByUrl('/');
  }
}

function stateFor(error: unknown): FirstRunState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof PasskeyUnsupportedError) {
    return 'unsupported';
  }
  if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict) {
    return 'alreadyDone';
  }
  return 'failed';
}
