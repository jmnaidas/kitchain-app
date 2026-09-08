export const indoorOutdoorTypes = ['Indoor', 'Outdoor', 'Mixed'] as const;
export type IndoorOutdoor = (typeof indoorOutdoorTypes)[number];

export const amenityLabels = {
  Parking: 'Parking',
  Restroom: 'Restrooms',
  Shower: 'Showers',
  ChangingRoom: 'Changing rooms',
  AirConditioning: 'Air conditioning',
  PaddleRental: 'Paddle rental',
  BallOrEquipmentRental: 'Equipment rental',
  ProShop: 'Pro shop',
  FoodAndDrinks: 'Food & drinks',
  SeatingOrWaitingArea: 'Seating area',
  Coaching: 'Coaching',
  Lockers: 'Lockers',
  Other: 'Other amenities',
} as const;
export type Amenity = keyof typeof amenityLabels;
export const amenityOptions = Object.entries(amenityLabels).map(([value, label]) => ({
  value,
  label,
}));

export const bookingLabels = {
  ExternalPlatform: 'External platform',
  Website: 'Website',
  GoogleForm: 'Google Form',
  Phone: 'Phone',
  Message: 'Message',
  WalkIn: 'Walk-in',
  Other: 'Contact venue',
} as const;
export const sourceLabels = {
  OwnerSupplied: 'Owner supplied',
  CommunitySupplied: 'Community supplied',
  KitchainCurated: 'Kitchain curated',
} as const;
export const priceUnitLabels = {
  PerHour: '/ hour',
  PerPerson: '/ person',
  PerSession: '/ session',
} as const;

/** Public list contract only; no persistence types or unneeded detail fields. */
export interface CourtSummary {
  id: string;
  name: string;
  city: string;
  address: string;
  numberOfCourts: number;
  indoorOutdoor: IndoorOutdoor;
  startingPrice: number | null;
  currencyCode: string | null;
  priceUnit: keyof typeof priceUnitLabels | null;
  amenities: Amenity[];
  bookingMethod: keyof typeof bookingLabels;
  dataSource: keyof typeof sourceLabels;
  availability: { status: 'NotIntegrated'; message: string };
}

export interface CourtPage {
  items: CourtSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CourtDetail extends CourtSummary {
  region: string | null;
  latitude: number | null;
  longitude: number | null;
  surface: string | null;
  openingHours: string | null;
  phone: string | null;
  websiteUrl: string | null;
  socialUrl: string | null;
  bookingUrl: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CourtSearch {
  city?: string;
  indoorOutdoor?: IndoorOutdoor;
  minCourts?: number;
  maxStartingPrice?: number;
  currencyCode?: string;
  amenity?: Amenity;
  page: number;
  pageSize: number;
}
