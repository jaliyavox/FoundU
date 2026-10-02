import { api } from '@/lib/api/client'

/** Always the same answer, whether or not the address has an account. */
export const requestPasswordReset = (email: string) =>
  api.post<{ message: string }>('/api/auth/forgot-password', { email }, { anonymous: true })

export const resetPassword = (email: string, token: string, newPassword: string) =>
  api.post<void>('/api/auth/reset-password', { email, token, newPassword }, { anonymous: true })

export const confirmEmail = (userId: string, token: string) =>
  api.post<void>('/api/auth/confirm-email', { userId, token }, { anonymous: true })

/** Sends the confirmation link again, to the signed-in account's address. */
export const resendConfirmation = () => api.post<void>('/api/auth/resend-confirmation')
