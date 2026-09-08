import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { CourtPhoto } from '../data-access/court.models';

@Component({
  selector: 'app-court-gallery',
  templateUrl: './court-gallery.html',
  styleUrl: './court-gallery.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtGallery {
  readonly photos = input.required<CourtPhoto[]>();
  readonly venueName = input.required<string>();
  // Retain the intentional geometry when no usable image can be loaded.
  private readonly failed = signal<ReadonlySet<string>>(new Set());
  protected readonly visiblePhotos = computed(() =>
    this.photos().filter((photo) => {
      if (this.failed().has(photo.imageUrl)) return false;
      try {
        const url = new URL(photo.imageUrl);
        return ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password;
      } catch {
        return false;
      }
    }),
  );

  protected imageFailed(url: string) {
    this.failed.update((failed) => new Set([...failed, url]));
  }
}
