import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService } from './i18n.service';
import { TranslationKey } from './translations';

/**
 * Impure on purpose.
 *
 * A pure pipe caches by input, so switching language would leave every already
 * rendered label in the old one until something else forced a re-render. The cost
 * is a lookup in a plain object per change detection cycle, which is nothing next
 * to a page of network data.
 */
@Pipe({ name: 'translate', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(key: TranslationKey): string {
    return this.i18n.translate(key);
  }
}
