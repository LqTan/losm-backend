export type AreaLevel = "Country" | "Province" | "District" | "Commune";

export const AreaLevelNumber: Record<AreaLevel, number> = {
  Country: 0,
  Province: 1,
  District: 2,
  Commune: 3,
};

export interface Area {
  id: string;
  name: string;
  level: number;
  levelName: AreaLevel;
  parentId: string | null;
  externalCode: string | null;
  bboxMinLat: number;
  bboxMinLng: number;
  bboxMaxLat: number;
  bboxMaxLng: number;
  isActive: boolean;
}

export interface CreateAreaInput {
  name: string;
  level: number;
  parentId: string | null;
  externalCode?: string | null;
  bboxMinLat: number;
  bboxMinLng: number;
  bboxMaxLat: number;
  bboxMaxLng: number;
  isActive?: boolean;
}

export interface UpdateAreaInput extends CreateAreaInput {
  id: string;
}

export interface Place {
  id: string;
  externalId: string;
  name: string;
  address: string | null;
  latitude: number;
  longitude: number;
  openingHours: string | null;
  category: string | null;
  source: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreatePlaceInput {
  name: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  category?: string | null;
  openingHours?: string | null;
  source?: string | null;
  externalId?: string | null;
}

export interface UpdatePlaceInput {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  address?: string | null;
  category?: string | null;
  openingHours?: string | null;
  source?: string | null;
}