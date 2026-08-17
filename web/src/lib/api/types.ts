/** Mirrors FoundU.Domain.Enums.UserRole. */
export type UserRole = 'Student' | 'Staff' | 'Admin'

/** Mirrors FoundU.Application.Auth.Dtos.UserDto. */
export interface User {
  id: string
  fullName: string
  email: string
  role: UserRole
  studentNumber: string | null
  isSuspended: boolean
}

/** Mirrors FoundU.Application.Auth.Dtos.AuthResponse. */
export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  user: User
}

/** The RFC 7807 envelope GlobalExceptionHandler returns for every error. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Field-level messages, present only on 400s raised by ValidationAppException. */
  errors?: Record<string, string[]>
}
