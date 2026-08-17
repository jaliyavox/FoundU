import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '@/components/layout/app-layout'
import { LoginPage } from '@/features/auth/login-page'
import { ProtectedRoute } from './protected-route'
import { AdminPage } from '@/pages/admin-page'
import { ItemsPage } from '@/pages/items-page'
import { MyReportsPage } from '@/pages/my-reports-page'
import { ForbiddenPage } from '@/pages/forbidden-page'
import { NotFoundPage } from '@/pages/not-found-page'
import { RoleHomeRedirect } from './role-home-redirect'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          // "/" is role-dependent, so it redirects rather than rendering anything itself.
          { index: true, element: <RoleHomeRedirect /> },

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
