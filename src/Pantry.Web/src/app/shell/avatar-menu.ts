import { A11yModule } from '@angular/cdk/a11y';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { LanguageSwitcher } from '../core/i18n/language-switcher';
import { ThemeSwitcher } from '../core/theme/theme-switcher';
import { Session, SIGN_IN_PATH } from '../identity/session';
import { Icon } from '../shared/icon/icon';

const POSITIONS: Record<'side' | 'below', ConnectedPosition[]> = {
  side: [{ originX: 'end', originY: 'bottom', overlayX: 'start', overlayY: 'bottom', offsetX: 28 }],
  below: [{ originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 8 }],
};

let nextId = 0;

/** Avatar button that opens the account menu: UI language, theme and sign-out. */
@Component({
  selector: 'app-avatar-menu',
  imports: [A11yModule, CdkOverlayOrigin, CdkConnectedOverlay, TranslocoDirective, HlmButton, LanguageSwitcher, ThemeSwitcher, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      <button
        #origin="cdkOverlayOrigin"
        cdkOverlayOrigin
        type="button"
        class="grid size-tap place-items-center rounded-full border-2 border-outline bg-secondary text-secondary-foreground shadow-control active:translate-px active:shadow-none"
        data-testid="avatar-menu-trigger"
        aria-haspopup="dialog"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="open() ? panelId : null"
        [attr.aria-label]="t('shell.account')"
        (click)="open.set(!open())"
      >
        <app-icon name="user" class="size-5" />
      </button>

      <ng-template
        cdkConnectedOverlay
        [cdkConnectedOverlayOrigin]="origin"
        [cdkConnectedOverlayOpen]="open()"
        [cdkConnectedOverlayPositions]="positions()"
        (overlayOutsideClick)="closeUnlessOrigin($event, origin.elementRef.nativeElement)"
        (overlayKeydown)="closeOnEscape($event)"
        (detach)="open.set(false)"
      >
        <div
          role="dialog"
          cdkTrapFocus
          [cdkTrapFocusAutoCapture]="true"
          [id]="panelId"
          [attr.aria-label]="t('shell.account')"
          class="grid w-72 gap-3 rounded-card border-2 border-outline bg-popover p-3.5 text-popover-foreground shadow-card"
        >
          <div class="grid gap-1">
            <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground" aria-hidden="true">{{ t('language.label') }}</span>
            <app-language-switcher />
          </div>
          <div class="grid gap-1">
            <span class="text-xs font-bold uppercase tracking-wider text-muted-foreground" aria-hidden="true">{{ t('theme.label') }}</span>
            <app-theme-switcher />
          </div>
          <button
            hlmBtn
            variant="ghost"
            size="sm"
            type="button"
            class="min-h-tap justify-self-start text-danger"
            data-testid="sign-out"
            [disabled]="signingOut()"
            (click)="signOut()"
          >
            <app-icon name="signOut" class="size-4" />
            {{ t('shell.signOut') }}
          </button>
        </div>
      </ng-template>
    </ng-container>
  `,
})
export class AvatarMenu {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  readonly placement = input<'side' | 'below'>('side');

  protected readonly open = signal(false);
  protected readonly positions = computed(() => POSITIONS[this.placement()]);
  protected readonly panelId = `avatar-menu-${nextId++}`;
  protected readonly signingOut = signal(false);

  protected async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.session.signOut().catch(() => undefined);
    } finally {
      this.open.set(false);
      this.signingOut.set(false);
      await this.router.navigateByUrl(`/${SIGN_IN_PATH}`, { replaceUrl: true });
    }
  }

  protected closeUnlessOrigin(event: MouseEvent, origin: HTMLElement): void {
    if (!origin.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  protected closeOnEscape(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.open.set(false);
    }
  }
}
