import { formatNumber } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { UiLanguage } from '../../core/i18n/ui-language';
import { Unit } from '../units';

export interface Quantity {
  readonly amount: number;
  readonly unit: Unit;
}

const LARGER_UNITS: Partial<Record<Unit, Unit>> = { g: 'kg', ml: 'l' };
const LARGER_UNIT_FROM = 1000;
const NUMBER_FORMAT = '1.0-2';
const PARTS_SEPARATOR = ' + ';
const UNIT_SPACE = ' ';

/**
 * Writes quantities the way people read them in the UI language: `1500 g` as `1,5 kg`, unit names
 * in the right plural form (`2 ząbki`, `5 ząbków`, `2,5 ząbka`), and amounts that could not be
 * added up as `3 szt. + 300 g`.
 */
@Injectable({ providedIn: 'root' })
export class QuantityFormatter {
  private readonly transloco = inject(TranslocoService);

  format(quantity: Quantity | readonly Quantity[], language: UiLanguage): string {
    const parts = isQuantityList(quantity) ? quantity : [quantity];
    return parts.map((part) => this.formatPart(part, language)).join(PARTS_SEPARATOR);
  }

  private formatPart(quantity: Quantity, language: UiLanguage): string {
    const { amount, unit } = inLargerUnit(quantity);
    const label = this.transloco.translate(`catalog.unitAmount.${unit}`, { count: amount }, language);
    return `${formatNumber(amount, language, NUMBER_FORMAT)}${UNIT_SPACE}${label}`;
  }
}

function isQuantityList(quantity: Quantity | readonly Quantity[]): quantity is readonly Quantity[] {
  return Array.isArray(quantity);
}

function inLargerUnit(quantity: Quantity): Quantity {
  const amount = roundToDisplay(quantity.amount);
  const larger = LARGER_UNITS[quantity.unit];
  if (larger && amount >= LARGER_UNIT_FROM) {
    return { amount: roundToDisplay(amount / LARGER_UNIT_FROM), unit: larger };
  }
  return { amount, unit: quantity.unit };
}

function roundToDisplay(amount: number): number {
  return Math.round((amount + Number.EPSILON) * 100) / 100;
}
