import { useMemo } from 'react'
import { Link as RouterLink } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Box, Card, CardContent, CardHeader, Grid, LinearProgress, Link, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import { dashboardApi, queryKeys } from '../../api/endpoints'
import type { RecentLeave, RecentTask, TaskStats } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { EmptyState, ErrorState, LeaveStatusChip, PageHeader, PriorityChip, StatCard, TableSkeletonRows, TaskStatusChip } from '../../components/Common'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { formatDate, statusLabel } from '../../utils/format'

export default function DashboardPage() {
  const { isAdmin } = useAuth()
  return isAdmin ? <AdminDashboard /> : <EmployeeDashboard />
}

function AdminDashboard() {
  useDocumentTitle('Dashboard')
  const { data, isLoading, error, refetch } = useQuery({ queryKey: queryKeys.dashboard('admin'), queryFn: dashboardApi.admin })
  return (
    <>
      <PageHeader title="Overview" subtitle="Organisation-wide tasks, people and leave at a glance." />
      {error && <Box sx={{ mb: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Active employees" value={data?.activeEmployees ?? 0} hint={data && `${data.totalEmployees} total`} loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Open tasks" value={data?.tasks.open ?? 0} hint={data && `${data.tasks.overdue} overdue`} loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Pending leave requests" value={data?.leaves.pending ?? 0} hint="Awaiting review" loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="On leave today" value={data?.employeesOnLeaveToday ?? 0} hint={data && `${data.leaves.approved} approved overall`} loading={isLoading} /></Grid>
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 4 }}><TaskBreakdown stats={data?.tasks} loading={isLoading} /></Grid>
        <Grid size={{ xs: 12, lg: 8 }}><RecentTasksCard title="Recently created tasks" tasks={data?.recentTasks} loading={isLoading} link="/tasks" showAssignee /></Grid>
        <Grid size={12}><RecentLeavesCard leaves={data?.recentLeaves} loading={isLoading} link="/leaves/manage" showEmployee /></Grid>
      </Grid>
    </>
  )
}

function EmployeeDashboard() {
  useDocumentTitle('Dashboard')
  const { user } = useAuth()
  const { data, isLoading, error, refetch } = useQuery({ queryKey: queryKeys.dashboard('employee'), queryFn: dashboardApi.mine })
  const completionRate = useMemo(() => {
    if (!data) return 0
    const relevant = data.tasks.total - data.tasks.cancelled
    return relevant === 0 ? 0 : Math.round((data.tasks.completed / relevant) * 100)
  }, [data])

  return (
    <>
      <PageHeader title={`Welcome back, ${user?.firstName}`} subtitle="Your work and leave summary." />
      {error && <Box sx={{ mb: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Open tasks" value={data?.tasks.open ?? 0} hint={data && `${data.tasks.inProgress} in progress`} loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Overdue" value={data?.tasks.overdue ?? 0} hint="Past due date" loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Completion rate" value={`${completionRate}%`} hint={data && `${data.tasks.completed} completed`} loading={isLoading} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard label="Leave days taken" value={data?.approvedLeaveDaysThisYear ?? 0} hint={`Approved in ${new Date().getFullYear()}`} loading={isLoading} /></Grid>
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 7 }}><RecentTasksCard title="Upcoming deadlines" tasks={data?.upcomingTasks} loading={isLoading} link="/my-tasks" /></Grid>
        <Grid size={{ xs: 12, lg: 5 }}><RecentLeavesCard leaves={data?.recentLeaves} loading={isLoading} link="/leaves" /></Grid>
      </Grid>
    </>
  )
}

function TaskBreakdown({ stats, loading }: { stats?: TaskStats; loading: boolean }) {
  const rows = useMemo(() => {
    if (!stats) return []
    return (['pending', 'inProgress', 'completed', 'cancelled'] as const).map((key) => ({
      key, label: statusLabel(key === 'inProgress' ? 'InProgress' : (key[0].toUpperCase() + key.slice(1)) as never),
      count: stats[key], pct: stats.total ? (stats[key] / stats.total) * 100 : 0,
    }))
  }, [stats])

  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardHeader title="Tasks by status" slotProps={{ title: { variant: 'subtitle1' } }} subheader={stats && `${stats.total} total`} />
      <CardContent>
        {loading ? <LinearProgress /> : (
          <Stack spacing={2}>
            {rows.map((r) => (
              <Box key={r.key}>
                <Stack direction="row" sx={{ justifyContent: 'space-between', mb: 0.5 }}>
                  <Typography variant="body2">{r.label}</Typography>
                  <Typography variant="body2" color="text.secondary">{r.count}</Typography>
                </Stack>
                <LinearProgress variant="determinate" value={r.pct} sx={{ height: 6, borderRadius: 3 }} />
              </Box>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  )
}

function RecentTasksCard({ title, tasks, loading, link, showAssignee }: { title: string; tasks?: RecentTask[]; loading: boolean; link: string; showAssignee?: boolean }) {
  const columns = showAssignee ? 5 : 4
  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardHeader title={title} slotProps={{ title: { variant: 'subtitle1' } }} action={<Link component={RouterLink} to={link} variant="body2">View all</Link>} />
      <Box sx={{ overflowX: 'auto' }}>
        <Table size="small">
          <TableHead><TableRow><TableCell>Task</TableCell>{showAssignee && <TableCell>Assignee</TableCell>}<TableCell>Priority</TableCell><TableCell>Status</TableCell><TableCell>Due</TableCell></TableRow></TableHead>
          <TableBody>
            {loading && <TableSkeletonRows rows={4} columns={columns} />}
            {tasks?.length === 0 && <TableRow><TableCell colSpan={columns}><EmptyState title="No tasks yet" /></TableCell></TableRow>}
            {tasks?.map((t) => (
              <TableRow key={t.id} hover>
                <TableCell><Link component={RouterLink} to={`/tasks/${t.id}`} underline="hover">{t.title}</Link></TableCell>
                {showAssignee && <TableCell>{t.assigneeName ?? <Typography variant="body2" color="text.secondary">Unassigned</Typography>}</TableCell>}
                <TableCell><PriorityChip priority={t.priority} /></TableCell>
                <TableCell><TaskStatusChip status={t.status} /></TableCell>
                <TableCell sx={{ color: t.isOverdue ? 'error.main' : undefined, whiteSpace: 'nowrap' }}>{formatDate(t.dueDate)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </Card>
  )
}

function RecentLeavesCard({ leaves, loading, link, showEmployee }: { leaves?: RecentLeave[]; loading: boolean; link: string; showEmployee?: boolean }) {
  const columns = showEmployee ? 4 : 3
  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardHeader title="Recent leave requests" slotProps={{ title: { variant: 'subtitle1' } }} action={<Link component={RouterLink} to={link} variant="body2">View all</Link>} />
      <Box sx={{ overflowX: 'auto' }}>
        <Table size="small">
          <TableHead><TableRow>{showEmployee && <TableCell>Employee</TableCell>}<TableCell>Type</TableCell><TableCell>Dates</TableCell><TableCell>Status</TableCell></TableRow></TableHead>
          <TableBody>
            {loading && <TableSkeletonRows rows={3} columns={columns} />}
            {leaves?.length === 0 && <TableRow><TableCell colSpan={columns}><EmptyState title="No leave requests" /></TableCell></TableRow>}
            {leaves?.map((l) => (
              <TableRow key={l.id} hover>
                {showEmployee && <TableCell>{l.employeeName}</TableCell>}
                <TableCell>{l.leaveType}</TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(l.startDate)} – {formatDate(l.endDate)}</TableCell>
                <TableCell><LeaveStatusChip status={l.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </Card>
  )
}
