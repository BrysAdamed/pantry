import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CurrentUser, HANDLES_UNAUTHORIZED, IdentityApi } from './identity-api';

export const SIGN_IN_PATH = 'sign-in';

/** The signed-in user of this browser, if any. */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(IdentityApi);
  private readonly current = signal<CurrentUser | null>(null);

  readonly user = this.current.asReadonly();

  /** The signed-in user, asking the API when none is known yet. `null` when signed out; throws when the API cannot be reached. */
  async resolve(): Promise<CurrentUser | null> {
    const known = this.current();
    if (known) {
      return known;
    }
    try {
      const user = await this.api.me();
      this.current.set(user);
      return user;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized) {
        return null;
      }
      throw error;
    }
  }

  signedIn(user: CurrentUser): void {
    this.current.set(user);
  }

  /** The API said the session is gone (expired or signed out elsewhere). */
  ended(): void {
    this.current.set(null);
  }

  async signOut(): Promise<void> {
    await this.api.signOut();
    this.current.set(null);
  }
}

/** Where to go after sign-in: only paths inside the app, never back to sign-in itself. */
export function safeReturnUrl(url: string | null | undefined): string {
  if (!url || !url.startsWith('/') || url.startsWith('//') || url.startsWith('/\\') || url.startsWith(`/${SIGN_IN_PATH}`)) {
    return '/';
  }
  return url;
}

function signInUrl(router: Router, returnUrl: string, reason?: 'expired') {
  const target = safeReturnUrl(returnUrl);
  return router.createUrlTree(['/', SIGN_IN_PATH], {
    queryParams: { returnUrl: target === '/' ? undefined : target, reason },
  });
}

/** Lets only signed-in users into the app. Fails closed: if the API cannot be reached, the user lands on sign-in. */
export const requireSignedIn: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const session = inject(Session);
  try {
    return (await session.resolve()) ? true : signInUrl(router, state.url);
  } catch {
    return signInUrl(router, state.url);
  }
};

/** Sends a user who is already signed in from the sign-in screen to where they were going. */
export const requireSignedOut: CanActivateFn = async (route) => {
  const router = inject(Router);
  const session = inject(Session);
  try {
    return (await session.resolve()) ? router.parseUrl(safeReturnUrl(route.queryParamMap.get('returnUrl'))) : true;
  } catch {
    return true;
  }
};

/** A 401 from the API means the session is gone: forget the user and send them to sign-in, back to this screen afterwards. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);
  const session = inject(Session);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === HttpStatusCode.Unauthorized &&
        !request.context.get(HANDLES_UNAUTHORIZED)
      ) {
        session.ended();
        void router.navigateByUrl(signInUrl(router, router.url, 'expired'));
      }
      return throwError(() => error);
    }),
  );
};
