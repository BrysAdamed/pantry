import { formatNumber } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ModuleReferenceCount, UnitConversion } from '../catalog/catalog-api';
import { Unit } from '../catalog/units';
import { UiLanguageService } from '../core/i18n/ui-language.service';

/** Modules with their own labels; references of any other module show the module code. */
export const KNOWN_MODULES = ['recipes', 'shopping', 'household'];
const AMOUNT_FORMAT = '1.0-4';

/** Texts about ingredients shared by the detail and the merge dialog. Reading them in a `computed` follows the UI language. */
@Injectable({ providedIn: 'root' })
export class IngredientTexts {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(UiLanguageService).current;

  unit(unit: Unit): string {
    return this.transloco.translate(`catalog.unit.${unit}`);
  }

  /** `1 clove = 5 g`. */
  conversion(conversion: UnitConversion, baseUnit: Unit): string {
    const unit = this.transloco.translate(`catalog.unitAmount.${conversion.unit}`, { count: 1 });
    const amount = formatNumber(conversion.amount, this.language(), AMOUNT_FORMAT);
    return `1 ${unit} = ${amount} ${this.unit(baseUnit)}`;
  }

  conversions(conversions: readonly UnitConversion[], baseUnit: Unit): string {
    return conversions.map((c) => this.conversion(c, baseUnit)).join(', ');
  }

  /** `3 recipes, 1 shopping list`. */
  usage(references: readonly ModuleReferenceCount[]): string {
    return references
      .map((r) =>
        KNOWN_MODULES.includes(r.module)
          ? this.transloco.translate(`ingredients.detail.usage.count.${r.module}`, {
              count: r.count,
            })
          : this.transloco.translate('ingredients.detail.usage.count.other', {
              module: r.module,
              count: r.count,
            }),
      )
      .join(', ');
  }
}
