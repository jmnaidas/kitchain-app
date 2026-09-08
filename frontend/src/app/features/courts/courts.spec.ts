import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from '../../app';
import { routes } from '../../app.routes';
import { CourtPage, CourtSummary } from './data-access/court.models';

// Test-only contract fixture. Runtime venues always come from the API.
const venue: CourtSummary = {
  id: '00000000-0000-4000-8000-000000000001',
  name: 'Test venue',
  city: 'Makati',
  address: 'Test address',
  numberOfCourts: 4,
  indoorOutdoor: 'Indoor',
  startingPrice: 0,
  currencyCode: 'PHP',
  priceUnit: 'PerPerson',
  amenities: ['Parking', 'Restroom', 'Shower', 'Lockers'],
  bookingMethod: 'Phone',
  dataSource: 'OwnerSupplied',
  availability: { status: 'NotIntegrated', message: 'Availability not integrated' },
};
const response = (
  items: CourtSummary[] = [venue],
  overrides: Partial<CourtPage> = {},
): CourtPage => ({
  items,
  page: 1,
  pageSize: 5,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
  ...overrides,
});

describe('Courts Explore', () => {
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

  async function navigate(url = '/courts') {
    await router.navigateByUrl(url);
    fixture.detectChanges();
    TestBed.tick();
  }
  function request() {
    return http.expectOne((req) => req.url === '/api/courts');
  }
  async function settle() {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  function button(label: string) {
    const result = Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find(
      (item) => item.textContent?.trim() === label || item.getAttribute('aria-label') === label,
    );
    if (!result) throw new Error(`Missing button: ${label}`);
    return result;
  }

  it('loads real HTTP results with honest zero pricing, provenance and availability', async () => {
    await navigate();
    expect(element.textContent).toContain('Finding places to play');
    const pending = request();
    expect(pending.request.params.get('pageSize')).toBe('5');
    pending.flush(response());
    await settle();
    expect(element.querySelector('article h3')?.textContent).toBe('Test venue');
    expect(element.textContent).toContain('₱0');
    expect(element.textContent).toContain('/ person');
    expect(element.textContent).toContain('Owner supplied');
    expect(element.textContent).toContain('Booking: Phone');
    expect(element.textContent).toContain('Availability not connected yet');
    expect(element.querySelector('details summary')?.textContent).toContain('+1 more');
    expect(element.querySelector('article a')?.getAttribute('href')).toContain(
      `/courts/${venue.id}`,
    );
  });

  it('restores a shared search and sends exactly one selected amenity with server pagination', async () => {
    await navigate(
      '/courts?city=Makati&indoorOutdoor=Indoor&amenity=Parking&minCourts=2&maxStartingPrice=600&page=2&pageSize=2',
    );
    const pending = request();
    expect(pending.request.params.getAll('amenity')).toEqual(['Parking']);
    expect(pending.request.params.get('currencyCode')).toBe('PHP');
    expect(pending.request.params.get('page')).toBe('2');
    pending.flush(response([venue], { page: 2, pageSize: 2, totalCount: 3, totalPages: 2 }));
    await settle();
    expect(element.querySelector<HTMLInputElement>('#court-city')?.value).toBe('Makati');
    expect(element.querySelector<HTMLSelectElement>('#court-setting')?.value).toBe('Indoor');
    expect(element.querySelector<HTMLSelectElement>('#court-amenity')?.value).toBe('Parking');
    expect(element.querySelector<HTMLSelectElement>('.page-size select')?.value).toBe('2');
    expect(button('Next page').disabled).toBe(true);
    button('Previous page').click();
    await settle();
    const previous = request();
    expect(previous.request.params.get('page')).toBe('1');
    expect(previous.request.params.get('city')).toBe('Makati');
    previous.flush(response());
    await settle();
    expect(router.url).not.toContain('page=2');
  });

  it('applies a draft only on submit and reset clears the URL and controls', async () => {
    await navigate();
    request().flush(response());
    await settle();
    const city = element.querySelector<HTMLInputElement>('#court-city')!;
    city.value = ' Pasig ';
    city.dispatchEvent(new Event('input'));
    http.expectNone((req) => req.url === '/api/courts');
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await settle();
    const filtered = request();
    expect(filtered.request.params.get('city')).toBe('Pasig');
    filtered.flush(response());
    await settle();
    expect(router.url).toContain('city=Pasig');
    // An unapplied edit in an otherwise unchanged field must also clear.
    element.querySelector<HTMLInputElement>('#court-count')!.value = '9';
    button('Clear filters').click();
    await settle();
    const cleared = request();
    expect(cleared.request.params.has('city')).toBe(false);
    cleared.flush(response());
    await settle();
    expect(router.url).toBe('/courts');
    expect(city.value).toBe('');
    expect(element.querySelector<HTMLInputElement>('#court-count')?.value).toBe('');
  });

  it('keeps filters through an HTTP failure and retry, then explains empty results', async () => {
    await navigate('/courts?city=Makati');
    request().flush({}, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('We couldn’t load');
    button('Try again').click();
    TestBed.tick();
    const retry = request();
    expect(retry.request.params.get('city')).toBe('Makati');
    retry.flush(response([]));
    await settle();
    expect(element.textContent).toContain('No places match those filters');
  });

  it('handles an out-of-range page without calling it an empty search', async () => {
    await navigate('/courts?page=9');
    request().flush(response([], { page: 9, totalCount: 5, totalPages: 1 }));
    await settle();
    expect(element.textContent).toContain('This page is past the last result');
    button('Go to first page').click();
    await settle();
    request().flush(response());
    await settle();
    expect(router.url).toBe('/courts');
  });

  it('cancels stale HTTP searches when the URL changes', async () => {
    await navigate('/courts?city=Makati');
    const old = request();
    await navigate('/courts?city=Pasig');
    expect(old.cancelled).toBe(true);
    request().flush(response([]));
    await settle();
    expect(element.querySelector<HTMLInputElement>('#court-city')?.value).toBe('Pasig');
  });

  it('blocks invalid query links instead of sending a broader search', async () => {
    await navigate('/courts?amenity=Parking&amenity=Shower');
    http.expectNone((req) => req.url === '/api/courts');
    expect(element.textContent).toContain('This search link needs a small fix');
  });

  it('shows unknown pricing and opens details with the Courts navigation active', async () => {
    await navigate('/courts?city=Makati');
    request().flush(
      response([{ ...venue, startingPrice: null, currencyCode: null, priceUnit: null }]),
    );
    await settle();
    expect(element.textContent).toContain('Ask about pricing');
    const link = element.querySelector<HTMLAnchorElement>('article a')!;
    expect(link.href).toContain('city=Makati');
    link.click();
    await settle();
    http.expectOne(`/api/courts/${venue.id}`).flush({
      ...venue,
      region: null,
      latitude: null,
      longitude: null,
      surface: null,
      openingHours: null,
      phone: null,
      websiteUrl: null,
      socialUrl: null,
      bookingUrl: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    });
    await settle();
    expect(element.querySelector('h1')?.textContent).toContain(venue.name);
    expect(
      element.querySelector('nav[aria-label="Mobile primary"] [aria-current="page"]')?.textContent,
    ).toContain('Courts');
    http.expectNone((req) => req.url.startsWith('/api/'));
  });
});
