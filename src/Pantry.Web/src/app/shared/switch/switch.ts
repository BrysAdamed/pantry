import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** On/off switch from the design prototypes, with a full tap target around the track. */
@Component({
  selector: 'app-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <button
      type="button"
      role="switch"
      class="group grid h-tap place-items-center rounded-control outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:opacity-45"
      [attr.aria-checked]="checked()"
      [attr.aria-label]="label()"
      [disabled]="disabled()"
      (click)="toggled.emit(!checked())"
    >
      <span
        class="relative h-7 w-11 rounded-full border-2 border-outline bg-secondary transition-colors group-aria-checked:bg-accent after:absolute after:top-[3px] after:left-[3px] after:size-[18px] after:rounded-full after:bg-outline after:transition-[left] group-aria-checked:after:left-[19px]"
        aria-hidden="true"
      ></span>
    </button>
  `,
})
export class Switch {
  readonly checked = input.required<boolean>();
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly toggled = output<boolean>();
}
