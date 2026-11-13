import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Badge, Box, Button, Divider, IconButton, List, ListItemButton, ListItemText, Popover, Tooltip, Typography } from '@mui/material'
import NotificationsOutlined from '@mui/icons-material/NotificationsOutlined'
import { notificationsApi, queryKeys } from '../api/endpoints'
import type { AppNotification, PagedResult } from '../api/types'
import { relativeTime } from '../utils/format'

export function NotificationBell() {
  const anchorRef = useRef<HTMLButtonElement>(null)
  const [open, setOpen] = useState(false)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // Polling every 30s (only while the tab is visible). The endpoint is Redis-cached per user.
  const { data: unread } = useQuery({ queryKey: queryKeys.unreadCount, queryFn: notificationsApi.unreadCount, refetchInterval: 30_000 })
  const { data: recent, isLoading } = useQuery({
    queryKey: [...queryKeys.notifications, 'recent'],
    queryFn: () => notificationsApi.list(1, false),
    enabled: open,
  })

  // Optimistic: the item turns "read" and the badge decrements before the server responds.
  const markRead = useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.notifications })
      const previous = queryClient.getQueryData<PagedResult<AppNotification>>([...queryKeys.notifications, 'recent'])
      queryClient.setQueryData<PagedResult<AppNotification>>([...queryKeys.notifications, 'recent'], (old) =>
        old && { ...old, items: old.items.map((n) => (n.id === id ? { ...n, isRead: true } : n)) })
      queryClient.setQueryData<{ count: number }>(queryKeys.unreadCount, (old) => old && { count: Math.max(0, old.count - 1) })
      return { previous }
    },
    onError: (_e, _id, ctx) => queryClient.setQueryData([...queryKeys.notifications, 'recent'], ctx?.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.notifications }),
  })

  const markAll = useMutation({
    mutationFn: notificationsApi.markAllRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.notifications }),
  })

  const openItem = (n: AppNotification) => {
    if (!n.isRead) markRead.mutate(n.id)
    setOpen(false)
    if (n.link) navigate(n.link)
  }

  return (
    <>
      <Tooltip title="Notifications">
        <IconButton ref={anchorRef} onClick={() => setOpen(true)} aria-label={`${unread?.count ?? 0} unread notifications`}>
          <Badge badgeContent={unread?.count ?? 0} color="error" max={99}><NotificationsOutlined /></Badge>
        </IconButton>
      </Tooltip>
      <Popover open={open} anchorEl={anchorRef.current} onClose={() => setOpen(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 380, maxWidth: 'calc(100vw - 32px)' } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 2, py: 1.5 }}>
          <Typography variant="subtitle2">Notifications</Typography>
          <Button size="small" disabled={!unread?.count || markAll.isPending} onClick={() => markAll.mutate()}>Mark all read</Button>
        </Box>
        <Divider />
        <List dense disablePadding sx={{ maxHeight: 400, overflow: 'auto' }}>
          {isLoading && <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>Loading…</Typography>}
          {recent?.items.length === 0 && <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>You're all caught up.</Typography>}
          {recent?.items.slice(0, 8).map((n) => (
            <ListItemButton key={n.id} onClick={() => openItem(n)} sx={{ alignItems: 'flex-start', bgcolor: n.isRead ? undefined : 'action.hover' }}>
              <ListItemText
                primary={<Typography variant="body2" sx={{ fontWeight: n.isRead ? 400 : 600 }}>{n.title}</Typography>}
                secondary={<><Typography variant="caption" component="span" sx={{ display: 'block' }}>{n.message}</Typography>
                  <Typography variant="caption" color="text.disabled">{relativeTime(n.createdAt)}</Typography></>} />
            </ListItemButton>
          ))}
        </List>
        <Divider />
        <Button fullWidth onClick={() => { setOpen(false); navigate('/notifications') }}>View all</Button>
      </Popover>
    </>
  )
}
