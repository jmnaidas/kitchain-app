import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from '../../../app';
import { routes } from '../../../app.routes';
import { SubmissionReview } from './court-moderation-api';

const submission: SubmissionReview = {
  id: '00000000-0000-4000-8000-000000000001',
  name: 'Community court',
  city: 'Makati',
  address: 'Example street',
  region: 'Metro Manila',
  latitude: 14.55,
  longitude: 121.02,
  numberOfCourts: 3,
  indoorOutdoor: 'Mixed',
  surface: 'Acrylic',
  openingHours: 'Daily 8am–8pm',
  startingPrice: 0,
  currencyCode: 'PHP',
  priceUnit: 'PerPerson',
  amenities: ['Parking', 'Shower'],
  bookingMethod: 'Website',
  phone: '09170000000',
  websiteUrl: 'https://example.com/venue',
  socialUrl: null,
  bookingUrl: 'https://example.com/book',
  status: 'Pending',
  submittedAt: '2026-09-09T01:00:00Z',
  updatedAt: '2026-09-09T01:00:00Z',
  photos: [
    {
      id: 'photo-id',
      imageUrl: 'https://example.com/court.jpg',
      altText: 'Main playing area',
      displayOrder: 0,
      isPrimary: true,
      createdAt: '2026-09-09T01:00:00Z',
    },
  ],
};
const baseUrl = '/api/admin/court-submissions';

describe('Court submission moderation', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let router: Router;
  let element: HTMLElement;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });
  afterEach(() => http.verify());
  async function navigate(url: string) {
    await router.navigateByUrl(url);
    fixture.detectChanges();
    TestBed.tick();
  }
  async function settle() {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  async function openReview() {
    await navigate(`/admin/court-submissions/${submission.id}`);
    http.expectOne(`${baseUrl}/${submission.id}`).flush(submission);
    await settle();
  }
  function button(label: string) {
    const found = Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find(
      (button) => button.textContent?.trim() === label,
    );
    if (!found) throw new Error(`Missing button: ${label}`);
    return found;
  }
  async function click(label: string) {
    button(label).click();
    fixture.detectChanges();
    TestBed.tick();
  }

  it('loads pending summaries with review links and an explicit unprotected preview notice', async () => {
    await navigate('/admin/court-submissions');
    expect(element.textContent).toContain('Loading submissions');
    http.expectOne(`${baseUrl}?status=Pending`).flush([submission]);
    await settle();
    for (const text of [
      'Community court',
      'Makati',
      '3 courts',
      'Website',
      'Pending',
      'No authentication',
    ]) {
      expect(element.textContent).toContain(text);
    }
    expect(
      element.querySelector('a[aria-label="Review Community court"]')?.getAttribute('href'),
    ).toBe(`/admin/court-submissions/${submission.id}`);
  });

  it('renders details, confirms approval and links to the public Court before returning to a fresh queue', async () => {
    await openReview();
    for (const text of [
      'Example street',
      'Acrylic',
      'Daily 8am',
      'PHP 0',
      'Shower',
      'Primary image',
      'Main playing area',
    ]) {
      expect(element.textContent).toContain(text);
    }
    expect(element.querySelector('img')?.getAttribute('alt')).toBe('Main playing area');
    await click('Approve');
    http.expectNone((request) => request.method === 'POST');
    expect(element.textContent).toContain('Publish this venue?');
    await click('Cancel');
    expect(element.textContent).not.toContain('Publish this venue?');
    await click('Approve');
    await click('Confirm approval');
    const request = http.expectOne(`${baseUrl}/${submission.id}/approve`);
    expect(request.request.method).toBe('POST');
    expect(button('Saving decision…').disabled).toBe(true);
    button('Saving decision…').click();
    http.expectNone((request) => request.method === 'POST');
    const courtId = '00000000-0000-4000-8000-000000000002';
    request.flush({
      submissionId: submission.id,
      courtId,
      submissionStatus: 'Approved',
      updatedAt: '2026-09-09T02:00:00Z',
    });
    await settle();
    expect(element.textContent).toContain('Approved and published.');
    expect(element.querySelector(`a[href="/courts/${courtId}"]`)?.textContent).toContain(
      'View published Court',
    );
    await navigate('/admin/court-submissions');
    http.expectOne(`${baseUrl}?status=Pending`).flush([]);
    await settle();
    expect(element.textContent).toContain('The queue is clear.');
  });

  it('confirms rejection and displays retained history without a public Court link', async () => {
    await openReview();
    await click('Reject');
    http.expectNone((request) => request.method === 'POST');
    await click('Confirm rejection');
    http.expectOne(`${baseUrl}/${submission.id}/reject`).flush({
      submissionId: submission.id,
      courtId: null,
      submissionStatus: 'Rejected',
      updatedAt: '2026-09-09T02:00:00Z',
    });
    await settle();
    expect(element.textContent).toContain('Submission rejected.');
    expect(element.textContent).toContain('kept for history');
    expect(element.textContent).not.toContain('View published Court');
    expect(element.textContent).not.toContain('Confirm rejection');
  });

  it('requires a reload after conflict and disables decisions for an already processed submission', async () => {
    await openReview();
    await click('Approve');
    await click('Confirm approval');
    http
      .expectOne(`${baseUrl}/${submission.id}/approve`)
      .flush({}, { status: 409, statusText: 'Conflict' });
    await settle();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'already been processed',
    );
    await click('Reload submission');
    http.expectOne(`${baseUrl}/${submission.id}`).flush({ ...submission, status: 'Rejected' });
    await settle();
    expect(element.textContent).toContain('No further decisions');
    expect(
      Array.from(element.querySelectorAll('button')).some(
        (button) => button.textContent?.trim() === 'Approve',
      ),
    ).toBe(false);
  });

  it('handles missing submissions and recovers from queue errors', async () => {
    await navigate(`/admin/court-submissions/${submission.id}`);
    http
      .expectOne(`${baseUrl}/${submission.id}`)
      .flush({}, { status: 404, statusText: 'Not found' });
    await settle();
    expect(element.textContent).toContain('Submission not found');
    await navigate('/admin/court-submissions');
    http
      .expectOne(`${baseUrl}?status=Pending`)
      .flush({}, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('couldn’t load');
    await click('Try again');
    http.expectOne(`${baseUrl}?status=Pending`).flush([]);
    await settle();
    expect(element.textContent).toContain('The queue is clear.');
  });
});
