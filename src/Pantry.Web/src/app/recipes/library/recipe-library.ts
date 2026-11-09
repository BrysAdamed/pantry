import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { filter, map } from 'rxjs';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { Icon } from '../../shared/icon/icon';
import { RECIPE_SORTS, RecipeSort } from '../recipes-api';
import { newRecipePath } from '../recipes-paths';
import { RecipeCard } from './recipe-card';
import { RecipeLibraryScreen } from './recipe-library-screen';

/**
 * The *Przepisy* destination: the library with search, tag chips, *without never*, the archive and the sort, and the
 * open recipe beside it from `md` up; on phones the recipe replaces the list.
 */
@Component({
  selector: 'app-recipe-library',
  imports: [RouterOutlet, RouterLink, TranslocoDirective, HlmButton, Icon, EmptyState, RecipeCard],
  providers: [RecipeLibraryScreen],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './recipe-library.html',
})
export class RecipeLibrary {
  private readonly router = inject(Router);

  protected readonly screen = inject(RecipeLibraryScreen);
  protected readonly sorts = RECIPE_SORTS;
  protected readonly newPath = newRecipePath();

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** The open recipe's id, or null when only the list shows. */
  protected readonly open = computed(() => this.url().split(/[?#]/)[0].split('/')[2] ?? null);

  protected setSort(value: string): void {
    if (RECIPE_SORTS.includes(value as RecipeSort)) {
      this.screen.sort.set(value as RecipeSort);
    }
  }
}
