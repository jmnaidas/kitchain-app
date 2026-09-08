import { convertToParamMap } from '@angular/router';
import { activeFilterLabels, courtQueryParams, readCourtQuery } from './court-query';

describe('Court search links', () => {
  it('restores filters, trims the city, and defaults a zero price cap to PHP', () => {
    const result = readCourtQuery(
      convertToParamMap({
        city: ' Makati ',
        indoorOutdoor: 'Indoor',
        amenity: 'Parking',
        minCourts: '2',
        maxStartingPrice: '0',
        page: '2',
        pageSize: '10',
      }),
    );
    expect(result.invalid).toBe(false);
    expect(result.query).toEqual({
      city: 'Makati',
      indoorOutdoor: 'Indoor',
      amenity: 'Parking',
      minCourts: 2,
      maxStartingPrice: 0,
      currencyCode: 'PHP',
      page: 2,
      pageSize: 10,
    });
    expect(activeFilterLabels(result.query)).toContain('Up to PHP 0');
  });

  it.each([
    { page: '0' },
    { page: '1.5' },
    { page: '1000001' },
    { pageSize: '101' },
    { minCourts: '-1' },
    { maxStartingPrice: 'NaN' },
    { currencyCode: 'pesos' },
    { amenity: ['Parking', 'Shower'] },
    { amenity: 'Pool' },
    { indoorOutdoor: 'Both' },
  ])('rejects unsupported shared query values: %j', (values) => {
    expect(readCourtQuery(convertToParamMap(values)).invalid).toBe(true);
  });

  it('creates a compact shareable URL without losing an explicit currency', () => {
    expect(
      courtQueryParams({
        page: 1,
        pageSize: 5,
        city: undefined,
        maxStartingPrice: 300,
        currencyCode: 'USD',
      }),
    ).toEqual({ maxStartingPrice: 300, currencyCode: 'USD' });
  });
});
