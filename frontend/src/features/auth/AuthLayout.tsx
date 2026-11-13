import type { ReactNode } from 'react'
import { Box, Card, CardContent, Typography } from '@mui/material'

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', bgcolor: 'background.default', p: 2 }}>
      <Box sx={{ width: '100%', maxWidth: 420 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 3, justifyContent: 'center' }}>
          <Box sx={{ width: 32, height: 32, borderRadius: 1, bgcolor: 'primary.main', color: 'primary.contrastText', display: 'grid', placeItems: 'center', fontWeight: 700 }}>T</Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>TaskFlow</Typography>
        </Box>
        <Card variant="outlined">
          <CardContent sx={{ p: 4 }}>
            <Typography variant="h5" component="h1">{title}</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 3 }}>{subtitle}</Typography>
            {children}
          </CardContent>
        </Card>
      </Box>
    </Box>
  )
}
