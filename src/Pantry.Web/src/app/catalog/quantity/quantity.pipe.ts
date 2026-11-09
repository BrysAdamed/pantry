import { inject, Pipe, PipeTransform } from '@angular/core';
import { UiLanguage } from '../../core/i18n/ui-language';
import { Quantity, QuantityFormatter } from './quantity-formatter';

/** `{{ line.quantity | quantity: language() }}`; the language argument re-renders on a language switch. */
@Pipe({ name: 'quantity' })
export class QuantityPipe implements PipeTransform {
  private readonly formatter = inject(QuantityFormatter);

  transform(quantity: Quantity | readonly Quantity[] | null | undefined, language: UiLanguage): string {
    return quantity ? this.formatter.format(quantity, language) : '';
  }
}
