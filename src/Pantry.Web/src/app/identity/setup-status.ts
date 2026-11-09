import { inject, Injectable } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { IdentityApi } from './identity-api';

export const FIRST_RUN_PATH = 'first-run';

/** Whether the deployment still needs its first run. Once done, it stays done, so the answer is cached. */
@Injectable({ providedIn: 'root' })
export class SetupStatusService {
  private readonly api = inject(IdentityApi);
  private done = false;

  async firstRunNeeded(): Promise<boolean> {
    if (this.done) {
      return false;
    }
    const { firstRunNeeded } = await this.api.setupStatus();
    this.done = !firstRunNeeded;
    return firstRunNeeded;
  }

  markDone(): void {
    this.done = true;
  }
}

/** Sends every app route to the first-run screen while no user exists. If the API cannot be reached, the app opens as usual. */
export const requireSetupDone: CanActivateFn = async () => {
  const router = inject(Router);
  try {
    return (await inject(SetupStatusService).firstRunNeeded()) ? router.parseUrl(`/${FIRST_RUN_PATH}`) : true;
  } catch {
    return true;
  }
};

/** Keeps the first-run screen reachable only while it is needed. */
export const requireFirstRunNeeded: CanActivateFn = async () => {
  const router = inject(Router);
  try {
    return (await inject(SetupStatusService).firstRunNeeded()) ? true : router.parseUrl('/');
  } catch {
    return true;
  }
};
