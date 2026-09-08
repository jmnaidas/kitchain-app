import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Amenity, IndoorOutdoor, bookingLabels, priceUnitLabels } from './court.models';

export interface CourtSubmissionInput {
  name: string;
  city: string;
  address: string;
  numberOfCourts: number;
  indoorOutdoor: IndoorOutdoor;
  bookingMethod: keyof typeof bookingLabels;
  region: string | null;
  surface: string | null;
  openingHours: string | null;
  startingPrice: number | null;
  currencyCode: string | null;
  priceUnit: keyof typeof priceUnitLabels | null;
  phone: string | null;
  websiteUrl: string | null;
  socialUrl: string | null;
  bookingUrl: string | null;
  amenities: Amenity[];
  photos: { imageUrl: string; altText: string | null }[];
}
export interface CourtSubmissionReceipt {
  id: string;
  status: 'Pending';
  submittedAt: string;
}
@Injectable({ providedIn: 'root' })
export class CourtSubmissionsApi {
  private readonly http = inject(HttpClient);
  submit(input: CourtSubmissionInput) {
    return this.http.post<CourtSubmissionReceipt>('/api/court-submissions', input);
  }
}
