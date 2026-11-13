import { memo, useCallback, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Avatar, Box, Card, Chip, MenuItem, Stack, Switch, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Tooltip, Typography } from '@mui/material'
import { employeesApi, queryKeys } from '../../api/endpoints'
import { toApiError } from '../../api/client'
import type { Employee, PagedResult } from '../../api/types'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState, ErrorState, PageHeader, TableMessageRow, TableSkeletonRows } from '../../components/Common'
import { SearchField } from '../../components/SearchField'
import { SortableHeader } from '../../components/SortableHeader'
import { useListParams } from '../../hooks/useListParams'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { useNotify } from '../../hooks/useNotify'
import { formatDate } from '../../utils/format'

const EmployeeRow = memo(function EmployeeRow({ employee, onToggle }: { employee: Employee; onToggle: (e: Employee) => void }) {
  return (
    <TableRow hover>
      <TableCell>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          <Avatar sx={{ width: 32, height: 32, fontSize: 13 }}>{employee.firstName[0]}{employee.lastName[0]}</Avatar>
          <Box><Typography variant="body2" sx={{ fontWeight: 500 }}>{employee.fullName}</Typography><Typography variant="caption" color="text.secondary">{employee.email}</Typography></Box>
        </Stack>
      </TableCell>
      <TableCell>{employee.department}</TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(employee.joinDate)}</TableCell>
      <TableCell align="right">{employee.openTasks}<Typography variant="caption" color="text.secondary"> / {employee.totalTasks}</Typography></TableCell>
      <TableCell><Chip size="small" label={employee.isActive ? 'Active' : 'Inactive'} color={employee.isActive ? 'success' : 'default'} variant="outlined" /></TableCell>
      <TableCell align="right">
        <Tooltip title={employee.isActive ? 'Deactivate' : 'Activate'}>
          <Switch checked={employee.isActive} onChange={() => onToggle(employee)} slotProps={{ input: { 'aria-label': `Toggle ${employee.fullName}` } }} />
        </Tooltip>
      </TableCell>
    </TableRow>
  )
})

export default function EmployeesPage() {
  useDocumentTitle('Employees')
  const notify = useNotify()
  const queryClient = useQueryClient()
  const [pending, setPending] = useState<Employee | null>(null)
  const { params, dispatch } = useListParams({ sortBy: 'name', sortDirection: 'Asc' })
  const { data: departments = [] } = useQuery({ queryKey: queryKeys.departments, queryFn: employeesApi.departments, staleTime: 5 * 60_000 })
  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: queryKeys.employeeList(params), queryFn: () => employeesApi.list(params), placeholderData: keepPreviousData,
  })

  // Optimistic toggle with rollback.
  const toggle = useMutation({
    mutationFn: (e: Employee) => employeesApi.setActive(e.id, !e.isActive),
    onMutate: async (e) => {
      const key = queryKeys.employeeList(params)
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<PagedResult<Employee>>(key)
      queryClient.setQueryData<PagedResult<Employee>>(key, (old) => old && { ...old, items: old.items.map((x) => (x.id === e.id ? { ...x, isActive: !x.isActive } : x)) })
      return { previous, key }
    },
    onError: (err, _e, ctx) => { if (ctx) queryClient.setQueryData(ctx.key, ctx.previous); notify(toApiError(err).message, 'error') },
    onSuccess: (updated) => notify(`${updated.fullName} ${updated.isActive ? 'activated' : 'deactivated'}`),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.employees })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })

  const onToggle = useCallback((e: Employee) => (e.isActive ? setPending(e) : toggle.mutate(e)), [toggle])
  const onSort = useCallback((field: string) => dispatch({ type: 'sort', field }), [dispatch])
  const onSearch = useCallback((value: string) => dispatch({ type: 'search', value }), [dispatch])

  return (
    <>
      <PageHeader title="Employees" subtitle="Directory of employee accounts. Deactivated employees cannot sign in or be assigned tasks." />
      <Card variant="outlined">
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ p: 2 }}>
          <SearchField onSearch={onSearch} placeholder="Search name, email or department  ( / )" />
          <TextField select size="small" label="Department" value={params.department ?? ''} sx={{ minWidth: 180 }} onChange={(e) => dispatch({ type: 'filter', key: 'department', value: e.target.value })}>
            <MenuItem value="">All departments</MenuItem>
            {departments.map((d) => <MenuItem key={d} value={d}>{d}</MenuItem>)}
          </TextField>
          <TextField select size="small" label="Status" value={params.isActive === undefined ? '' : String(params.isActive)} sx={{ minWidth: 140 }}
            onChange={(e) => dispatch({ type: 'filter', key: 'isActive', value: e.target.value === '' ? undefined : e.target.value === 'true' })}>
            <MenuItem value="">All</MenuItem><MenuItem value="true">Active</MenuItem><MenuItem value="false">Inactive</MenuItem>
          </TextField>
        </Stack>
        {error && <Box sx={{ px: 2, pb: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
        <TableContainer sx={{ opacity: isFetching && !isLoading ? 0.6 : 1 }}>
          <Table>
            <TableHead>
              <TableRow>
                <SortableHeader field="name" label="Employee" params={params} onSort={onSort} />
                <SortableHeader field="department" label="Department" params={params} onSort={onSort} />
                <SortableHeader field="joinDate" label="Joined" params={params} onSort={onSort} />
                <SortableHeader field="openTasks" label="Open / total tasks" params={params} onSort={onSort} align="right" />
                <TableCell>Status</TableCell>
                <TableCell align="right">Active</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading && <TableSkeletonRows columns={6} />}
              {data?.items.length === 0 && <TableMessageRow columns={6}><EmptyState title="No employees found" description="Try a different search or filter." /></TableMessageRow>}
              {data?.items.map((e) => <EmployeeRow key={e.id} employee={e} onToggle={onToggle} />)}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination component="div" count={data?.totalCount ?? 0} page={(data?.page ?? params.page) - 1} rowsPerPage={params.pageSize} rowsPerPageOptions={[10, 20, 50]}
          onPageChange={(_, p) => dispatch({ type: 'page', page: p + 1 })} onRowsPerPageChange={(e) => dispatch({ type: 'pageSize', pageSize: Number(e.target.value) })} />
      </Card>
      <ConfirmDialog open={!!pending} title="Deactivate employee?" confirmLabel="Deactivate" destructive
        message={`${pending?.fullName} will be signed out within a minute and will not be able to sign in or receive new tasks.`}
        onClose={() => setPending(null)} onConfirm={() => { if (pending) toggle.mutate(pending); setPending(null) }} />
    </>
  )
}
