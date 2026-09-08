import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CourtDetail, CourtPhoto } from '../data-access/court.models';

export type SubmissionStatus = 'Pending' | 'Approved' | 'Rejected';
export type ModerationDecision = 'approve' | 'reject';
export interface SubmissionReview extends Omit<
  CourtDetail,
  'dataSource' | 'createdAt' | 'availability' | 'photos'
> {
  status: SubmissionStatus;
  submittedAt: string;
  photos: (CourtPhoto & { createdAt: string })[];
}
export type SubmissionSummary = Pick<
  SubmissionReview,
  | 'id'
  | 'name'
  | 'city'
  | 'address'
  | 'numberOfCourts'
  | 'indoorOutdoor'
  | 'bookingMethod'
  | 'status'
  | 'submittedAt'
>;
export interface ModerationReceipt {
  submissionId: string;
  courtId: string | null;
  submissionStatus: 'Approved' | 'Rejected';
  updatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class CourtModerationApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/admin/court-submissions';
  pending() {
    return this.http.get<SubmissionSummary[]>(this.baseUrl, { params: { status: 'Pending' } });
  }
  detail(id: string) {
    return this.http.get<SubmissionReview>(`${this.baseUrl}/${encodeURIComponent(id)}`);
  }
  decide(id: string, decision: ModerationDecision) {
    return this.http.post<ModerationReceipt>(
      `${this.baseUrl}/${encodeURIComponent(id)}/${decision}`,
      {},
    );
  }
}
