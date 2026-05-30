import { createSlice, PayloadAction } from '@reduxjs/toolkit'

export const MIN_GROUP_COLUMN_WIDTH = 150
export const MAX_GROUP_COLUMN_WIDTH = 480
export const DEFAULT_GROUP_COLUMN_WIDTH = 260

interface RoadmapTimelineSettingsState {
  groupColumnWidthByRoadmapId: Record<string, number>
}

const initialState: RoadmapTimelineSettingsState = {
  groupColumnWidthByRoadmapId: {},
}

const roadmapTimelineSettingsSlice = createSlice({
  name: 'roadmapTimelineSettings',
  initialState,
  reducers: {
    setRoadmapGroupColumnWidth: (
      state,
      action: PayloadAction<{ roadmapId: string; width: number }>,
    ) => {
      const clamped = Math.min(
        MAX_GROUP_COLUMN_WIDTH,
        Math.max(MIN_GROUP_COLUMN_WIDTH, action.payload.width),
      )
      state.groupColumnWidthByRoadmapId[action.payload.roadmapId] = clamped
    },
    resetRoadmapGroupColumnWidth: (
      state,
      action: PayloadAction<{ roadmapId: string }>,
    ) => {
      delete state.groupColumnWidthByRoadmapId[action.payload.roadmapId]
    },
  },
})

export const {
  setRoadmapGroupColumnWidth,
  resetRoadmapGroupColumnWidth,
} = roadmapTimelineSettingsSlice.actions

export default roadmapTimelineSettingsSlice.reducer
