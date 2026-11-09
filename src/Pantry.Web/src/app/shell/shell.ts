import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import { SyncIndicator } from '../core/sync/sync-indicator';
import { Icon } from '../shared/icon/icon';
import { AvatarMenu } from './avatar-menu';
import { DESTINATION_ICONS, DESTINATIONS } from './destinations';

/** App frame: icon rail on wide screens, header and bottom tabs on phones, routed screen in between. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoDirective, SyncIndicator, AvatarMenu, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
})
export class Shell {
  private readonly router = inject(Router);

  protected readonly destinations = DESTINATIONS;
  protected readonly icons = DESTINATION_ICONS;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly current = computed(() => {
    const segment = this.url().split(/[/?#]/)[1];
    return this.destinations.find((destination) => destination === segment);
  });
}
