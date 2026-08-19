import { useState, type FormEvent } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  Loader2Icon,
  RotateCwIcon,
  SearchIcon,
  ShieldCheckIcon,
  ShieldOffIcon,
  UsersIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { FormSelect } from '@/features/reports/form-select'
import { useAuth } from '@/features/auth/use-auth'
import { formatDate, getUsers, reinstateUser, type AdminUser } from './admin-api'
import { StatCards } from './stat-cards'
import { SuspendDialog } from './suspend-dialog'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 15

const ROLE_OPTIONS = [
  { value: 'all', label: 'All roles' },
  { value: 'Student', label: 'Student' },
  { value: 'Staff', label: 'Staff' },
  { value: 'Admin', label: 'Admin' },
]

const STATUS_OPTIONS = [
  { value: 'all', label: 'All accounts' },
  { value: 'active', label: 'Active only' },
  { value: 'suspended', label: 'Suspended only' },
]

export function AdminUsersPage() {
  const { user: currentUser } = useAuth()
  const queryClient = useQueryClient()

  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('all')
  const [status, setStatus] = useState('all')
  const [suspendTarget, setSuspendTarget] = useState<AdminUser | null>(null)

  const { data, isPending, isError, error, isFetching, refetch } = useQuery({
    queryKey: ['admin-users', { page, search, role, status }],
    queryFn: () =>
      getUsers({
        page,
        pageSize: PAGE_SIZE,
        search,
        role: role === 'all' ? undefined : role,
        isSuspended: status === 'all' ? undefined : status === 'suspended',
      }),
    placeholderData: keepPreviousData,
  })

  const reinstate = useMutation({
    mutationFn: reinstateUser,
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      queryClient.invalidateQueries({ queryKey: ['admin-user-stats'] })
      toast.success(`${updated.fullName} can sign in again.`)
    },
    onError: (mutationError) => {
      toast.error(
        mutationError instanceof ApiError ? mutationError.message : 'Could not reach the server.',
      )
    },
  })

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput)
  }

  function resetFilters() {
    setSearchInput('')
    setSearch('')
    setRole('all')
    setStatus('all')
    setPage(1)
  }

  const hasFilters = Boolean(search) || role !== 'all' || status !== 'all'

  return (
    <section className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          Every account on FoundU. Suspending one signs it out immediately and blocks sign-in
          until it is reinstated.
        </p>
      </div>

      <StatCards />

      {/* ------------------------------------------------------------- filters */}
      <form onSubmit={handleSearch} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex flex-1 flex-col gap-2">
          <Label htmlFor="user-search">Search</Label>
          <div className="relative">
            <SearchIcon
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <Input
              id="user-search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Name, email or student number"
              className="pl-9"
            />
          </div>
        </div>

        <div className="flex flex-col gap-2 sm:w-40">
          <Label htmlFor="role-filter">Role</Label>
          <FormSelect
            id="role-filter"
            value={role}
            onValueChange={(next) => {
              setRole(next)
              setPage(1)
            }}
            options={ROLE_OPTIONS}
            placeholder="All roles"
          />
        </div>

        <div className="flex flex-col gap-2 sm:w-44">
          <Label htmlFor="status-filter">Status</Label>
          <FormSelect
            id="status-filter"
            value={status}
            onValueChange={(next) => {
              setStatus(next)
              setPage(1)
            }}
            options={STATUS_OPTIONS}
            placeholder="All accounts"
          />
        </div>

        <Button type="submit" variant="outline">
          Search
        </Button>
      </form>

      {/* -------------------------------------------------------------- states */}
      {isPending ? (
        <Card>
          <CardContent className="flex flex-col gap-3 pt-6">
            {Array.from({ length: 6 }).map((_, index) => (
              <div key={index} className="flex items-center gap-4">
                <Skeleton className="h-4 flex-1" />
                <Skeleton className="h-4 w-48" />
                <Skeleton className="h-5 w-16 rounded-full" />
                <Skeleton className="h-8 w-24" />
              </div>
            ))}
          </CardContent>
        </Card>
      ) : isError ? (
        <Card role="alert" className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base">Could not load users</CardTitle>
            <CardDescription>
              {error instanceof ApiError ? error.message : 'Check the API is running.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="outline" onClick={() => refetch()}>
              <RotateCwIcon aria-hidden="true" />
              Try again
            </Button>
          </CardContent>
        </Card>
      ) : data.items.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-16 text-center">
            <span className="flex size-12 items-center justify-center rounded-2xl bg-muted">
              <UsersIcon className="size-5 text-muted-foreground" aria-hidden="true" />
            </span>
            <p className="text-base font-medium">
              {hasFilters ? 'No accounts match those filters' : 'No accounts yet'}
            </p>
            <p className="max-w-sm text-sm text-muted-foreground">
              {hasFilters
                ? 'Try a broader search, or clear the filters to see everyone.'
                : 'Accounts appear here as students register.'}
            </p>
            {hasFilters && (
              <Button variant="outline" className="mt-1" onClick={resetFilters}>
                Clear filters
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <>
          <Card className={cn('transition-opacity duration-200', isFetching && 'opacity-60')}>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Reports</TableHead>
                      <TableHead>Joined</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>

                  <TableBody>
                    {data.items.map((user) => {
                      // The API rejects both of these; disabling the control says so up front
                      // rather than letting an admin discover it through an error toast.
                      const isSelf = user.id === currentUser?.id
                      const isAdmin = user.role === 'Admin'
                      const cannotSuspend = isSelf || isAdmin

                      return (
                        <TableRow key={user.id}>
                          <TableCell>
                            <div className="flex flex-col leading-tight">
                              <span className="font-medium">
                                {user.fullName}
                                {isSelf && (
                                  <span className="pl-2 text-xs font-normal text-muted-foreground">
                                    you
                                  </span>
                                )}
                              </span>
                              <span className="text-xs text-muted-foreground">{user.email}</span>
                              {user.studentNumber && (
                                <span className="text-xs text-muted-foreground">
                                  {user.studentNumber}
                                </span>
                              )}
                            </div>
                          </TableCell>

                          <TableCell>
                            <Badge variant="secondary">{user.role}</Badge>
                          </TableCell>

                          <TableCell>
                            {user.isSuspended ? (
                              <div className="flex flex-col gap-0.5">
                                <Badge variant="secondary" className="w-fit bg-destructive/12 text-destructive">
                                  Suspended
                                </Badge>
                                <span className="max-w-56 truncate text-xs text-muted-foreground">
                                  {user.suspensionReason}
                                </span>
                                {user.suspendedByName && user.suspendedAt && (
                                  <span className="text-xs text-muted-foreground">
                                    by {user.suspendedByName}, {formatDate(user.suspendedAt)}
                                  </span>
                                )}
                              </div>
                            ) : (
                              <Badge
                                variant="secondary"
                                className="bg-brand-green/15 text-brand-forest dark:text-brand-sage"
                              >
                                Active
                              </Badge>
                            )}
                          </TableCell>

                          <TableCell className="text-right tabular-nums">
                            {user.lostReportCount}
                          </TableCell>

                          <TableCell className="text-sm text-muted-foreground">
                            {formatDate(user.createdAt)}
                          </TableCell>

                          <TableCell className="text-right">
                            {user.isSuspended ? (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => reinstate.mutate(user.id)}
                                disabled={reinstate.isPending && reinstate.variables === user.id}
                              >
                                {reinstate.isPending && reinstate.variables === user.id && (
                                  <Loader2Icon className="animate-spin" aria-hidden="true" />
                                )}
                                <ShieldCheckIcon aria-hidden="true" />
                                Reinstate
                              </Button>
                            ) : (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={cannotSuspend}
                                title={
                                  isSelf
                                    ? 'You cannot suspend your own account'
                                    : isAdmin
                                      ? 'Administrator accounts cannot be suspended'
                                      : undefined
                                }
                                onClick={() => setSuspendTarget(user)}
                              >
                                <ShieldOffIcon aria-hidden="true" />
                                Suspend
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          <nav aria-label="User pages" className="flex items-center justify-between gap-4">
            <Button
              variant="outline"
              disabled={!data.hasPreviousPage}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeftIcon aria-hidden="true" />
              Previous
            </Button>
            <span className="text-xs text-muted-foreground tabular-nums">
              {data.totalCount} {data.totalCount === 1 ? 'account' : 'accounts'} · page {data.page}{' '}
              of {data.totalPages}
            </span>
            <Button
              variant="outline"
              disabled={!data.hasNextPage}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </nav>
        </>
      )}

      <SuspendDialog user={suspendTarget} onClose={() => setSuspendTarget(null)} />
    </section>
  )
}
