import { ParamMap, Params } from '@angular/router';
import {
  Amenity,
  amenityLabels,
  CourtSearch,
  indoorOutdoorTypes,
  IndoorOutdoor,
} from './court.models';

export const defaultSearch: CourtSearch = { page: 1, pageSize: 5 };

/** Invalid shared links are explained in the UI instead of silently broadening the search. */
export function readCourtQuery(params: ParamMap): { query: CourtSearch; invalid: boolean } {
  const query: CourtSearch = { ...defaultSearch };
  let invalid = false;
  const read = (key: string) => {
    if (params.getAll(key).length > 1) invalid = true;
    return params.get(key)?.trim() || undefined;
  };
  const city = read('city');
  if (city && city.length <= 100) query.city = city;
  else if (city) invalid = true;

  const setting = read('indoorOutdoor');
  if (setting && indoorOutdoorTypes.includes(setting as IndoorOutdoor))
    query.indoorOutdoor = setting as IndoorOutdoor;
  else if (setting) invalid = true;

  const amenity = read('amenity');
  if (amenity && Object.hasOwn(amenityLabels, amenity)) query.amenity = amenity as Amenity;
  else if (amenity) invalid = true;

  const currency = read('currencyCode');
  if (currency && /^[a-z]{3}$/i.test(currency)) query.currencyCode = currency.toUpperCase();
  else if (currency) invalid = true;

  const number = (key: string, min: number, max: number, integer = true) => {
    const value = read(key);
    if (value === undefined) return undefined;
    const parsed = Number(value);
    if (
      !/^\d+(\.\d+)?$/.test(value) ||
      !Number.isFinite(parsed) ||
      parsed < min ||
      parsed > max ||
      (integer && !Number.isInteger(parsed))
    ) {
      invalid = true;
      return undefined;
    }
    return parsed;
  };
  query.minCourts = number('minCourts', 1, 2147483647);
  query.maxStartingPrice = number('maxStartingPrice', 0, 9999999999.99, false);
  query.page = number('page', 1, 1000000) ?? defaultSearch.page;
  query.pageSize = number('pageSize', 1, 100) ?? defaultSearch.pageSize;
  if (query.maxStartingPrice !== undefined) query.currencyCode ??= 'PHP';
  return { query, invalid };
}

export function courtQueryParams(query: CourtSearch): Params {
  const params: Params = {};
  for (const [key, value] of Object.entries(query)) {
    if (
      value !== undefined &&
      value !== '' &&
      !(key === 'page' && value === 1) &&
      !(key === 'pageSize' && value === defaultSearch.pageSize)
    )
      params[key] = value;
  }
  return params;
}

export function activeFilterLabels(query: CourtSearch): string[] {
  return [
    query.city,
    query.indoorOutdoor,
    query.minCourts !== undefined ? `${query.minCourts}+ courts` : undefined,
    query.maxStartingPrice !== undefined
      ? `Up to ${query.currencyCode ?? 'PHP'} ${query.maxStartingPrice.toLocaleString('en-PH')}`
      : query.currencyCode
        ? `Currency: ${query.currencyCode}`
        : undefined,
    query.amenity ? amenityLabels[query.amenity] : undefined,
  ].filter((label): label is string => label !== undefined);
}
