import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Alert, Box, Button } from '@mui/material'

interface Props { children: ReactNode; resetKey?: string }
interface State { error: Error | null }

/** Catches render-time errors so one broken page doesn't blank the whole app. Resets on navigation. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State { return { error } }

  componentDidCatch(error: Error, info: ErrorInfo) { console.error('Render error', error, info.componentStack) }

  componentDidUpdate(prev: Props) {
    if (this.state.error && prev.resetKey !== this.props.resetKey) this.setState({ error: null })
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error" action={<Button color="inherit" size="small" onClick={() => this.setState({ error: null })}>Try again</Button>}>
          Something went wrong while displaying this page.
        </Alert>
      </Box>
    )
  }
}
