import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type EmptyStateIllustration = 'planner' | 'recipes' | 'lists' | 'household' | 'ingredients';

/**
 * Empty state from docs/design/empty-states.html: illustration, heading naming the goal, one sentence,
 * an optional agent hint and projected actions.
 */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid justify-items-center content-start gap-3.5 px-6 py-7 text-center' },
  styles: `
    :host {
      --ill-ink: light-dark(#2a1b2d, #fbefe3);
      --ill-fill: light-dark(#fff4e6, #322536);
    }
  `,
  template: `
    <svg viewBox="0 0 132 112" class="h-28 w-33" aria-hidden="true" stroke-width="4">
      @switch (illustration()) {
        @case ('planner') {
          <rect x="14" y="18" width="104" height="84" rx="14" fill="var(--ill-fill)" stroke="var(--ill-ink)" />
          <path d="M14 40h104" stroke="var(--ill-ink)" />
          <path d="M38 10v16M94 10v16" stroke="var(--ill-ink)" stroke-linecap="round" />
          <g fill="none" stroke="var(--ill-ink)" stroke-width="3" stroke-dasharray="5 5" opacity=".5">
            <rect x="26" y="52" width="22" height="18" rx="5" />
            <rect x="55" y="52" width="22" height="18" rx="5" />
            <rect x="84" y="52" width="22" height="18" rx="5" />
            <rect x="26" y="76" width="22" height="18" rx="5" />
            <rect x="55" y="76" width="22" height="18" rx="5" />
          </g>
          <rect x="84" y="76" width="22" height="18" rx="5" fill="var(--accent)" stroke="var(--ill-ink)" stroke-width="3" />
        }
        @case ('recipes') {
          <path d="M24 22h70a10 10 0 0 1 10 10v66H34a10 10 0 0 1-10-10z" fill="var(--ill-fill)" stroke="var(--ill-ink)" />
          <path d="M24 88a10 10 0 0 1 10-10h70" fill="none" stroke="var(--ill-ink)" />
          <path d="M44 40h40M44 52h28" stroke="var(--ill-ink)" stroke-linecap="round" opacity=".5" />
          <circle cx="104" cy="28" r="14" fill="var(--accent)" stroke="var(--ill-ink)" />
          <path d="M104 21v14M97 28h14" stroke="var(--ill-ink)" stroke-linecap="round" />
        }
        @case ('lists') {
          <path d="M30 40h72l-6 58H36z" fill="var(--ill-fill)" stroke="var(--ill-ink)" stroke-linejoin="round" />
          <path d="M48 40V32a18 18 0 0 1 36 0v8" fill="none" stroke="var(--ill-ink)" />
          <circle cx="96" cy="88" r="16" fill="var(--accent)" stroke="var(--ill-ink)" />
          <path d="M89 88l5 5 9-10" fill="none" stroke="var(--ill-ink)" stroke-linecap="round" stroke-linejoin="round" />
        }
        @case ('household') {
          <path d="M20 54L66 16l46 38v42a6 6 0 0 1-6 6H26a6 6 0 0 1-6-6z" fill="var(--ill-fill)" stroke="var(--ill-ink)" stroke-linejoin="round" />
          <path d="M36 62h18M36 74h12" stroke="var(--ill-ink)" stroke-linecap="round" opacity=".5" />
          <rect x="70" y="62" width="24" height="40" rx="5" fill="var(--accent)" stroke="var(--ill-ink)" />
        }
        @case ('ingredients') {
          <rect x="22" y="14" width="88" height="88" rx="24" fill="var(--ill-fill)" stroke="var(--ill-ink)" />
          <path d="M76 50L42 84a5 5 0 0 1-7-7l34-34" fill="var(--accent)" stroke="var(--ill-ink)" stroke-linejoin="round" />
          <path d="M69 43a10 10 0 0 1 14 14" fill="none" stroke="var(--ill-ink)" />
          <path d="M80 46l14-14M77 37l5-9M89 49l9-5" stroke="var(--ill-ink)" stroke-linecap="round" />
        }
      }
    </svg>
    <h2 class="text-2xl">{{ heading() }}</h2>
    <p class="max-w-[34ch] text-muted-foreground">{{ description() }}</p>
    @if (agentHint(); as hint) {
      <p class="max-w-[34ch] rounded-control border-2 border-dashed border-agent bg-agent-surface px-3 py-2 text-sm text-agent">
        {{ hint }}
      </p>
    }
    <div class="mt-1 grid justify-items-center gap-1.5 empty:hidden"><ng-content /></div>
  `,
})
export class EmptyState {
  readonly illustration = input.required<EmptyStateIllustration>();
  readonly heading = input.required<string>();
  readonly description = input.required<string>();
  readonly agentHint = input<string>();
}
