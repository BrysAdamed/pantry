import { Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { THEME_PREFERENCES } from './theme';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-theme-switcher',
  imports: [TranslocoDirective, HlmButton],
  template: `
    <div *transloco="let t" role="group" class="flex gap-1" [attr.aria-label]="t('theme.label')">
      @for (preference of preferences; track preference) {
        <button
          hlmBtn
          size="sm"
          [variant]="preference === themeService.preference() ? 'default' : 'ghost'"
          [attr.aria-pressed]="preference === themeService.preference()"
          [attr.data-testid]="'theme-' + preference"
          (click)="themeService.use(preference)"
        >
          {{ t('theme.' + preference) }}
        </button>
      }
    </div>
  `,
})
export class ThemeSwitcher {
  protected readonly themeService = inject(ThemeService);
  protected readonly preferences = THEME_PREFERENCES;
}
