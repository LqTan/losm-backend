declare const _brand: unique symbol;
type Brand<T, B> = T & { readonly [_brand]: B };

export type PlaceId = Brand<string, "PlaceId">;
export type SessionId = Brand<string, "SessionId">;
export type UserId = Brand<string, "UserId">;
export type ActionId = Brand<string, "ActionId">;

export const asPlaceId = (s: string): PlaceId => s as PlaceId;
export const asSessionId = (s: string): SessionId => s as SessionId;
export const asUserId = (s: string): UserId => s as UserId;
export const asActionId = (s: string): ActionId => s as ActionId;
