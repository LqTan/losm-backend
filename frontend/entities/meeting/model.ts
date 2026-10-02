import type { ActionId } from "@/shared/lib/brands";

export type MeetingStatus =
  | "Pending"
  | "Confirmed"
  | "Completed"
  | "Failed"
  | "PartiallyFailed"
  | "Cancelled";

export interface Meeting {
  readonly actionId: ActionId;
  readonly title: string;
  readonly description: string;
  readonly status: MeetingStatus;
  readonly startAt: string | null;
  readonly durationMinutes: number | null;
  readonly placeId: string | null;
  readonly placeName: string | null;
  readonly placeAddress: string | null;
  readonly attendeeEmails: readonly string[];
  readonly note: string | null;
  readonly emailsSent: boolean;
  readonly createdAt: string;
}

export interface PendingActionPayload {
  readonly title?: string;
  readonly startAt?: string;
  readonly durationMinutes?: number;
  readonly attendees?: readonly string[];
  readonly note?: string;
  readonly placeName?: string;
  readonly placeId?: string;
}

export interface PendingActionDetail {
  readonly actionId: ActionId;
  readonly description: string;
  readonly status: MeetingStatus;
  readonly payload: PendingActionPayload;
}

export interface CreateReviewInput {
  readonly placeId: string;
  readonly rating: number;
  readonly comment: string;
}

export interface Review {
  readonly id: string;
  readonly placeId: string;
  readonly placeName: string;
  readonly rating: number;
  readonly comment: string;
  readonly createdAt: string;
}

export interface SavedPlace {
  readonly savedPlaceId: string;
  readonly placeId: string;
  readonly name: string | null;
  readonly address: string | null;
  readonly createdAt: string;
}

export interface GoogleCalendarStatus {
  readonly connected: boolean;
  readonly googleEmail: string | null;
  readonly accessTokenExpiresAt: string | null;
}
