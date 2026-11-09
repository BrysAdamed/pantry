import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { ThemeService } from './theme.service';

export function provideTheme(): EnvironmentProviders {
  return provideAppInitializer(() => inject(ThemeService).init());
}
