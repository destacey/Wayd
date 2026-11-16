import { configureStore } from '@reduxjs/toolkit'
import { setupListeners } from '@reduxjs/toolkit/query'
import teamReducer from './features/organizations/team-slice'
import roadmapTimelineSettingsReducer from './features/planning/roadmap-timeline-settings-slice'
import workProcessReducer from './features/work-management/work-process-slice'
import workspaceReducer from './features/work-management/workspace-slice'
import workTypeReducer from './features/work-management/work-type-slice'
import { apiSlice } from './features/apiSlice'

export const store = configureStore({
  reducer: {
    team: teamReducer,
    roadmapTimelineSettings: roadmapTimelineSettingsReducer,
    workProcess: workProcessReducer,
    workspace: workspaceReducer,
    workType: workTypeReducer,
    [apiSlice.reducerPath]: apiSlice.reducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(apiSlice.middleware),
})

// Tracks window focus and connectivity, without which a query's
// refetchOnFocus and skipPollingIfUnfocused do nothing.
setupListeners(store.dispatch)

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
