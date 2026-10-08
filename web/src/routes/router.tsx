import type { ComponentType } from 'react'
import { createBrowserRouter } from 'react-router-dom'
import { AuthLoading } from '@/features/auth/auth-loading'
import { AppLayout } from '@/components/layout/app-layout'
import { ProtectedRoute } from './protected-route'

/**
 * Each page is its own chunk, loaded when its route is first opened. Before this, the whole app
 * (charts, calendars, every admin screen) was one 1.4 MB script that a student opening the
 * landing page had to download and parse first - Lighthouse measured 4 s+ to first paint on
 * a phone (Assignment 2, defect 67).
 */
function page<M extends Record<string, unknown>>(load: () => Promise<M>, name: keyof M & string) {
  return {
    lazy: async () => ({ Component: (await load())[name] as ComponentType }),
    HydrateFallback: AuthLoading,
  }
}

export const router = createBrowserRouter([
  // Public. Signed-in visitors are redirected to their role's home from inside the page.
  { path: '/', ...page(() => import('@/pages/landing-page'), 'LandingPage') },
  { path: '/login', ...page(() => import('@/features/auth/login-page'), 'LoginPage') },
  { path: '/register', ...page(() => import('@/features/auth/register-page'), 'RegisterPage') },
  // From the emails. Public: the person following the link may well be signed out.
  { path: '/forgot-password', ...page(() => import('@/features/auth/forgot-password-page'), 'ForgotPasswordPage') },
  { path: '/reset-password', ...page(() => import('@/features/auth/reset-password-page'), 'ResetPasswordPage') },
  { path: '/confirm-email', ...page(() => import('@/features/auth/confirm-email-page'), 'ConfirmEmailPage') },
  { path: '/feed', ...page(() => import('@/features/feed/feed-page'), 'FeedPage') },
  // The board behind the "Fresh finds" strip. Public, like the lost feed.
  { path: '/found', ...page(() => import('@/features/feed/found-board-page'), 'FoundBoardPage') },

  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          {
            element: <ProtectedRoute allow={['Staff', 'Admin']} />,
            children: [
              { path: 'items', ...page(() => import('@/features/items/items-page'), 'ItemsPage') },
              { path: 'items/new', ...page(() => import('@/features/items/log-item-page'), 'LogItemPage') },
              { path: 'items/:id', ...page(() => import('@/features/items/item-detail-page'), 'ItemDetailPage') },
              { path: 'claims', ...page(() => import('@/features/claims/claim-queue-page'), 'ClaimQueuePage') },
              { path: 'handovers', ...page(() => import('@/features/items/handover-desk-page'), 'HandoverDeskPage') },
              { path: 'admin/support', ...page(() => import('@/features/support/admin-support-page'), 'AdminSupportPage') },
              { path: 'admin/overview', ...page(() => import('@/features/admin/admin-overview-page'), 'AdminOverviewPage') },
            ],
          },
          {
            element: <ProtectedRoute allow={['Student']} />,
            children: [
              { path: 'my-reports', ...page(() => import('@/features/reports/my-reports-page'), 'MyReportsPage') },
              { path: 'my-reports/new', ...page(() => import('@/features/reports/report-lost-page'), 'ReportLostPage') },
              { path: 'my-claims', ...page(() => import('@/features/claims/my-claims-page'), 'MyClaimsPage') },
              { path: 'ask-foundu', ...page(() => import('@/features/intake/ask-foundu-page'), 'AskFoundUPage') },
              { path: 'help-to-find', ...page(() => import('@/features/help/help-to-find-page'), 'HelpToFindPage') },
            ],
          },

          // Both sides read the same claim from opposite ends, and the API decides who may
          // see which - so this route is open to any signed-in user rather than duplicated.
          { path: 'account', ...page(() => import('@/features/account/account-page'), 'AccountPage') },
          { path: 'support', ...page(() => import('@/features/support/support-page'), 'SupportPage') },
          { path: 'claims/:id', ...page(() => import('@/features/claims/claim-detail-page'), 'ClaimDetailPage') },

          // Anyone signed in can post something they found - a staff member walking across
          // campus is a finder too.
          { path: 'found/new', ...page(() => import('@/features/feed/post-found-page'), 'PostFoundPage') },
          {
            element: <ProtectedRoute allow={['Admin']} />,
            children: [
              { path: 'admin', ...page(() => import('@/features/admin/admin-users-page'), 'AdminUsersPage') },
              { path: 'admin/reference', ...page(() => import('@/features/admin/reference-page'), 'ReferencePage') },
              { path: 'admin/analytics', ...page(() => import('@/features/admin/analytics-page'), 'AnalyticsPage') },
              { path: 'admin/moderation', ...page(() => import('@/features/admin/moderation-page'), 'ModerationPage') },
            ],
          },

          { path: 'forbidden', ...page(() => import('@/pages/forbidden-page'), 'ForbiddenPage') },
        ],
      },
    ],
  },

  { path: '*', ...page(() => import('@/pages/not-found-page'), 'NotFoundPage') },
])
