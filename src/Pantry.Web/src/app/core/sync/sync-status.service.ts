import { Injectable, signal } from '@angular/core';

export const SYNC_STATES = ['online', 'offline', 'pending'] as const;

export type SyncState = (typeof SYNC_STATES)[number];

/** Whether local changes reached the server. Always online until E9 adds offline writes. */
@Injectable({ providedIn: 'root' })
export class SyncStatusService {
  readonly state = signal<SyncState>('online').asReadonly();
}
