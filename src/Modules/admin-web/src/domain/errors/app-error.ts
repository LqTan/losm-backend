/**
 * Business errors owned by the domain/application layer. The presentation
 * layer only needs the message to render it and never sees HTTP status codes.
 */
export abstract class AppError extends Error {
  abstract readonly code: string;

  constructor(message: string) {
    super(message);
    this.name = new.target.name;
  }
}

export class ValidationError extends AppError {
  readonly code = "VALIDATION_ERROR";
}

export class UnauthorizedError extends AppError {
  readonly code = "UNAUTHORIZED";
}

export class ForbiddenError extends AppError {
  readonly code = "FORBIDDEN";
}

export class NotFoundError extends AppError {
  readonly code = "NOT_FOUND";
}

export class ConflictError extends AppError {
  readonly code = "CONFLICT";
}

export class NetworkError extends AppError {
  readonly code = "NETWORK_ERROR";

  constructor(message = "Unable to reach the server.") {
    super(message);
  }
}

export function isAppError(error: unknown): error is AppError {
  return error instanceof AppError;
}

/** Safe message to surface to the user. */
export function toDisplayMessage(error: unknown): string {
  if (isAppError(error)) return error.message;
  if (error instanceof Error) return error.message;
  return "Something went wrong. Please try again.";
}
