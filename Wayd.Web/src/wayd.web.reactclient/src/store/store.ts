import { configureStore } from '@reduxjs/toolkit'
import breadcrumbReducer from './breadcrumbs/breadcrumb-slice'
import teamReducer from './features/organizations/team-slice'
import roadmapTimelineSettingsReducer from './features/planning/roadmap-timeline-settings-slice'
import workProcessReducer from './features/work-management/work-process-slice'
import workspaceReducer from './features/work-management/workspace-slice'
import workStatusReducer from './features/work-management/work-status-slice'
import workTypeReducer from './features/work-management/work-type-slice'
import { apiSlice } from './features/apiSlice'

export const store = configureStore({
  reducer: {
    team: teamReducer,
    roadmapTimelineSettings: roadmapTimelineSettingsReducer,
    breadcrumb: breadcrumbReducer,
    workProcess: workProcessReducer,
    workspace: workspaceReducer,
    workStatus: workStatusReducer,
    workType: workTypeReducer,
    [apiSlice.reducerPath]: apiSlice.reducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(apiSlice.middleware),
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
