import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';
import { TRANSLATIONS } from './translations';

describe('I18nService', () => {
  let service: I18nService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(I18nService);
    service.language.set('en');
  });

  afterEach(() => localStorage.clear());

  it('mirrors the document when Arabic is selected', () => {
    service.language.set('ar');
    TestBed.flushEffects();

    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(document.documentElement.getAttribute('lang')).toBe('ar');
  });

  it('restores left-to-right when English is selected', () => {
    service.language.set('ar');
    TestBed.flushEffects();
    service.language.set('en');
    TestBed.flushEffects();

    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
  });

  it('remembers the chosen language across sessions', () => {
    service.language.set('ar');
    TestBed.flushEffects();

    expect(localStorage.getItem('netwatch.language')).toBe('ar');
  });

  describe('formatDuration', () => {
    it('shows seconds alone under a minute', () => {
      expect(service.formatDuration(45)).toBe('45s');
    });

    it('shows minutes and seconds under an hour', () => {
      expect(service.formatDuration(125)).toBe('2m 5s');
    });

    it('shows hours and minutes under a day', () => {
      expect(service.formatDuration(3_600 + 14 * 60)).toBe('1h 14m');
    });

    it('shows days and hours beyond a day', () => {
      expect(service.formatDuration(2 * 86_400 + 3 * 3_600)).toBe('2d 3h');
    });

    it('never renders a negative duration', () => {
      expect(service.formatDuration(-10)).toBe('0s');
    });

    it('uses Arabic unit labels when the language is Arabic', () => {
      service.language.set('ar');

      expect(service.formatDuration(125)).toBe('2د 5ث');
    });
  });

  describe('formatRelative', () => {
    it('treats a timestamp with no zone suffix as UTC', () => {
      // ASP.NET Core serialises DateTime without a suffix. Read as local time this
      // would be wrong by the UTC offset - three hours in Amman - so "a moment ago"
      // would render as "3h ago".
      const oneMinuteAgo = new Date(Date.now() - 60_000).toISOString().replace('Z', '');

      expect(service.formatRelative(oneMinuteAgo)).toBe('1m 0s ago');
    });

    it('accepts a timestamp that already carries a zone suffix', () => {
      const oneMinuteAgo = new Date(Date.now() - 60_000).toISOString();

      expect(service.formatRelative(oneMinuteAgo)).toBe('1m 0s ago');
    });

    it('reports never for a probe that has not run', () => {
      expect(service.formatRelative(null)).toBe('Never');
    });
  });

  it('has an Arabic string for every English key', () => {
    // Guards the one failure mode a dictionary-based approach has: an English
    // string added without its Arabic counterpart, which would render as blank.
    const english = Object.keys(TRANSLATIONS.en).sort();
    const arabic = Object.keys(TRANSLATIONS.ar).sort();

    expect(arabic).toEqual(english);
  });
});
