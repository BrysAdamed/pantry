import { Component } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { LanguageSwitcher } from '../account/language-switcher';
import { ThemeSwitcher } from '../account/theme-switcher';

interface Swatch {
  token: string;
  className: string;
}

interface StateSample {
  labelKey: string;
  className: string;
}

interface StateGroup {
  titleKey: string;
  samples: StateSample[];
}

const SWATCHES: Swatch[] = [
  { token: 'background', className: 'bg-background' },
  { token: 'card', className: 'bg-card' },
  { token: 'secondary', className: 'bg-secondary' },
  { token: 'foreground', className: 'bg-foreground' },
  { token: 'muted-foreground', className: 'bg-muted-foreground' },
  { token: 'outline', className: 'bg-outline' },
  { token: 'primary', className: 'bg-primary' },
  { token: 'accent', className: 'bg-accent' },
  { token: 'agent', className: 'bg-agent' },
  { token: 'success', className: 'bg-success' },
  { token: 'warning', className: 'bg-warning' },
  { token: 'danger', className: 'bg-danger' },
];

const CHIP = 'rounded-chip border-2 px-2.5 py-1 text-sm font-semibold';

const STATE_GROUPS: StateGroup[] = [
  {
    titleKey: 'designSystem.planEntry',
    samples: [
      { labelKey: 'planEntry.state.proposed', className: `${CHIP} border-dashed border-entry-proposed bg-entry-proposed-surface text-entry-proposed` },
      { labelKey: 'planEntry.state.accepted', className: `${CHIP} border-outline bg-entry-accepted-surface text-entry-accepted shadow-control` },
      { labelKey: 'planEntry.state.eaten', className: `${CHIP} border-outline bg-entry-eaten-surface text-entry-eaten line-through` },
      { labelKey: 'planEntry.leftover', className: `${CHIP} border-outline bg-entry-leftover-surface text-entry-leftover` },
      { labelKey: 'planEntry.emptySlotMarker', className: `${CHIP} border-dotted border-empty-slot-marker bg-empty-slot-marker-surface text-empty-slot-marker` },
    ],
  },
  {
    titleKey: 'designSystem.listItem',
    samples: [
      { labelKey: 'listItem.state.open', className: `${CHIP} border-outline bg-card text-item-open` },
      { labelKey: 'listItem.state.haveAtHome', className: `${CHIP} border-border bg-card text-item-have-at-home` },
      { labelKey: 'listItem.state.bought', className: `${CHIP} border-border bg-card text-item-bought line-through` },
    ],
  },
  {
    titleKey: 'designSystem.cartRunReport',
    samples: [
      { labelKey: 'cartRunReport.result.added', className: `${CHIP} border-report-added bg-report-added-surface text-report-added` },
      { labelKey: 'cartRunReport.result.quantityChanged', className: `${CHIP} border-report-quantity-changed bg-report-quantity-changed-surface text-report-quantity-changed` },
      { labelKey: 'cartRunReport.result.substituted', className: `${CHIP} border-report-substituted bg-report-substituted-surface text-report-substituted` },
      { labelKey: 'cartRunReport.result.missing', className: `${CHIP} border-report-missing bg-report-missing-surface text-report-missing` },
    ],
  },
  {
    titleKey: 'designSystem.dislike',
    samples: [
      { labelKey: 'dislike.level.never', className: `${CHIP} border-dislike-never bg-dislike-never-surface text-dislike-never` },
      { labelKey: 'dislike.level.avoid', className: `${CHIP} border-dislike-avoid bg-dislike-avoid-surface text-dislike-avoid` },
    ],
  },
  {
    titleKey: 'designSystem.sync',
    samples: [
      { labelKey: 'sync.state.online', className: `${CHIP} border-sync-online bg-card text-sync-online` },
      { labelKey: 'sync.state.offline', className: `${CHIP} border-sync-offline bg-card text-sync-offline` },
      { labelKey: 'sync.state.pending', className: `${CHIP} border-sync-pending bg-card text-sync-pending` },
    ],
  },
];

@Component({
  selector: 'app-token-showcase',
  imports: [TranslocoDirective, HlmButton, LanguageSwitcher, ThemeSwitcher],
  template: `
    <main *transloco="let t" class="mx-auto max-w-5xl space-y-10 p-4 sm:p-8">
      <header class="flex flex-wrap items-end justify-between gap-4">
        <div class="space-y-1">
          <h1 class="text-4xl">{{ t('designSystem.title') }}</h1>
          <p class="text-muted-foreground">{{ t('designSystem.intro') }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <app-language-switcher />
          <app-theme-switcher />
        </div>
      </header>

      <section class="space-y-4">
        <h2 class="text-2xl">{{ t('designSystem.colours') }}</h2>
        <ul class="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
          @for (swatch of swatches; track swatch.token) {
            <li class="overflow-hidden rounded-card border-2 border-outline bg-card shadow-card">
              <div class="h-16 border-b-2 border-outline" [class]="swatch.className"></div>
              <code class="block p-2 text-xs">{{ swatch.token }}</code>
            </li>
          }
        </ul>
      </section>

      <section class="space-y-4">
        <h2 class="text-2xl">{{ t('designSystem.typography') }}</h2>
        <div class="space-y-2 rounded-card border-2 border-outline bg-card p-5 shadow-card">
          <p class="font-display text-4xl font-extrabold tracking-tight">{{ t('designSystem.typographySample') }}</p>
          <p class="text-lg">{{ t('designSystem.typographySample') }}</p>
          <p class="text-sm text-muted-foreground">{{ t('designSystem.typographySample') }}</p>
        </div>
      </section>

      <section class="space-y-4">
        <h2 class="text-2xl">{{ t('designSystem.buttons') }}</h2>
        <div class="flex flex-wrap items-center gap-3">
          @for (variant of variants; track variant) {
            <button hlmBtn [variant]="variant">{{ variant }}</button>
          }
        </div>
        <div class="flex flex-wrap items-center gap-3">
          <button hlmBtn size="lg">lg</button>
          <button hlmBtn>default</button>
          <button hlmBtn size="sm">sm</button>
          <button hlmBtn size="xs">xs</button>
          <button hlmBtn disabled>disabled</button>
        </div>
      </section>

      <section class="space-y-4">
        <h2 class="text-2xl">{{ t('designSystem.states') }}</h2>
        <div class="grid gap-4 sm:grid-cols-2">
          @for (group of stateGroups; track group.titleKey) {
            <div class="space-y-3 rounded-card border-2 border-outline bg-card p-4 shadow-card">
              <h3 class="text-lg">{{ t(group.titleKey) }}</h3>
              <div class="flex flex-wrap gap-2">
                @for (sample of group.samples; track sample.labelKey) {
                  <span [class]="sample.className">{{ t(sample.labelKey) }}</span>
                }
              </div>
            </div>
          }
        </div>
      </section>
    </main>
  `,
})
export class TokenShowcase {
  protected readonly swatches = SWATCHES;
  protected readonly stateGroups = STATE_GROUPS;
  protected readonly variants = ['default', 'secondary', 'outline', 'ghost', 'destructive', 'link'] as const;
}
