import { Suspense, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import {
  AppBar, Avatar, Box, Divider, Drawer, IconButton, LinearProgress, List, ListItemButton, ListItemIcon, ListItemText,
  Menu, MenuItem, Toolbar, Typography, useMediaQuery, useTheme,
} from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'
import DashboardOutlined from '@mui/icons-material/DashboardOutlined'
import AssignmentOutlined from '@mui/icons-material/AssignmentOutlined'
import EventNoteOutlined from '@mui/icons-material/EventNoteOutlined'
import PeopleOutlined from '@mui/icons-material/PeopleOutlined'
import NotificationsOutlined from '@mui/icons-material/NotificationsOutlined'
import PersonOutlined from '@mui/icons-material/PersonOutlined'
import { useAuth } from '../auth/AuthContext'
import { NotificationBell } from './NotificationBell'
import { ErrorBoundary } from './ErrorBoundary'

const DRAWER_WIDTH = 240

const adminNav = [
  { to: '/', label: 'Dashboard', icon: <DashboardOutlined /> },
  { to: '/tasks', label: 'Tasks', icon: <AssignmentOutlined /> },
  { to: '/leaves/manage', label: 'Leave requests', icon: <EventNoteOutlined /> },
  { to: '/employees', label: 'Employees', icon: <PeopleOutlined /> },
]
const employeeNav = [
  { to: '/', label: 'Dashboard', icon: <DashboardOutlined /> },
  { to: '/my-tasks', label: 'My tasks', icon: <AssignmentOutlined /> },
  { to: '/leaves', label: 'My leave', icon: <EventNoteOutlined /> },
]
const sharedNav = [
  { to: '/notifications', label: 'Notifications', icon: <NotificationsOutlined /> },
  { to: '/profile', label: 'Profile', icon: <PersonOutlined /> },
]

export function AppShell() {
  const { user, isAdmin, signOut } = useAuth()
  const theme = useTheme()
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'))
  const [mobileOpen, setMobileOpen] = useState(false)
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const location = useLocation()

  const nav = isAdmin ? adminNav : employeeNav
  const initials = `${user?.firstName[0] ?? ''}${user?.lastName[0] ?? ''}`

  const drawer = (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Toolbar sx={{ gap: 1 }}>
        <Box sx={{ width: 28, height: 28, borderRadius: 1, bgcolor: 'primary.main', color: 'primary.contrastText', display: 'grid', placeItems: 'center', fontWeight: 700, fontSize: 14 }}>T</Box>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>TaskFlow</Typography>
      </Toolbar>
      <Divider />
      <List sx={{ px: 1, py: 1.5 }}>
        {[...nav, ...sharedNav].map((item, i) => (
          <Box key={item.to}>
            {i === nav.length && <Divider sx={{ my: 1.5 }} />}
            <ListItemButton component={NavLink} to={item.to} end={item.to === '/'} onClick={() => setMobileOpen(false)}
              sx={{ borderRadius: 1, mb: 0.25, '&.active': { bgcolor: 'action.selected', '& .MuiListItemIcon-root, & .MuiTypography-root': { color: 'primary.main', fontWeight: 600 } } }}>
              <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} slotProps={{ primary: { variant: 'body2' } }} />
            </ListItemButton>
          </Box>
        ))}
      </List>
      <Box sx={{ mt: 'auto', p: 2 }}>
        <Typography variant="caption" color="text.secondary">Signed in as {user?.role.toLowerCase()}</Typography>
      </Box>
    </Box>
  )

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="fixed" color="inherit" elevation={0}
        sx={{ borderBottom: 1, borderColor: 'divider', width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, ml: { md: `${DRAWER_WIDTH}px` } }}>
        <Toolbar>
          {!isDesktop && <IconButton edge="start" onClick={() => setMobileOpen(true)} aria-label="Open navigation" sx={{ mr: 1 }}><MenuIcon /></IconButton>}
          <Box sx={{ flexGrow: 1 }} />
          <NotificationBell />
          <IconButton onClick={(e) => setMenuAnchor(e.currentTarget)} sx={{ ml: 1 }} aria-label="Account menu">
            <Avatar sx={{ width: 32, height: 32, fontSize: 14, bgcolor: 'primary.main' }}>{initials}</Avatar>
          </IconButton>
          <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
            <Box sx={{ px: 2, py: 1 }}>
              <Typography variant="subtitle2">{user?.fullName}</Typography>
              <Typography variant="caption" color="text.secondary">{user?.email}</Typography>
            </Box>
            <Divider />
            <MenuItem component={NavLink} to="/profile" onClick={() => setMenuAnchor(null)}>Profile</MenuItem>
            <MenuItem onClick={() => { setMenuAnchor(null); void signOut() }}>Sign out</MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer variant={isDesktop ? 'permanent' : 'temporary'} open={isDesktop || mobileOpen} onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }} sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}>
          {drawer}
        </Drawer>
      </Box>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, md: 4 }, mt: 8 }}>
        <ErrorBoundary resetKey={location.pathname}>
          {/* Route-level code splitting: each page chunk loads on first visit. */}
          <Suspense fallback={<LinearProgress sx={{ position: 'fixed', top: 0, left: 0, right: 0, zIndex: 2000 }} />}>
            <Outlet />
          </Suspense>
        </ErrorBoundary>
      </Box>
    </Box>
  )
}
