import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import { DESKTOP_QUERY, mediaQuery } from '../core/layout/media-query';
import { Icon } from '../shared/icon/icon';
import { DEFAULT_SECTION, isSettingsSection, SECTION_ICONS, SETTINGS_GROUPS } from './settings-sections';

/**
 * The *Dom* destination: section list and the open section side by side from `md` up,
 * a drill-down (list, then section with a back link) on phones.
 */
@Component({
  selector: 'app-household-settings',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoDirective, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './household-settings.html',
})
export class HouseholdSettings {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly groups = SETTINGS_GROUPS;
  protected readonly icons = SECTION_ICONS;
  protected readonly desktop = mediaQuery(DESKTOP_QUERY);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly section = computed(() => {
    const segment = this.url().split(/[?#]/)[0].split('/')[2];
    return isSettingsSection(segment) ? segment : null;
  });

  constructor() {
    effect(() => {
      if (this.desktop() && !this.section()) {
        void this.router.navigate([DEFAULT_SECTION], { relativeTo: this.route, replaceUrl: true });
      }
    });
  }
}
