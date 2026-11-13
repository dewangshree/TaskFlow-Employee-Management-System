import { memo, useCallback, useState } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Box, Button, Card, Chip, FormControlLabel, Link, MenuItem, Stack, Switch, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { queryKeys, tasksApi } from '../../api/endpoints'
import { TASK_PRIORITIES, TASK_STATUSES, type TaskListItem } from '../../api/types'
import { EmptyState, ErrorState, PageHeader, PriorityChip, TableMessageRow, TableSkeletonRows, TaskStatusChip } from '../../components/Common'
import { SearchField } from '../../components/SearchField'
import { SortableHeader } from '../../components/SortableHeader'
import { useListParams } from '../../hooks/useListParams'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { formatDate, statusLabel } from '../../utils/format'
import { TaskFormDialog } from './TaskFormDialog'

/**
 * Rows are memoised: typing in the search box re-renders this page on every keystroke,
 * but rows only re-render when their own task object changes. Stable keys (task id)
 * let React reconcile pages without remounting rows.
 */
const TaskRow = memo(function TaskRow({ task, showAssignee, onOpen }: { task: TaskListItem; showAssignee: boolean; onOpen: (id: number) => void }) {
  return (
    <TableRow hover sx={{ cursor: 'pointer' }} onClick={() => onOpen(task.id)}>
      <TableCell sx={{ maxWidth: 360 }}>
        <Link component={RouterLink} to={`/tasks/${task.id}`} underline="hover" onClick={(e) => e.stopPropagation()} sx={{ fontWeight: 500 }}>{task.title}</Link>
      </TableCell>
      {showAssignee && <TableCell>{task.assigneeName ?? <Typography variant="body2" color="text.secondary">Unassigned</Typography>}</TableCell>}
      <TableCell><PriorityChip priority={task.priority} /></TableCell>
      <TableCell><TaskStatusChip status={task.status} /></TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap', color: task.isOverdue ? 'error.main' : undefined }}>
        {formatDate(task.dueDate)}{task.isOverdue && <Chip size="small" label="Overdue" color="error" variant="outlined" sx={{ ml: 1 }} />}
      </TableCell>
    </TableRow>
  )
})

export default function TaskListPage({ scope }: { scope: 'all' | 'mine' }) {
  const isAdminView = scope === 'all'
  useDocumentTitle(isAdminView ? 'Tasks' : 'My tasks')
  const navigate = useNavigate()
  const [createOpen, setCreateOpen] = useState(false)
  const { params, dispatch, activeFilterCount } = useListParams({ sortBy: isAdminView ? 'createdAt' : 'dueDate', sortDirection: isAdminView ? 'Desc' : 'Asc' })

  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: queryKeys.taskList(scope, params),
    queryFn: () => (isAdminView ? tasksApi.list(params) : tasksApi.mine(params)),
    placeholderData: keepPreviousData, // keep showing the current page while the next one loads
  })

  const onOpen = useCallback((id: number) => navigate(`/tasks/${id}`), [navigate])
  const onSort = useCallback((field: string) => dispatch({ type: 'sort', field }), [dispatch])
  const onSearch = useCallback((value: string) => dispatch({ type: 'search', value }), [dispatch])
  const columns = isAdminView ? 5 : 4

  return (
    <>
      <PageHeader
        title={isAdminView ? 'Tasks' : 'My tasks'}
        subtitle={isAdminView ? 'Create, assign and track work across the organisation.' : 'Tasks assigned to you. Open a task to update its status.'}
        actions={isAdminView && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>New task</Button>}
      />
      <Card variant="outlined">
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ p: 2, alignItems: { md: 'center' } }}>
          <SearchField onSearch={onSearch} placeholder={isAdminView ? 'Search title, description or assignee  ( / )' : 'Search tasks  ( / )'} />
          <TextField select size="small" label="Status" value={params.status ?? ''} sx={{ minWidth: 150 }}
            onChange={(e) => dispatch({ type: 'filter', key: 'status', value: e.target.value })}>
            <MenuItem value="">All statuses</MenuItem>
            {TASK_STATUSES.map((s) => <MenuItem key={s} value={s}>{statusLabel(s)}</MenuItem>)}
          </TextField>
          <TextField select size="small" label="Priority" value={params.priority ?? ''} sx={{ minWidth: 150 }}
            onChange={(e) => dispatch({ type: 'filter', key: 'priority', value: e.target.value })}>
            <MenuItem value="">All priorities</MenuItem>
            {TASK_PRIORITIES.map((p) => <MenuItem key={p} value={p}>{p}</MenuItem>)}
          </TextField>
          <FormControlLabel control={<Switch size="small" checked={params.overdueOnly === true}
            onChange={(e) => dispatch({ type: 'filter', key: 'overdueOnly', value: e.target.checked || undefined })} />} label="Overdue only" />
          {activeFilterCount > 0 && <Button size="small" onClick={() => dispatch({ type: 'reset' })}>Clear filters</Button>}
        </Stack>
        {error && <Box sx={{ px: 2, pb: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
        <TableContainer sx={{ opacity: isFetching && !isLoading ? 0.6 : 1, transition: 'opacity 120ms' }}>
          <Table>
            <TableHead>
              <TableRow>
                <SortableHeader field="title" label="Title" params={params} onSort={onSort} />
                {isAdminView && <SortableHeader field="assignee" label="Assignee" params={params} onSort={onSort} />}
                <SortableHeader field="priority" label="Priority" params={params} onSort={onSort} />
                <SortableHeader field="status" label="Status" params={params} onSort={onSort} />
                <SortableHeader field="dueDate" label="Due date" params={params} onSort={onSort} />
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading && <TableSkeletonRows columns={columns} rows={params.pageSize} />}
              {data?.items.length === 0 && (
                <TableMessageRow columns={columns}>
                  <EmptyState
                    title={params.search || activeFilterCount ? 'No tasks match your filters' : isAdminView ? 'No tasks yet' : 'Nothing assigned to you'}
                    description={params.search || activeFilterCount ? 'Try a different search or clear filters.' : undefined}
                    action={isAdminView && !params.search && !activeFilterCount ? <Button onClick={() => setCreateOpen(true)}>Create the first task</Button> : undefined}
                  />
                </TableMessageRow>
              )}
              {data?.items.map((task) => <TaskRow key={task.id} task={task} showAssignee={isAdminView} onOpen={onOpen} />)}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination component="div" count={data?.totalCount ?? 0} page={(data?.page ?? params.page) - 1} rowsPerPage={params.pageSize}
          rowsPerPageOptions={[10, 20, 50]} onPageChange={(_, page) => dispatch({ type: 'page', page: page + 1 })}
          onRowsPerPageChange={(e) => dispatch({ type: 'pageSize', pageSize: Number(e.target.value) })} />
      </Card>
      {isAdminView && <TaskFormDialog open={createOpen} onClose={() => setCreateOpen(false)} />}
    </>
  )
}
