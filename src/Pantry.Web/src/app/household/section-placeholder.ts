import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { SettingsSection } from './settings-sections';

/** A settings section that is not built yet. Set the section as the `section` route data. */
@Component({
  selector: 'app-section-placeholder',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  template: `
    <ng-container *transloco="let t; prefix: 'household'">
      <div class="grid gap-1">
        <h2 class="text-2xl md:text-[30px]">{{ t('sections.' + section() + '.title') }}</h2>
        <p class="max-w-[70ch] text-sm text-muted-foreground">{{ t('sections.' + section() + '.description') }}</p>
      </div>
      <p
        class="rounded-card border-2 border-dashed border-border px-4.5 py-4 text-sm text-muted-foreground"
        data-testid="section-placeholder"
      >
        {{ t('notBuiltYet') }}
      </p>
    </ng-container>
  `,
})
export class SectionPlaceholder {
  readonly section = input.required<SettingsSection>();
}
