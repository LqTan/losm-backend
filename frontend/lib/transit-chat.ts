export type TransitChatLocation = {
  name?: string | null;
  latitude?: number | null;
  longitude?: number | null;
};

export type TransitChatStop = {
  id: string;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  mode: string;
  distanceMeters: number | null;
  routeNumbers: number[];
};

export type TransitChatRoute = {
  id: string;
  number: number;
  name: string;
  mode: string;
  patternId: string;
  direction: number;
  directionName: string;
  firstMinute: number | null;
  lastMinute: number | null;
  headwayMin: number | null;
  headwayMax: number | null;
  serviceDays: string;
  referenceFare: number | null;
  fareDescription: string;
  hasExcludedStops: boolean;
  stops: TransitChatStop[];
};

export type TransitChatJourney = {
  route: TransitChatRoute;
  boardingStop: TransitChatStop;
  alightingStop: TransitChatStop;
  stopCount: number;
  accessDistanceMeters: number;
  egressDistanceMeters: number;
  score?: number;
  rankingMethod?: string;
};

export type TransitChatResult = {
  kind: string;
  status: string;
  message: string;
  supportedAreas: string[];
  stops: TransitChatStop[];
  routes: TransitChatRoute[];
  journeys: TransitChatJourney[];
  origin?: TransitChatLocation | null;
  destination?: TransitChatLocation | null;
  rankingMethod?: string;
};

