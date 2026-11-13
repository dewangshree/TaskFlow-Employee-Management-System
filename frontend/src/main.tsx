import { StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CssBaseline, ThemeProvider } from '@mui/material'
import App from './App'
import { AuthProvider } from './auth/AuthContext'
import { FullPageSpinner } from './auth/Guards'
import { ErrorBoundary } from './components/ErrorBoundary'
import { NotifyProvider } from './hooks/useNotify'
import { theme } from './theme'
import { toApiError } from './api/client'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      // Don't retry client errors (401/403/404/409); retry transient failures twice.
      retry: (failureCount, error) => { const s = toApiError(error).status; return (s === 0 || s >= 500) && failureCount < 2 },
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <ErrorBoundary>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <NotifyProvider>
              <AuthProvider>
                <Suspense fallback={<FullPageSpinner />}><App /></Suspense>
              </AuthProvider>
            </NotifyProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </ErrorBoundary>
    </ThemeProvider>
  </StrictMode>,
)
