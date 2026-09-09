import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { of } from 'rxjs';
import { CourtsApi } from './features/courts/data-access/courts-api';

describe('Kitchain foundation', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        {
          provide: CourtsApi,
          useValue: {
            search: () => of({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders the shell, skip link and the intended navigation', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const labels = (selector: string) =>
      Array.from(element.querySelectorAll(selector), (link) => link.textContent?.trim());

    expect(labels('nav[aria-label="Primary"] a')).toEqual(['Courts', 'Play', 'Gear']);
    expect(labels('nav[aria-label="Mobile primary"] a')).toEqual([
      'Home',
      'Courts',
      'Play',
      'Gear',
    ]);
    expect(element.querySelector('.skip-link')?.getAttribute('href')).toBe('/#main-content');
    expect(element.querySelector('main#main-content')).not.toBeNull();
  });

  it.each([
    ['/', 'Your pickleball', 'Home'],
    ['/courts', 'Find your place', 'Courts'],
    ['/play', 'Bring the group', 'Play'],
    ['/gear', 'Make it your game.', 'Gear'],
  ])('renders %s and exposes its active navigation state', async (url, heading, label) => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl(url);
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent).toContain(heading);
    expect(element.querySelectorAll('h1')).toHaveLength(1);
    expect(element.querySelector('.skip-link')?.getAttribute('href')).toBe(`${url}#main-content`);
    expect(
      element
        .querySelector('nav[aria-label="Mobile primary"] [aria-current="page"]')
        ?.textContent?.trim(),
    ).toBe(label);
    if (url !== '/courts' && url !== '/play')
      expect(element.querySelector('.foundation-note')?.textContent).toMatch(
        /still to come|not available yet/,
      );
  });
});
