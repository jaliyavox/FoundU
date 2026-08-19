import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '@/components/layout/app-layout'
import { LoginPage } from '@/features/auth/login-page'
import { FeedPage } from '@/features/feed/feed-page'
import { RegisterPage } from '@/features/auth/register-page'
import { ProtectedRoute } from './protected-route'
import { AdminPage } from '@/pages/admin-page'
import { ItemsPage } from '@/pages/items-page'
import { LandingPage } from '@/pages/landing-page'
import { MyReportsPage } from '@/pages/my-reports-page'
import { ForbiddenPage } from '@/pages/forbidden-page'
import { NotFoundPage } from '@/pages/not-found-page'

export const router = createBrowserRouter([
  // Public. Signed-in visitors are redirected to their role's home from inside the page.
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  { path: '/feed', element: <FeedPage /> },

  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          {
            element: <ProtectedRoute allow={['Staff', 'Admin']} />,
            children: [{ path: 'items', element: <ItemsPage /> }],
          },
          {
            element: <ProtectedRoute allow={['Student']} />,
            children: [{ path: 'my-reports', element: <MyReportsPage /> }],
          },
          {
            element: <ProtectedRoute allow={['Admin']} />,
            children: [{ path: 'admin', element: <AdminPage /> }],
          },

          { path: 'forbidden', element: <ForbiddenPage /> },
        ],
      },
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
