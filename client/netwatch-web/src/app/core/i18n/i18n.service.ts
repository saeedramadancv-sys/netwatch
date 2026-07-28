import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Language, TRANSLATIONS, TranslationKey } from './translations';

const STORAGE_KEY = 'netwatch.language';

/**
 * Runtime language switching with full right-to-left support.
 *
 * Direction is applied to the document element, not to a container, so it reaches
 * everything the browser lays out — including scrollbars, native form controls and
 * text selection. Styling then relies on CSS logical properties
 * (margin-inline-start rather than margin-left), which means the layout mirrors
 * itself without a second Arabic-only stylesheet.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly document = inject(DOCUMENT);

  readonly language = signal<Language>(readStoredLanguage());

  readonly isRtl = computed(() => this.language() === 'ar');

  constructor() {
    effect(() => {
      const language = this.language();
      const root = this.document.documentElement;

      root.setAttribute('lang', language);
      root.setAttribute('dir', language === 'ar' ? 'rtl' : 'ltr');
      localStorage.setItem(STORAGE_KEY, language);
    });
  }

  translate(key: TranslationKey): string {
    return TRANSLATIONS[this.language()][key];
  }

  toggle(): void {
    this.language.update((current) => (current === 'en' ? 'ar' : 'en'));
  }

  /**
   * Formats a duration in seconds compactly, e.g. "2h 14m".
   *
   * Written by hand rather than pulled from a date library: the output is two units
   * at most, uses the active language's unit labels, and is the only formatting the
   * app needs.
   */
  formatDuration(totalSeconds: number): string {
    const seconds = Math.max(0, Math.floor(totalSeconds));

    const days = Math.floor(seconds / 86_400);
    const hours = Math.floor((seconds % 86_400) / 3_600);
    const minutes = Math.floor((seconds % 3_600) / 60);
    const remaining = seconds % 60;

    const unit = {
      d: this.translate('common.days'),
      h: this.translate('common.hours'),
      m: this.translate('common.minutes'),
      s: this.translate('common.seconds'),
    };

    if (days > 0) {
      return `${days}${unit.d} ${hours}${unit.h}`;
    }
    if (hours > 0) {
      return `${hours}${unit.h} ${minutes}${unit.m}`;
    }
    if (minutes > 0) {
      return `${minutes}${unit.m} ${remaining}${unit.s}`;
    }
    return `${remaining}${unit.s}`;
  }

  /** "3m ago" style relative time, from a UTC timestamp string. */
  formatRelative(utcTimestamp: string | null): string {
    if (!utcTimestamp) {
      return this.translate('probes.never');
    }

    const elapsedSeconds = (Date.now() - Date.parse(ensureUtc(utcTimestamp))) / 1_000;
    return `${this.formatDuration(elapsedSeconds)} ${this.translate('common.ago')}`;
  }

  /** Locale-aware clock time, from a UTC timestamp string. */
  formatTime(utcTimestamp: string): string {
    return new Date(ensureUtc(utcTimestamp)).toLocaleTimeString(this.language() === 'ar' ? 'ar' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  }

  formatDateTime(utcTimestamp: string): string {
    return new Date(ensureUtc(utcTimestamp)).toLocaleString(this.language() === 'ar' ? 'ar' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'short',
    });
  }
}

/**
 * ASP.NET Core serialises DateTime values without a zone suffix. Left alone, the
 * browser would read them as local time and every "5 minutes ago" would be wrong by
 * the UTC offset — three hours in Amman. Appending 'Z' when no offset is present
 * makes the intent explicit.
 */
function ensureUtc(timestamp: string): string {
  return /[Zz]|[+-]\d{2}:\d{2}$/.test(timestamp) ? timestamp : `${timestamp}Z`;
}

function readStoredLanguage(): Language {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'ar' || stored === 'en') {
    return stored;
  }

  // Falls back to the browser's preference, so an Arabic-configured machine opens
  // in Arabic without anyone touching a setting.
  return navigator.language?.startsWith('ar') ? 'ar' : 'en';
}
