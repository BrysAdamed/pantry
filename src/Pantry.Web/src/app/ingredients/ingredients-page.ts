import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { filter, map } from 'rxjs';
import { INGREDIENT_CATEGORIES, IngredientCategory } from '../catalog/catalog-api';
import { EmptyState } from '../shared/empty-state/empty-state';
import { Icon } from '../shared/icon/icon';
import { IngredientRow } from './ingredient-row';
import { IngredientsScreen } from './ingredients-screen';
import { NEW_INGREDIENT_PATH } from './ingredients-paths';
import { TidyUpCard } from './tidy-up-card';

/**
 * The *Składniki* destination: the catalog grouped by category with search and filters, and the open ingredient
 * beside it from `md` up; on phones the ingredient replaces the list.
 */
@Component({
  selector: 'app-ingredients-page',
  imports: [RouterOutlet, RouterLink, TranslocoDirective, HlmButton, Icon, EmptyState, IngredientRow, TidyUpCard],
  providers: [IngredientsScreen],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ingredients-page.html',
})
export class IngredientsPage {
  private readonly router = inject(Router);

  protected readonly screen = inject(IngredientsScreen);
  protected readonly categories = INGREDIENT_CATEGORIES;
  protected readonly newPath = NEW_INGREDIENT_PATH;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** The open ingredient's id, `new`, or null when only the list shows. */
  protected readonly open = computed(() => this.url().split(/[?#]/)[0].split('/')[2] ?? null);

  protected setCategory(value: string): void {
    this.screen.category.set(INGREDIENT_CATEGORIES.includes(value as IngredientCategory) ? (value as IngredientCategory) : null);
  }
}
