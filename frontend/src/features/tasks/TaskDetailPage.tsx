import { useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Box, Breadcrumbs, Button, Card, CardContent, Divider, Grid, Link, Skeleton, Stack, Typography } from '@mui/material'
import { queryKeys, tasksApi } from '../../api/endpoints'
import type { TaskStatus } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { ErrorState, PriorityChip, TaskStatusChip } from '../../components/Common'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { formatDate, formatDateTime, statusLabel } from '../../utils/format'
import { TaskFormDialog } from './TaskFormDialog'
import { useDeleteTask, useUpdateTaskStatus } from './useTaskMutations'

export default function TaskDetailPage() {
  const id = Number(useParams().id)
  const { isAdmin } = useAuth()
  const navigate = useNavigate()
  const [editOpen, setEditOpen] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const { data: task, isLoading, error, refetch } = useQuery({ queryKey: queryKeys.task(id), queryFn: () => tasksApi.get(id), enabled: Number.isFinite(id) })
  const updateStatus = useUpdateTaskStatus()
  const deleteTask = useDeleteTask()
  useDocumentTitle(task?.title ?? 'Task')

  if (error) return <ErrorState error={error} onRetry={refetch} />

  const changeStatus = (status: TaskStatus) => task && updateStatus.mutate({ id: task.id, status, version: task.version })

  return (
    <>
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link component={RouterLink} to={isAdmin ? '/tasks' : '/my-tasks'} underline="hover" color="inherit">{isAdmin ? 'Tasks' : 'My tasks'}</Link>
        <Typography color="text.primary">Task #{id}</Typography>
      </Breadcrumbs>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 8 }}>
          <Card variant="outlined">
            <CardContent>
              {isLoading || !task ? <><Skeleton width="60%" height={36} /><Skeleton /><Skeleton width="80%" /></> : (
                <>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'flex-start' } }}>
                    <Typography variant="h5">{task.title}</Typography>
                    {isAdmin && (
                      <Stack direction="row" spacing={1}>
                        <Button variant="outlined" onClick={() => setEditOpen(true)}>Edit</Button>
                        <Button color="error" onClick={() => setConfirmDelete(true)}>Delete</Button>
                      </Stack>
                    )}
                  </Stack>
                  <Stack direction="row" spacing={1} sx={{ my: 2 }}><TaskStatusChip status={task.status} /><PriorityChip priority={task.priority} /></Stack>
                  <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }} color={task.description ? 'text.primary' : 'text.secondary'}>
                    {task.description || 'No description provided.'}
                  </Typography>
                  <Divider sx={{ my: 3 }} />
                  <Typography variant="subtitle2" gutterBottom>Update status</Typography>
                  {task.allowedTransitions.length === 0
                    ? <Typography variant="body2" color="text.secondary">No status changes are available to you for this task.</Typography>
                    : (
                      <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
                        {task.allowedTransitions.map((s) => (
                          <Button key={s} variant="outlined" size="small" disabled={updateStatus.isPending} onClick={() => changeStatus(s)}>
                            Move to {statusLabel(s)}
                          </Button>
                        ))}
                      </Stack>
                    )}
                </>
              )}
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={2}>
                <Detail label="Assignee" value={task?.assigneeName ?? 'Unassigned'} loading={isLoading} />
                <Detail label="Due date" value={task ? formatDate(task.dueDate) + (task.isOverdue ? ' (overdue)' : '') : ''} loading={isLoading} error={task?.isOverdue} />
                <Detail label="Created by" value={task?.createdByName ?? ''} loading={isLoading} />
                <Detail label="Created" value={task ? formatDateTime(task.createdAt) : ''} loading={isLoading} />
                {task?.updatedAt && <Detail label="Last updated" value={formatDateTime(task.updatedAt)} />}
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
      {isAdmin && task && <TaskFormDialog open={editOpen} task={task} onClose={() => setEditOpen(false)} />}
      <ConfirmDialog open={confirmDelete} title="Delete task?" message="This permanently removes the task. This cannot be undone." confirmLabel="Delete" destructive
        loading={deleteTask.isPending} onClose={() => setConfirmDelete(false)}
        onConfirm={() => deleteTask.mutate(id, { onSuccess: () => navigate('/tasks', { replace: true }) })} />
    </>
  )
}

function Detail({ label, value, loading, error }: { label: string; value: string; loading?: boolean; error?: boolean }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      {loading ? <Skeleton /> : <Typography variant="body2" color={error ? 'error.main' : undefined}>{value}</Typography>}
    </Box>
  )
}
