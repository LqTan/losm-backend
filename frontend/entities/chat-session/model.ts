import type { GeoPoint } from "@/entities/place/model";
import type { PlaceId, SessionId } from "@/shared/lib/brands";

export type Role = "user" | "assistant" | "system";

export interface ChatMessage {
  readonly id: string;
  readonly role: Role;
  readonly content: string;
  readonly places?: readonly AttachedPlace[];
  readonly pendingActionId?: string;
  readonly createdAt: string;
}

export interface AttachedPlace {
  readonly id: PlaceId;
  readonly name: string;
  readonly address: string | null;
  readonly location: GeoPoint;
  readonly category: string | null;
  readonly openingHours: string | null;
  readonly distanceKm: number | null;
  readonly finalScore: number | null;
}

export type AgentStepKind =
  | "ToolCall"
  | "ToolResult"
  | "ModelResponse"
  | "Finalize"
  | "PendingAction";

export interface AgentStepEvent {
  readonly type: "step";
  readonly order: number;
  readonly kind: AgentStepKind;
  readonly toolName: string | null;
  readonly summary: string | null;
  readonly succeeded: boolean;
}

export interface AgentResultEvent {
  readonly type: "result";
  readonly sessionId: SessionId;
  readonly answer: string;
  readonly places: readonly AttachedPlace[];
  readonly pendingActionIds: readonly string[];
}

export interface AgentErrorEvent {
  readonly type: "error";
  readonly message: string;
}

export type AgentStreamEvent =
  | AgentStepEvent
  | AgentResultEvent
  | AgentErrorEvent;

export interface ChatSession {
  readonly id: SessionId;
  readonly title: string;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface SessionHistory {
  readonly sessionId: SessionId;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly messages: readonly ChatMessage[];
}
