import { createTheme } from '@mui/material/styles'

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#2563eb' },
    background: { default: '#f6f7f9', paper: '#ffffff' },
    divider: '#e5e7eb',
    text: { primary: '#111827', secondary: '#6b7280' },
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: 'Inter, system-ui, -apple-system, Segoe UI, Roboto, sans-serif',
    h4: { fontWeight: 600 },
    h5: { fontWeight: 600, fontSize: '1.375rem' },
    subtitle1: { fontWeight: 600 },
    button: { textTransform: 'none', fontWeight: 500 },
  },
  components: {
    MuiCard: { defaultProps: { variant: 'outlined' } },
    MuiTableCell: { styleOverrides: { head: { fontWeight: 600, color: '#374151', backgroundColor: '#f9fafb' } } },
    MuiButton: { defaultProps: { disableElevation: true } },
  },
})
