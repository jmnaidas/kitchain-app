import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

export const playerNameKey = (name: string) => name.trim().replace(/\s+/g, ' ').toUpperCase();
export const validName = (value: unknown, limit = 80): value is string =>
  typeof value === 'string' && value.trim().length > 0 && value.trim().length <= limit;
export const record = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);
export const validId = (value: unknown): value is string =>
  typeof value === 'string' && /^[a-zA-Z0-9-]{1,80}$/.test(value);
export const validTime = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value) && value >= 0;

export function uniqueNames(names: readonly string[]) {
  const seen = new Set<string>();
  return names.map((name) => name.trim()).filter((name) => {
    const key = playerNameKey(name);
    if (!validName(name) || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

export function savedName(name: string, existing: readonly { id: string; name: string }[], id?: string) {
  if (!validName(name, 60)) throw new Error('Use a name of 1–60 characters.');
  if (existing.some((entry) => entry.id !== id && playerNameKey(entry.name) === playerNameKey(name)))
    throw new Error('That name is already saved. Choose a different name.');
  return name.trim();
}

/** Storage mechanics only. Repositories own schemas and UI never accesses localStorage. */
@Injectable({ providedIn: 'root' })
export class PlayHostStorage {
  private readonly document = inject(DOCUMENT);
  readonly unavailable = signal(false);

  read<T>(key: string, parse: (value: unknown) => T | null, limit: number): T[] {
    try {
      const raw: unknown = JSON.parse(this.document.defaultView?.localStorage.getItem(key) ?? '[]');
      if (!Array.isArray(raw)) return [];
      return raw.slice(0, 1000).map(parse).filter((entry): entry is T => entry !== null).slice(0, limit);
    } catch {
      return [];
    }
  }
  write(key: string, value: unknown) {
    try {
      const storage = this.document.defaultView?.localStorage;
      if (!storage) throw new Error('Storage unavailable');
      storage.setItem(key, JSON.stringify(value));
      this.unavailable.set(false);
    } catch {
      this.unavailable.set(true);
      throw new Error('Browser storage is unavailable or full. Your session still works, but this change could not be saved.');
    }
  }
}
