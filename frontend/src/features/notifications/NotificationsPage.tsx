import { useState } from 'react'
import { useNavigate } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Card, List, ListItemButton, ListItemText, Pagination, Stack, Tab, Tabs, Typography } from '@mui/material'
import { notificationsApi, queryKeys } from '../../api/endpoints'
import type { AppNotification } from '../../api/types'
import { EmptyState, ErrorState, PageHeader } from '../../components/Common'
import { useDocumentTitle } from '../../hooks/useDocumentTitle'
import { formatDateTime } from '../../utils/format'

export default function NotificationsPage() {
  useDocumentTitle('Notifications')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [unreadOnly, setUnreadOnly] = useState(false)
  const [page, setPage] = useState(1)
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: [...queryKeys.notifications, 'page', page, unreadOnly],
    queryFn: () => notificationsApi.list(page, unreadOnly),
    placeholderData: keepPreviousData,
  })
  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.notifications })
  const markRead = useMutation({ mutationFn: notificationsApi.markRead, onSettled: invalidate })
  const markAll = useMutation({ mutationFn: notificationsApi.markAllRead, onSettled: invalidate })

  const open = (n: AppNotification) => { if (!n.isRead) markRead.mutate(n.id); if (n.link) navigate(n.link) }

  return (
    <>
      <PageHeader title="Notifications" subtitle="Updates on task assignments, status changes and leave decisions."
        actions={<Button onClick={() => markAll.mutate()} disabled={markAll.isPending}>Mark all as read</Button>} />
      <Card variant="outlined">
        <Tabs value={unreadOnly ? 1 : 0} onChange={(_, v) => { setUnreadOnly(v === 1); setPage(1) }} sx={{ px: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Tab label="All" /><Tab label="Unread" />
        </Tabs>
        {error && <Box sx={{ p: 2 }}><ErrorState error={error} onRetry={refetch} /></Box>}
        {isLoading && <Typography sx={{ p: 2 }} color="text.secondary">Loading…</Typography>}
        {data?.items.length === 0 && <EmptyState title={unreadOnly ? 'No unread notifications' : 'No notifications yet'} />}
        <List disablePadding>
          {data?.items.map((n) => (
            <ListItemButton key={n.id} divider onClick={() => open(n)} sx={{ bgcolor: n.isRead ? undefined : 'action.hover' }}>
              <ListItemText
                primary={<Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}>
                  <Typography variant="body2" sx={{ fontWeight: n.isRead ? 400 : 600 }}>{n.title}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(n.createdAt)}</Typography>
                </Stack>}
                secondary={n.message} />
            </ListItemButton>
          ))}
        </List>
        {data && data.totalPages > 1 && <Stack sx={{ p: 2, alignItems: 'center' }}><Pagination count={data.totalPages} page={page} onChange={(_, p) => setPage(p)} /></Stack>}
      </Card>
    </>
  )
}
