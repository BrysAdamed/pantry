import { Directive, input, signal } from '@angular/core';
import { BrnButton } from '@spartan-ng/brain/button';
import { classes } from '@spartan-ng/helm/utils';
import { cva, type VariantProps } from 'class-variance-authority';
import type { ClassValue } from 'clsx';
import { injectBrnButtonConfig } from './hlm-button.token';

export const buttonVariants = cva(
	"focus-visible:ring-ring/50 data-[matches-spartan-invalid=true]:ring-destructive/30 data-[matches-spartan-invalid=true]:border-destructive rounded-control border-2 border-outline shadow-control bg-clip-padding text-sm font-semibold focus-visible:ring-3 active:not-aria-[haspopup]:translate-[1px] active:not-aria-[haspopup]:shadow-none data-[matches-spartan-invalid=true]:ring-3 [&_ng-icon:not([class*='text-'])]:text-[length:--spacing(4)] group/button inline-flex shrink-0 items-center justify-center whitespace-nowrap transition-[translate,box-shadow,background-color,color] outline-none select-none data-disabled:pointer-events-none data-disabled:opacity-50 [&_ng-icon]:pointer-events-none [&_ng-icon]:shrink-0",
	{
		variants: {
			variant: {
				default: 'bg-primary text-primary-foreground hover:bg-primary/90',
				outline: 'bg-card text-foreground hover:bg-secondary aria-expanded:bg-secondary',
				secondary: 'bg-accent text-accent-foreground hover:bg-accent/85 aria-expanded:bg-accent/85',
				ghost: 'border-transparent shadow-none hover:bg-muted hover:text-foreground aria-expanded:bg-muted',
				destructive: 'bg-card text-destructive hover:bg-danger-surface focus-visible:ring-destructive/30',
				link: 'border-transparent shadow-none text-primary underline-offset-4 hover:underline',
			},
			size: {
				default: 'h-tap gap-1.5 px-4 has-data-[icon=inline-end]:pr-3 has-data-[icon=inline-start]:pl-3',
				xs: "h-7 gap-1 rounded-chip px-2 text-xs pointer-coarse:h-tap [&_ng-icon:not([class*='text-'])]:text-[length:--spacing(3)]",
				sm: 'h-9 gap-1 px-3 pointer-coarse:h-tap',
				lg: 'h-12 gap-2 px-5 text-base',
				icon: 'size-tap',
				'icon-xs': "size-7 rounded-chip pointer-coarse:size-tap [&_ng-icon:not([class*='text-'])]:text-[length:--spacing(3)]",
				'icon-sm': 'size-9 pointer-coarse:size-tap',
				'icon-lg': 'size-12',
			},
		},
		defaultVariants: {
			variant: 'default',
			size: 'default',
		},
	},
);

export type ButtonVariants = VariantProps<typeof buttonVariants>;

@Directive({
	selector: 'button[hlmBtn], a[hlmBtn]',
	exportAs: 'hlmBtn',
	hostDirectives: [{ directive: BrnButton, inputs: ['disabled'] }],
	host: { 'data-slot': 'button' },
})
export class HlmButton {
	private readonly _config = injectBrnButtonConfig();

	private readonly _additionalClasses = signal<ClassValue>('');

	public readonly variant = input<ButtonVariants['variant']>(this._config.variant);

	public readonly size = input<ButtonVariants['size']>(this._config.size);

	constructor() {
		classes(() => [buttonVariants({ variant: this.variant(), size: this.size() }), this._additionalClasses()]);
	}

	setClass(classes: string): void {
		this._additionalClasses.set(classes);
	}
}
