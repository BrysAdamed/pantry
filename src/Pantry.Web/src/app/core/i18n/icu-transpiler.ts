import { Injectable } from '@angular/core';
import { DefaultTranspiler, TranspileParams } from '@jsverse/transloco';
import { formatIcuMessage, IcuMessage, isIcuMessage, parseIcuMessage } from './icu-message';
import { DEFAULT_UI_LANGUAGE } from './ui-language';

/**
 * Renders labels written as ICU messages (`{count, plural, one {…} few {…} other {…}}`) with the
 * plural rules of the active UI language. Other labels keep Transloco's `{{param}}` interpolation.
 * Parameters are values of the parsed message, so text people type is never parsed.
 */
@Injectable()
export class IcuTranspiler extends DefaultTranspiler {
  private locale: string = DEFAULT_UI_LANGUAGE;
  private readonly messages = new Map<string, IcuMessage>();

  override transpile(params: TranspileParams): unknown {
    const { value } = params;
    if (typeof value === 'string' && isIcuMessage(value)) {
      return formatIcuMessage(this.parsed(value), params.params ?? {}, this.locale);
    }
    return super.transpile(params);
  }

  onLangChanged(lang: string): void {
    this.locale = lang;
  }

  private parsed(label: string): IcuMessage {
    let message = this.messages.get(label);
    if (!message) {
      message = parseIcuMessage(label);
      this.messages.set(label, message);
    }
    return message;
  }
}
