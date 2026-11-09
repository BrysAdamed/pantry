import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { App } from './app';
import { provideTranslocoTesting } from './testing/transloco-testing';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
  });

  async function render(health: { body: object; status?: number }) {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/health')
      .flush(health.body, { status: health.status ?? 200, statusText: 'x' });
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the app name and API status', async () => {
    const page = await render({ body: { status: 'ok', version: 'test' } });

    expect(page.querySelector('h1')?.textContent).toContain('Pantry');
    expect(page.querySelector('[data-testid="api-status"]')?.textContent).toContain('ok (test)');
  });

  it('shows an unhealthy API when health returns 503', async () => {
    const page = await render({ body: { status: 'unhealthy', version: 'test' }, status: 503 });

    expect(page.querySelector('[data-testid="api-status"]')?.textContent).toContain('unhealthy');
  });

  it('switches the UI to Polish', async () => {
    const page = await render({ body: { status: 'ok', version: 'test' } });

    (page.querySelector('[data-testid="language-pl"]') as HTMLButtonElement).click();
    await firstValueFrom(TestBed.inject(TranslocoService).load('pl'));
    TestBed.tick();

    expect(page.querySelector('h1')?.textContent).toContain('Spiżarnia');
    expect(document.documentElement.lang).toBe('pl');
  });
});
