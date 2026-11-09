import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { ChangeDetectionStrategy, Component, inject, Injectable, input, TemplateRef } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DESKTOP_QUERY } from '../../core/layout/media-query';

/** What a dialog template gets: `let-data` and `let-dialog="dialogRef"` (close it with a result). */
export interface DialogContext<D, R> {
  $implicit: D;
  dialogRef: DialogRef<R>;
}

/**
 * Modal dialogs from the design prototypes (CDK dialog, ADR 0009): centred from `md` up, a bottom sheet on phones.
 * Focus is trapped inside and goes back to where it was; Escape and the backdrop close without a result.
 */
@Injectable({ providedIn: 'root' })
export class Dialogs {
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);

  /** Opens `template` and resolves with what it closed with, or undefined when dismissed. */
  open<R, D = undefined>(template: TemplateRef<DialogContext<D, R>>, options: { label: string; data?: D }): Promise<R | undefined> {
    const sheet = typeof matchMedia === 'function' && !matchMedia(DESKTOP_QUERY).matches;
    const position = this.overlay.position().global().centerHorizontally();
    const ref = this.dialog.open<R, D, unknown>(template, {
      data: options.data,
      ariaLabel: options.label,
      width: sheet ? '100%' : '28rem',
      maxWidth: sheet ? '100%' : 'calc(100vw - 2rem)',
      maxHeight: sheet ? '88dvh' : '92dvh',
      positionStrategy: sheet ? position.bottom('0') : position.centerVertically(),
      backdropClass: 'app-dialog-backdrop',
      panelClass: sheet ? 'app-sheet-panel' : 'app-dialog-panel',
    });
    return firstValueFrom(ref.closed);
  }
}

/** The look of a dialog's content: card on desktop, sheet with a handle on phones. Put it at the template root. */
@Component({
  selector: 'app-dialog-frame',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class:
      'grid max-h-[inherit] gap-4 overflow-y-auto border-outline bg-card p-5.5 text-foreground ' +
      'in-[.app-dialog-panel]:rounded-card in-[.app-dialog-panel]:border-2 in-[.app-dialog-panel]:shadow-card ' +
      'in-[.app-sheet-panel]:rounded-t-3xl in-[.app-sheet-panel]:border-t-2 in-[.app-sheet-panel]:px-4.5 in-[.app-sheet-panel]:pt-2 in-[.app-sheet-panel]:pb-6.5',
  },
  template: `
    <div class="mx-auto hidden h-1.25 w-11 rounded-full bg-border in-[.app-sheet-panel]:block" aria-hidden="true"></div>
    @if (label()) {
      <span class="text-xs font-bold tracking-[0.08em] text-muted-foreground uppercase">{{ label() }}</span>
    }
    <ng-content />
  `,
})
export class DialogFrame {
  readonly label = input<string>();
}
