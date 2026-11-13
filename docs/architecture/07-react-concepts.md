# React Concepts — where and why

| Concept | Where | Why |
|---|---|---|
| useState | dialogs, filters, SearchField input, NotificationBell open state | local UI state |
| useEffect | `AuthContext` bootstrap + 401 event listener (cleanup), `useDebounce` timer cleanup, `SearchField` "/" shortcut listener, form `reset` when dialogs open, `useDocumentTitle` | side effects with cleanup |
| useMemo | `useListParams` reducer, `AuthContext` value, dashboard completion rate and status breakdown | stable context value; derived data |
| useCallback | `onSort`/`onSearch`/`onOpen`/`onToggle`/`onReview` passed to memoised rows; `signIn`/`signOut` | keeps props stable so `React.memo` rows skip re-rendering |
| useRef | `SearchField` focus via "/", Login password refocus after failure, NotificationBell popover anchor, latest-callback ref | DOM access without re-renders |
| useContext | `AuthContext` (user, role, sign-in/out), `NotifyProvider` (toasts) | app-wide state |
| useReducer | `AuthContext` (loading → authenticated/anonymous), `useListParams` (search/filter/sort/page all reset page to 1, sort toggles direction) | coupled state transitions in one place |
| Custom hooks | `useDebounce`, `useListParams`, `useDocumentTitle`, `useNotify`, `useAuth`, `useUpdateTaskStatus`, `useDeleteTask` | reusable logic |
| React.memo | `TaskRow`, `LeaveRow`, `EmployeeRow`, status/priority chips | the page re-renders on every search keystroke; rows don't need to |
| Conditional rendering | role-based navigation and routes (`RequireRole`), loading/empty/error states, allowed-transition buttons | |
| React.lazy + Suspense | `App.tsx` (each page is its own chunk), `AppShell` Suspense fallback | smaller initial bundle (see `npm run build` output) |
| Error boundary | `components/ErrorBoundary.tsx`, global + per-route (resets on navigation) | |
| Debouncing | `SearchField` + `useDebounce` (350ms) | one API call per pause; pairs with the server `search` rate limit |
| Keys and reconciliation | rows keyed by entity id; `keepPreviousData` while paging | no row remounts or flicker |

Server state is handled by TanStack Query: centralised `queryKeys`, 30s `staleTime`, retry only for network/5xx errors. Optimistic updates cover task status (rolled back on 409), employee activation and mark-notification-read. Notifications poll every 30s.
