import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { App } from '../../../app';
import { routes } from '../../../app.routes';
import { CourtDetail } from '../data-access/court.models';

// Test-only data matching the existing public detail contract.
const venue: CourtDetail = {
  id: '00000000-0000-4000-8000-000000000001',
  name: 'Test Court',
  city: 'Makati',
  address: 'Test address',
  region: 'Metro Manila',
  latitude: null,
  longitude: null,
  numberOfCourts: 4,
  indoorOutdoor: 'Mixed',
  surface: 'Acrylic',
  openingHours: 'Monday–Friday\n8 am–8 pm',
  startingPrice: 0,
  currencyCode: 'PHP',
  priceUnit: 'PerPerson',
  amenities: ['Parking', 'PaddleRental'],
  bookingMethod: 'ExternalPlatform',
  phone: '+63 (2) 8123-4567',
  websiteUrl: 'https://example.com/venue',
  socialUrl: 'https://example.com/social',
  bookingUrl: 'https://example.com/book',
  dataSource: 'CommunitySupplied',
  availability: { status: 'NotIntegrated', message: 'Not integrated' },
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('Court Details', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let router: Router;
  let element: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(App);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });
  afterEach(() => http.verify());

  async function navigate(id = venue.id, query = '') {
    await router.navigateByUrl(`/courts/${id}${query}`);
    fixture.detectChanges();
    TestBed.tick();
  }
  async function settle() {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  function request(id = venue.id) {
    return http.expectOne(`/api/courts/${id}`);
  }

  it('shows loading, then renders venue facts, zero pricing and neutral availability', async () => {
    await navigate();
    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      'Loading venue details',
    );
    request().flush(venue);
    await settle();
    expect(element.querySelector('h1')?.textContent).toBe('Test Court');
    expect(element.querySelectorAll('h1')).toHaveLength(1);
    for (const text of [
      'Test address',
      'Mixed',
      'Acrylic',
      'Monday–Friday',
      '₱0',
      '/ person',
      'Paddle rental',
      'Community supplied',
      'Availability not connected yet',
    ]) {
      expect(element.textContent).toContain(text);
    }
    expect(TestBed.inject(Title).getTitle()).toBe('Test Court | Kitchain');
  });

  it('renders a calm not-found state for a missing or non-public venue', async () => {
    await navigate();
    request().flush({}, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(element.querySelector('h1')?.textContent).toBe('Court not found.');
    expect(element.querySelector('a.text-link')?.getAttribute('href')).toBe('/courts');
    expect(element.querySelector('article')).toBeNull();
  });

  it('does not request malformed IDs', async () => {
    await navigate('not-a-court-id');
    http.expectNone((req) => req.url.startsWith('/api/'));
    expect(element.querySelector('h1')?.textContent).toBe('Court not found.');
  });

  it('retries the same detail request after a network failure', async () => {
    await navigate();
    request().error(new ProgressEvent('error'));
    await settle();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Please try again');
    element.querySelector<HTMLButtonElement>('button.primary-action')!.click();
    TestBed.tick();
    request().flush(venue);
    await settle();
    expect(element.querySelector('h1')?.textContent).toBe(venue.name);
  });

  it('uses safe external booking/contact links and a normalized dial target', async () => {
    await navigate();
    request().flush(venue);
    await settle();
    for (const url of [venue.bookingUrl, venue.websiteUrl, venue.socialUrl]) {
      const link = element.querySelector<HTMLAnchorElement>(`a[href="${url}"]`)!;
      expect(link.target).toBe('_blank');
      expect(link.rel).toContain('noopener');
      expect(link.rel).toContain('noreferrer');
      expect(link.textContent).toContain('opens in a new tab');
    }
    expect(element.querySelector('a[href="tel:+63281234567"]')).not.toBeNull();
    expect(element.textContent).toContain('does not confirm reservations or process payments');
  });

  it('omits absent sections and unsafe URLs while keeping walk-in and unknown-price information', async () => {
    await navigate();
    request().flush({
      ...venue,
      surface: null,
      openingHours: null,
      amenities: [],
      phone: null,
      startingPrice: null,
      currencyCode: null,
      priceUnit: null,
      bookingMethod: 'WalkIn',
      bookingUrl: 'javascript:alert(1)',
      websiteUrl: 'https://user:pass@example.com',
      socialUrl: null,
    });
    await settle();
    expect(element.textContent).toContain('Ask about pricing');
    expect(element.textContent).toContain('Walk-in is the listed booking method');
    expect(element.querySelector('#amenities-title')).toBeNull();
    expect(element.querySelector('#contact-title')).toBeNull();
    expect(element.textContent).not.toContain('Opening hours');
    expect(element.querySelector('a[target="_blank"]')).toBeNull();
  });

  it('cancels an old detail request when navigating to another venue', async () => {
    await navigate();
    const old = request();
    const nextId = '00000000-0000-4000-8000-000000000002';
    await navigate(nextId);
    expect(old.cancelled).toBe(true);
    request(nextId).flush({ ...venue, id: nextId, name: 'Second Court' });
    await settle();
    expect(element.querySelector('h1')?.textContent).toBe('Second Court');
  });

  it('preserves Explore filters and pagination on the return link', async () => {
    await navigate(venue.id, '?city=Makati&page=2&pageSize=2');
    request().flush(venue);
    await settle();
    const link = element.querySelector<HTMLAnchorElement>('a.back-link')!;
    expect(link.getAttribute('href')).toBe('/courts?city=Makati&page=2&pageSize=2');
    expect(
      element.querySelector('nav[aria-label="Mobile primary"] [aria-current="page"]')?.textContent,
    ).toContain('Courts');
    link.click();
    await settle();
    const search = http.expectOne((req) => req.url === '/api/courts');
    expect(search.request.params.get('city')).toBe('Makati');
    expect(search.request.params.get('page')).toBe('2');
    search.flush({ items: [], page: 2, pageSize: 2, totalCount: 0, totalPages: 0 });
    await settle();
  });
});
