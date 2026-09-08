import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { CourtPage, CourtSearch } from './court.models';

@Injectable({ providedIn: 'root' })
export class CourtsApi {
  private readonly http = inject(HttpClient);

  search(query: CourtSearch) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<CourtPage>('/api/courts', { params });
  }
}
