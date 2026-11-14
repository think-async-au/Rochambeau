// Mirrors contracts/openapi.yaml (accounts). Dates are ISO-8601 UTC strings.

export type AccountRole = 'player' | 'administrator'

/** The caller's own Profile — the only shape that carries an email (AC-09). */
export interface OwnProfile {
  username: string
  email: string
  joinedAt: string
  role: AccountRole
}

/** Another Player's Profile — never an email (AC-11). */
export interface PublicProfile {
  username: string
  joinedAt: string
  role: AccountRole
}

/** Field name → what that field must be (ValidationProblemDetails.errors, AC-02). */
export type FieldErrors = Record<string, string[]>
