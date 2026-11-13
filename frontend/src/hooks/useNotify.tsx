import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { Alert, Snackbar } from '@mui/material'

type Severity = 'success' | 'error' | 'info' | 'warning'
const NotifyContext = createContext<(message: string, severity?: Severity) => void>(() => {})

export function NotifyProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<{ message: string; severity: Severity; key: number } | null>(null)
  const notify = useCallback((message: string, severity: Severity = 'success') => setToast({ message, severity, key: Date.now() }), [])
  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <Snackbar key={toast?.key} open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        {toast ? <Alert severity={toast.severity} variant="filled" onClose={() => setToast(null)}>{toast.message}</Alert> : undefined}
      </Snackbar>
    </NotifyContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useNotify = () => useContext(NotifyContext)
