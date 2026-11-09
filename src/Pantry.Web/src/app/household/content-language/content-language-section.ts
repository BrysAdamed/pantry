import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, linkedSignal, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { CONTENT_LANGUAGE_URL, CONTENT_LANGUAGES, ContentLanguage, ContentLanguageSetting, HouseholdApi } from '../household-api';

/** The household's content language, picked from the supported list and saved at once. */
@Component({
  selector: 'app-content-language-section',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './content-language-section.html',
})
export class ContentLanguageSection {
  private readonly api = inject(HouseholdApi);
  private writes: Promise<unknown> = Promise.resolve();

  protected readonly languages = CONTENT_LANGUAGES;

  protected readonly resource = httpResource<ContentLanguageSetting>(() => CONTENT_LANGUAGE_URL);
  protected readonly selected = linkedSignal<ContentLanguage | null>(() => this.resource.value()?.contentLanguage ?? null);
  protected readonly saveFailed = signal(false);

  protected select(language: ContentLanguage): void {
    if (language === this.selected()) {
      return;
    }
    this.selected.set(language);
    this.writes = this.writes
      .then(() => this.api.setContentLanguage(language))
      .then(() => this.saveFailed.set(false))
      .catch(() => {
        this.saveFailed.set(true);
        this.resource.reload();
      });
  }
}
