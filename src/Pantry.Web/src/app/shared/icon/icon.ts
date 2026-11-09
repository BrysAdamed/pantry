import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type IconName = 'jar' | 'calendar' | 'book' | 'cart' | 'home' | 'user' | 'signOut';

/** Decorative line icon from the design prototypes. Label the surrounding control, not the icon. */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0', 'aria-hidden': 'true' },
  template: `
    <svg
      viewBox="0 0 24 24"
      class="size-full"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      @switch (name()) {
        @case ('jar') {
          <path d="M7 3h10M8 3v3l-2 3v10a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V9l-2-3V3" />
          <path d="M6 12h12" />
        }
        @case ('calendar') {
          <rect x="3" y="4" width="18" height="17" rx="2" />
          <path d="M3 9h18M8 2v4M16 2v4" />
        }
        @case ('book') {
          <path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z" />
          <path d="M4 19V5M9 7h6" />
        }
        @case ('cart') {
          <circle cx="9" cy="20" r="1.5" />
          <circle cx="18" cy="20" r="1.5" />
          <path d="M2 3h3l2.5 12h11l2-8H6" />
        }
        @case ('home') {
          <path d="M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z" />
        }
        @case ('user') {
          <circle cx="12" cy="8" r="4" />
          <path d="M4 21a8 8 0 0 1 16 0" />
        }
        @case ('signOut') {
          <path d="M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10" />
        }
      }
    </svg>
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
}
