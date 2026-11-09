import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { COOLDOWN_DAYS, COOLDOWN_URL, CooldownSetting as Setting, HouseholdApi } from '../household-api';

/** The household cooldown: days before a recipe is no longer *too soon*, saved when the field is left. */
@Component({
  selector: 'app-cooldown-setting',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-3' },
  templateUrl: './cooldown-setting.html',
})
export class CooldownSetting {
  private readonly api = inject(HouseholdApi);
  private writes: Promise<unknown> = Promise.resolve();

  protected readonly limits = COOLDOWN_DAYS;
  protected readonly resource = httpResource<Setting>(() => COOLDOWN_URL);
  protected readonly invalid = signal(false);
  protected readonly saveFailed = signal(false);

  protected save(input: HTMLInputElement): void {
    const saved = this.resource.value()?.cooldownDays;
    const days = Number(input.value);
    if (input.value.trim() === '' || !Number.isInteger(days) || days < COOLDOWN_DAYS.min || days > COOLDOWN_DAYS.max) {
      this.invalid.set(true);
      return;
    }
    this.invalid.set(false);
    if (days === saved) {
      return;
    }
    this.resource.set({ cooldownDays: days });
    this.writes = this.writes
      .then(() => this.api.setCooldown(days))
      .then(() => this.saveFailed.set(false))
      .catch(() => {
        this.saveFailed.set(true);
        this.resource.reload();
      });
  }
}
