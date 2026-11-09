import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { Icon } from '../../shared/icon/icon';
import { newRecipePath } from '../recipes-paths';
import { RecipeLibraryScreen } from './recipe-library-screen';

/** The pane beside the library from `md` up while no recipe is open: a hint, or the empty library's welcome. */
@Component({
  selector: 'app-no-recipe-open',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      @if (screen.libraryEmpty() && !screen.archived()) {
        <app-empty-state
          class="mx-auto mt-10 max-w-115"
          illustration="recipes"
          [heading]="t('emptyState.recipes.title')"
          [description]="t('emptyState.recipes.description')"
          [agentHint]="t('emptyState.recipes.agentHint')"
        >
          <a hlmBtn [routerLink]="newPath">
            <app-icon name="plus" class="size-4" />
            {{ t('recipes.library.new') }}
          </a>
        </app-empty-state>
      } @else if (screen.libraryKnown()) {
        <p class="px-3 py-7 text-center text-sm text-muted-foreground">{{ t('recipes.library.nothingSelected') }}</p>
      }
    </ng-container>
  `,
})
export class NoRecipeOpen {
  protected readonly screen = inject(RecipeLibraryScreen);
  protected readonly newPath = newRecipePath();
}
