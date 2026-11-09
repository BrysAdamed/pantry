import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { SyncState, SyncStatusService } from './sync-status.service';

const STATE_CLASS: Record<SyncState, string> = {
  online: 'text-sync-online',
  offline: 'text-sync-offline',
  pending: 'text-sync-pending',
};

/** Sync state: a dot with a short label under it in the rail, or a pill in a screen header. */
@Component({
  selector: 'app-sync-indicator',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      *transloco="let t"
      role="status"
      data-testid="sync-indicator"
      [title]="t('sync.detail.' + sync.state())"
      [class]="stateClass()"
      [class.pill]="variant() === 'pill'"
      [class.rail]="variant() === 'rail'"
    >
      <span class="dot" aria-hidden="true"></span>
      <span class="sr-only">{{ t('sync.detail.' + sync.state()) }}</span>
      <span aria-hidden="true">{{ t('sync.state.' + sync.state()) }}</span>
    </span>
  `,
  styles: `
    .rail {
      display: grid;
      justify-items: center;
      gap: 4px;
      padding: 4px;
      font-size: 10px;
      font-weight: 800;
      line-height: 1.15;
      text-align: center;
    }
    .rail .dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      background: currentColor;
      border: 2px solid var(--card);
      outline: 2px solid currentColor;
    }
    .pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-height: 36px;
      padding: 6px 10px;
      font-size: 12px;
      font-weight: 800;
      border: 2px solid currentColor;
      border-radius: var(--radius-control);
      background: var(--card);
    }
    .pill .dot {
      width: 9px;
      height: 9px;
      border-radius: 50%;
      background: currentColor;
    }
  `,
})
export class SyncIndicator {
  protected readonly sync = inject(SyncStatusService);

  readonly variant = input<'rail' | 'pill'>('pill');

  protected readonly stateClass = computed(() => STATE_CLASS[this.sync.state()]);
}
