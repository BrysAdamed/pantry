import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { EmptyState } from '../shared/empty-state/empty-state';
import { Icon } from '../shared/icon/icon';
import { IngredientsScreen } from './ingredients-screen';
import { NEW_INGREDIENT_PATH } from './ingredients-paths';

/** The detail pane from `md` up while no ingredient is open: a hint, or the empty catalog's welcome. */
@Component({
  selector: 'app-no-ingredient-open',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      @if (screen.catalogEmpty()) {
        <app-empty-state
          class="mx-auto mt-10 max-w-115"
          illustration="ingredients"
          [heading]="t('ingredients.empty.title')"
          [description]="t('ingredients.empty.description')"
          [agentHint]="t('ingredients.empty.agentHint')"
        >
          <a hlmBtn [routerLink]="newPath">
            <app-icon name="plus" class="size-4" />
            {{ t('ingredients.empty.action') }}
          </a>
        </app-empty-state>
      } @else if (screen.total() !== null) {
        <p class="px-3 py-7 text-center text-sm text-muted-foreground">{{ t('ingredients.nothingSelected') }}</p>
      }
    </ng-container>
  `,
})
export class NoIngredientOpen {
  protected readonly screen = inject(IngredientsScreen);
  protected readonly newPath = NEW_INGREDIENT_PATH;
}
