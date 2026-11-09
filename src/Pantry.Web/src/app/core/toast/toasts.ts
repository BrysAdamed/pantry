import { ChangeDetectionStrategy, Component, DestroyRef, inject, Injectable, signal } from '@angular/core';

export const TOAST_DURATION_MS = 2400;

/** Short confirmations of a change that can be undone or needs no decision ("Ingredient saved"), one at a time. */
@Injectable({ providedIn: 'root' })
export class Toasts {
  private readonly current = signal<string | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;

  readonly message = this.current.asReadonly();

  show(message: string): void {
    clearTimeout(this.timer);
    this.current.set(message);
    this.timer = setTimeout(() => this.current.set(null), TOAST_DURATION_MS);
  }

  dismiss(): void {
    clearTimeout(this.timer);
    this.current.set(null);
  }
}

/** Where toasts show: above the bottom tabs on phones, bottom centre from `md` up. Rendered once, in the shell. */
@Component({
  selector: 'app-toasts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message(); as text) {
      <div
        role="status"
        class="fixed bottom-24 left-1/2 z-40 max-w-[calc(100vw-2rem)] -translate-x-1/2 rounded-control bg-foreground px-4 py-2.5 text-sm font-bold text-background shadow-control md:bottom-6"
        data-testid="toast"
      >
        {{ text }}
      </div>
    }
  `,
})
export class ToastOutlet {
  private readonly toasts = inject(Toasts);

  protected readonly message = this.toasts.message;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.toasts.dismiss());
  }
}
