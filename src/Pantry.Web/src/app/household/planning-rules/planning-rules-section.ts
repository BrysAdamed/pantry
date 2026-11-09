import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { Icon } from '../../shared/icon/icon';
import { Switch } from '../../shared/switch/switch';
import { HouseholdApi, PLANNING_RULE_TEXT_MAX_LENGTH, PLANNING_RULES_URL, PlanningRule } from '../household-api';

/** Planning rules in order: edit in place, arrows to reorder, a switch to keep a rule for later, remove after a confirmation. */
@Component({
  selector: 'app-planning-rules-section',
  imports: [TranslocoDirective, HlmButton, Icon, Switch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './planning-rules-section.html',
})
export class PlanningRulesSection {
  private readonly api = inject(HouseholdApi);
  private writes: Promise<unknown> = Promise.resolve();

  protected readonly textMaxLength = PLANNING_RULE_TEXT_MAX_LENGTH;

  protected readonly resource = httpResource<PlanningRule[]>(() => PLANNING_RULES_URL);
  protected readonly rules = linkedSignal<PlanningRule[]>(() => this.resource.value() ?? []);
  protected readonly enabledCount = computed(() => this.rules().filter((rule) => rule.isEnabled).length);

  protected readonly newText = signal('');
  protected readonly canAdd = computed(() => isValidText(this.newText()));
  protected readonly adding = signal(false);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly saveFailed = signal(false);

  protected edit(rule: PlanningRule, input: HTMLTextAreaElement): void {
    const text = input.value.trim();
    if (!isValidText(text) || text === rule.text) {
      input.value = rule.text;
      return;
    }
    this.replace({ ...rule, text });
    this.write(() => this.api.editPlanningRule(rule.id, text));
  }

  protected move(id: string, direction: 1 | -1): void {
    const rules = [...this.rules()];
    const index = rules.findIndex((rule) => rule.id === id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= rules.length) {
      return;
    }
    [rules[index], rules[target]] = [rules[target], rules[index]];
    const ordered = rules.map((rule, position) => ({ ...rule, position }));
    this.rules.set(ordered);
    this.write(() => this.api.reorderPlanningRules(ordered.map((rule) => rule.id)));
  }

  protected setEnabled(rule: PlanningRule, isEnabled: boolean): void {
    this.replace({ ...rule, isEnabled });
    this.write(() => this.api.setPlanningRuleEnabled(rule.id, isEnabled));
  }

  protected delete(rule: PlanningRule): void {
    this.deletingId.set(null);
    this.rules.update((rules) => rules.filter((r) => r.id !== rule.id).map((r, position) => ({ ...r, position })));
    this.write(() => this.api.deletePlanningRule(rule.id));
  }

  protected async add(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canAdd() || this.adding()) {
      return;
    }
    this.adding.set(true);
    try {
      await this.writes;
      const rule = await this.api.addPlanningRule(this.newText().trim());
      this.rules.update((rules) => [...rules, rule]);
      this.newText.set('');
      this.saveFailed.set(false);
    } catch {
      this.failAndReload();
    } finally {
      this.adding.set(false);
    }
  }

  private replace(rule: PlanningRule): void {
    this.rules.update((rules) => rules.map((r) => (r.id === rule.id ? rule : r)));
  }

  /** Shows changes at once and sends them one after another, so the server sees them in the order they were made. */
  private write(send: () => Promise<unknown>): void {
    this.writes = this.writes
      .then(send)
      .then(() => this.saveFailed.set(false))
      .catch(() => this.failAndReload());
  }

  private failAndReload(): void {
    this.saveFailed.set(true);
    this.resource.reload();
  }
}

function isValidText(value: string): boolean {
  const text = value.trim();
  return text.length > 0 && text.length <= PLANNING_RULE_TEXT_MAX_LENGTH;
}
