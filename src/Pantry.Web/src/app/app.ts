import { Component, computed } from '@angular/core';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageSwitcher } from './core/i18n/language-switcher';

interface Health {
  status: string;
  version: string;
}

type ApiStatus = 'checking' | 'ok' | 'unhealthy' | 'unreachable';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslocoDirective, LanguageSwitcher],
  templateUrl: './app.html',
})
export class App {
  protected readonly health = httpResource<Health>(() => '/api/health');

  protected readonly apiStatus = computed<ApiStatus>(() => {
    if (this.health.hasValue()) {
      return 'ok';
    }
    const error = this.health.error();
    if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.ServiceUnavailable) {
      return 'unhealthy';
    }
    return error ? 'unreachable' : 'checking';
  });
}
