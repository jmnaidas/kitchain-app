import { afterNextRender, DestroyRef, Directive, ElementRef, inject } from '@angular/core';
import { animate } from 'motion/mini';

/** Content remains visible when motion is disabled or unsupported. */
@Directive({ selector: '[appEntrance]' })
export class Entrance {
  constructor() {
    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      if (typeof element.animate !== 'function') return;
      const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
      if (preference.matches) return;
      const animation = animate(
        element,
        { opacity: [0, 1], transform: ['translateY(8px)', 'translateY(0)'] },
        { duration: 0.35, ease: 'easeOut' },
      );
      const finishOnPreferenceChange = () => {
        if (preference.matches) animation.complete();
      };
      preference.addEventListener('change', finishOnPreferenceChange);
      destroyRef.onDestroy(() => {
        preference.removeEventListener('change', finishOnPreferenceChange);
        animation.cancel();
      });
    });
  }
}
