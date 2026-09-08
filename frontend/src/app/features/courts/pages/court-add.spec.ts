import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CourtAdd } from './court-add';

describe('Add a Court', () => {
  let fixture: ComponentFixture<CourtAdd>;
  let http: HttpTestingController;
  let element: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CourtAdd],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(CourtAdd);
    http = TestBed.inject(HttpTestingController);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });
  afterEach(() => http.verify());

  function fill(name: string, value: string) {
    element.querySelector<HTMLInputElement | HTMLSelectElement>(`[name="${name}"]`)!.value = value;
  }
  function validForm() {
    fill('name', 'Community court');
    fill('city', 'Makati');
    fill('address', 'Example street');
    fill('numberOfCourts', '2');
    fill('indoorOutdoor', 'Mixed');
    fill('bookingMethod', 'WalkIn');
  }
  function submit() {
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
  }

  it('associates required errors with fields and sends no request', () => {
    submit();
    expect(element.querySelector('#name')?.getAttribute('aria-invalid')).toBe('true');
    expect(element.querySelector('#name-help')?.textContent).toContain('required');
    http.expectNone('/api/court-submissions');
  });

  it('validates booking contact and safe URLs before sending', () => {
    validForm();
    fill('bookingMethod', 'Phone');
    fill('websiteUrl', 'javascript:alert(1)');
    submit();
    expect(element.querySelector('#phone-help')?.textContent).toContain('phone number');
    expect(element.querySelector('#websiteUrl-help')?.textContent).toContain('HTTP(S)');
    http.expectNone('/api/court-submissions');
  });

  it('submits only editable fields, prevents duplicate requests and confirms pending review', () => {
    validForm();
    submit();
    const request = http.expectOne('/api/court-submissions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.status).toBeUndefined();
    expect(request.request.body.photos).toEqual([]);
    expect(element.querySelector<HTMLFieldSetElement>('.form-body')?.disabled).toBe(true);
    submit();
    http.expectNone('/api/court-submissions');
    request.flush({ id: 'submission-id', status: 'Pending', submittedAt: '2026-09-09T00:00:00Z' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Submitted for verification');
    expect(element.textContent).toContain('not published yet');
    expect(element.querySelector('form')).toBeNull();
  });

  it('preserves entered values after a failed POST and allows retry', () => {
    validForm();
    submit();
    http.expectOne('/api/court-submissions').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('try submitting again');
    expect(element.querySelector<HTMLInputElement>('#name')?.value).toBe('Community court');
    submit();
    http
      .expectOne('/api/court-submissions')
      .flush({ id: 'id', status: 'Pending', submittedAt: '2026-09-09T00:00:00Z' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Submitted for verification');
  });
});
