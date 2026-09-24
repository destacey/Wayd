import { configureStore } from '@reduxjs/toolkit'
import { renderHook, waitFor } from '@testing-library/react'
import { Provider } from 'react-redux'
import type { ReactNode } from 'react'
import {
  ScopedDependencyDto,
  WorkItemDetailsNavigationDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '@/src/store/features/apiSlice'
import { useExpandedWorkItemDependencies } from './use-expanded-work-item-dependencies'

const getWorkItemDependencies = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getWorkspacesClient: () => ({ getWorkItemDependencies }),
}))

const item = (id: string, key: string): WorkItemDetailsNavigationDto => ({
  id,
  key,
  title: key,
  workspaceKey: key.split('-')[0],
  type: 'Story',
  status: 'Active',
  statusCategory: { id: 2, name: 'Active' },
})

const predecessor = (
  id: string,
  other: WorkItemDetailsNavigationDto,
): ScopedDependencyDto => ({
  id,
  dependency: other,
  type: 'Predecessor',
  state: { id: 1, name: 'To Do' },
  health: { id: 1, name: 'Healthy' },
  scope: { id: 2, name: 'Cross-Team' },
  createdOn: new Date('2026-01-01'),
})

const auth = item('w-2', 'ID-7')
const keys = item('w-5', 'SEC-2')

const wrapperFor = () => {
  const store = configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    // The generated DTOs carry Dates, which the serializability check reports on every action.
    middleware: (getDefault) =>
      getDefault({ serializableCheck: false }).concat(apiSlice.middleware),
  })

  const Wrapper = ({ children }: { children: ReactNode }) => (
    <Provider store={store}>{children}</Provider>
  )
  return Wrapper
}

describe('useExpandedWorkItemDependencies', () => {
  beforeEach(() => {
    getWorkItemDependencies.mockReset()
  })

  it('fetches an expanded item from the workspace and key its link carries', async () => {
    // Arrange
    getWorkItemDependencies.mockResolvedValue([])

    // Act
    const { result } = renderHook(
      () => useExpandedWorkItemDependencies([predecessor('d1', auth)], ['w-2']),
      { wrapper: wrapperFor() },
    )

    // Assert
    await waitFor(() => expect(result.current['w-2']?.dependencies).toEqual([]))
    expect(getWorkItemDependencies).toHaveBeenCalledWith('ID', 'ID-7')
    expect(result.current['w-2'].workItem.key).toBe('ID-7')
  })

  it('reaches an item two hops out once the one before it has loaded', async () => {
    // Arrange
    getWorkItemDependencies.mockImplementation(
      async (_: string, key: string) =>
        key === 'ID-7' ? [predecessor('e1', keys)] : [],
    )

    // Act
    const { result } = renderHook(
      () =>
        useExpandedWorkItemDependencies(
          [predecessor('d1', auth)],
          ['w-2', 'w-5'],
        ),
      { wrapper: wrapperFor() },
    )

    // Assert
    await waitFor(() => expect(result.current['w-5']?.dependencies).toEqual([]))
    expect(getWorkItemDependencies).toHaveBeenCalledWith('SEC', 'SEC-2')
  })

  it('leaves out an expanded item nothing loaded yet names', () => {
    // Arrange
    getWorkItemDependencies.mockReturnValue(new Promise(() => {}))

    // Act
    const { result } = renderHook(
      () => useExpandedWorkItemDependencies([], ['w-9']),
      { wrapper: wrapperFor() },
    )

    // Assert
    expect(result.current).toEqual({})
    expect(getWorkItemDependencies).not.toHaveBeenCalled()
  })
})
