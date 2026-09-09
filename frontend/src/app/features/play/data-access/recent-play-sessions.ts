import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { normalizeCode, validCode } from './play.models';

interface RecentCode {
  code: string;
  accessedAt: number;
}

@Injectable({ providedIn: 'root' })
export class RecentPlaySessions {
  private readonly document = inject(DOCUMENT);
  private readonly key = 'kitchain.play.recent.v1';

  list(): RecentCode[] {
    try {
      const stored: unknown = JSON.parse(
        this.document.defaultView?.localStorage.getItem(this.key) ?? '[]',
      );
      if (!Array.isArray(stored)) return [];
      const seen = new Set<string>();
      return stored
        .filter(
          (entry): entry is RecentCode =>
            !!entry &&
            typeof entry.code === 'string' &&
            validCode(entry.code) &&
            typeof entry.accessedAt === 'number' &&
            Number.isFinite(entry.accessedAt),
        )
        .sort((a, b) => b.accessedAt - a.accessedAt)
        .filter(({ code }) => !seen.has(code) && !!seen.add(code))
        .slice(0, 8)
        .map(({ code, accessedAt }) => ({ code, accessedAt }));
    } catch {
      return [];
    }
  }

  remember(raw: string) {
    const code = normalizeCode(raw);
    if (validCode(code))
      this.save(
        [{ code, accessedAt: Date.now() }, ...this.list().filter((e) => e.code !== code)].slice(
          0,
          8,
        ),
      );
  }

  remove(code: string) {
    this.save(this.list().filter((entry) => entry.code !== code));
  }

  private save(entries: RecentCode[]) {
    try {
      this.document.defaultView?.localStorage.setItem(this.key, JSON.stringify(entries));
    } catch {
      // Session use remains available when browser storage is disabled or full.
    }
  }
}
