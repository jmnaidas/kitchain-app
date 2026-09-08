import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  Amenity,
  amenityOptions,
  bookingLabels,
  IndoorOutdoor,
  indoorOutdoorTypes,
  priceUnitLabels,
} from '../data-access/court.models';
import { CourtSubmissionsApi } from '../data-access/court-submissions-api';

interface Field {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
  maxLength?: number;
  min?: number;
  max?: number;
  step?: string;
  options?: { value: string; label: string }[];
  hint?: string;
}
const options = (labels: Record<string, string>) =>
  Object.entries(labels).map(([value, label]) => ({ value, label }));
@Component({
  selector: 'app-court-add',
  imports: [RouterLink],
  templateUrl: './court-add.html',
  styleUrl: './court-add.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtAdd {
  private readonly api = inject(CourtSubmissionsApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = signal<'editing' | 'submitting' | 'success' | 'error'>('editing');
  protected readonly errors = signal<Record<string, string>>({});
  protected readonly amenities = amenityOptions;
  protected readonly sections: { title: string; note?: string; fields: Field[] }[] = [
    {
      title: '01 / Venue',
      fields: [
        { name: 'name', label: 'Venue name', required: true, maxLength: 200 },
        { name: 'city', label: 'City', required: true, maxLength: 100 },
        { name: 'address', label: 'Street address', required: true, maxLength: 500 },
        { name: 'region', label: 'Region', maxLength: 100 },
        {
          name: 'numberOfCourts',
          label: 'Number of courts',
          type: 'number',
          required: true,
          min: 1,
          max: 2147483647,
          step: '1',
        },
        {
          name: 'indoorOutdoor',
          label: 'Setting',
          required: true,
          options: indoorOutdoorTypes.map((value) => ({ value, label: value })),
        },
      ],
    },
    {
      title: '02 / Playing details',
      fields: [
        { name: 'surface', label: 'Playing surface', maxLength: 100 },
        {
          name: 'openingHours',
          label: 'Opening hours',
          type: 'textarea',
          maxLength: 1000,
          hint: 'Share the days and hours you know. Leave unknown details blank.',
        },
      ],
    },
    {
      title: '03 / Pricing & booking',
      fields: [
        {
          name: 'startingPrice',
          label: 'Approximate starting price',
          type: 'number',
          min: 0,
          max: 9999999999.99,
          step: '0.01',
          hint: 'Leave blank if unknown. Enter 0 only if free.',
        },
        {
          name: 'currencyCode',
          label: 'Currency code',
          maxLength: 3,
          hint: 'Required with a price, for example PHP.',
        },
        { name: 'priceUnit', label: 'Price unit', options: options(priceUnitLabels) },
        {
          name: 'bookingMethod',
          label: 'Booking method',
          required: true,
          options: options(bookingLabels),
        },
        {
          name: 'bookingUrl',
          label: 'Booking URL',
          type: 'url',
          maxLength: 2048,
          hint: 'Required for an external platform or Google Form.',
        },
      ],
    },
    {
      title: '04 / Contact',
      note: 'Phone booking needs a number; messaging needs a phone or social link. Website booking needs a website or booking URL. “Contact venue” needs at least one contact or link. Walk-in needs no extra contact.',
      fields: [
        { name: 'phone', label: 'Venue phone number', type: 'tel', maxLength: 50 },
        { name: 'websiteUrl', label: 'Website URL', type: 'url', maxLength: 2048 },
        { name: 'socialUrl', label: 'Social page URL', type: 'url', maxLength: 2048 },
      ],
    },
    {
      title: '05 / Photos',
      note: 'Venue photography will be required and verified during review. You can share an existing public image URL now, or leave this blank. File uploads are coming later.',
      fields: [
        { name: 'imageUrl', label: 'Photo URL', type: 'url', maxLength: 2048 },
        {
          name: 'altText',
          label: 'Photo description',
          maxLength: 500,
          hint: 'Briefly describe what the photo shows.',
        },
      ],
    },
  ];

  protected validateField(event: Event) {
    const control = event.target as HTMLInputElement;
    control.setCustomValidity('');
    if (control.required && !control.value.trim())
      control.setCustomValidity('This field is required.');
    if (control.type === 'url' && control.value.trim()) {
      try {
        const url = new URL(control.value.trim());
        if (
          !['http:', 'https:'].includes(url.protocol) ||
          !url.hostname ||
          url.username ||
          url.password ||
          url.href.length > 2048
        )
          throw new Error();
      } catch {
        control.setCustomValidity('Use a full HTTP(S) URL without embedded credentials.');
      }
    }
    this.errors.update((errors) => ({ ...errors, [control.name]: control.validationMessage }));
  }

  protected submit(event: Event) {
    event.preventDefault();
    if (this.state() === 'submitting' || this.state() === 'success') return;
    const form = event.target as HTMLFormElement;
    const controls = Array.from(
      form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>(
        'input:not([type=checkbox]), select, textarea',
      ),
    );
    this.errors.set({});
    for (const control of controls) this.validateField({ target: control } as unknown as Event);
    const values = new FormData(form);
    const text = (name: string) => String(values.get(name) ?? '').trim();
    const optional = (name: string) => text(name) || null;
    const errors = { ...this.errors() };
    const method = text('bookingMethod');
    if (['ExternalPlatform', 'GoogleForm'].includes(method) && !text('bookingUrl'))
      errors['bookingUrl'] = 'Supply a booking URL.';
    if (method === 'Website' && !text('websiteUrl') && !text('bookingUrl'))
      errors['websiteUrl'] = 'Supply a website or booking URL.';
    if (method === 'Phone' && !text('phone')) errors['phone'] = 'Supply a phone number.';
    if (method === 'Message' && !text('socialUrl') && !text('phone'))
      errors['socialUrl'] = 'Supply a social link or phone number.';
    if (
      method === 'Other' &&
      !['phone', 'websiteUrl', 'socialUrl', 'bookingUrl'].some((name) => text(name))
    )
      errors['phone'] = 'Supply a contact or booking link.';
    if (text('startingPrice') && !/^[a-zA-Z]{3}$/.test(text('currencyCode')))
      errors['currencyCode'] = 'Use a three-letter currency code with your price.';
    if (text('currencyCode') && !/^[a-zA-Z]{3}$/.test(text('currencyCode')))
      errors['currencyCode'] = 'Use a three-letter currency code.';
    if (text('priceUnit') && !text('startingPrice'))
      errors['startingPrice'] = 'Supply a price or clear the price unit.';
    if (text('altText') && !text('imageUrl'))
      errors['imageUrl'] = 'Supply a photo URL or clear the description.';
    this.errors.set(errors);
    if (Object.values(errors).some(Boolean)) {
      controls.find((control) => errors[control.name])?.focus();
      return;
    }
    this.state.set('submitting');
    this.api
      .submit({
        name: text('name'),
        city: text('city'),
        address: text('address'),
        numberOfCourts: Number(text('numberOfCourts')),
        indoorOutdoor: text('indoorOutdoor') as IndoorOutdoor,
        bookingMethod: method as keyof typeof bookingLabels,
        region: optional('region'),
        surface: optional('surface'),
        openingHours: optional('openingHours'),
        startingPrice: text('startingPrice') ? Number(text('startingPrice')) : null,
        currencyCode: optional('currencyCode')?.toUpperCase() ?? null,
        priceUnit: optional('priceUnit') as keyof typeof priceUnitLabels | null,
        phone: optional('phone'),
        websiteUrl: optional('websiteUrl'),
        socialUrl: optional('socialUrl'),
        bookingUrl: optional('bookingUrl'),
        amenities: values.getAll('amenities') as Amenity[],
        photos: text('imageUrl')
          ? [{ imageUrl: text('imageUrl'), altText: optional('altText') }]
          : [],
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.state.set('success'),
        error: (response: HttpErrorResponse) => {
          const validation: unknown = response.status === 400 ? response.error?.errors : null;
          if (validation && typeof validation === 'object') {
            const fieldErrors: Record<string, string> = {};
            for (const [key, messages] of Object.entries(validation)) {
              const field = controls.find(
                (control) => control.name.toLowerCase() === key.toLowerCase(),
              );
              if (field && Array.isArray(messages) && typeof messages[0] === 'string') {
                fieldErrors[field.name] = messages[0];
              }
            }
            this.errors.set(fieldErrors);
          }
          this.state.set('error');
        },
      });
  }
}
