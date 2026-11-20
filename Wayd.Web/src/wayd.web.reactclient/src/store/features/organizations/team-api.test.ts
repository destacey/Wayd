import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import { teamApi } from './team-api'

const getActiveSprint = vi.fn()
const addTeamOfTeamsMembership = vi.fn()
const updateTeamOfTeamsMembership = vi.fn()
const removeTeamOfTeamsMembership = vi.fn()

vi.mock(import('@/src/services/api/teams'), async (importOriginal) => ({
  ...(await importOriginal()),
  teamsGetActiveSprint: (...args: unknown[]) => getActiveSprint(...args),
}))

vi.mock(
  import('@/src/services/api/teams-of-teams'),
  async (importOriginal) => ({
    ...(await importOriginal()),
    teamsOfTeamsAddTeamMembership: (...args: unknown[]) =>
      addTeamOfTeamsMembership(...args),
    teamsOfTeamsUpdateTeamMembership: (...args: unknown[]) =>
      updateTeamOfTeamsMembership(...args),
    teamsOfTeamsRemoveTeamMembership: (...args: unknown[]) =>
      removeTeamOfTeamsMembership(...args),
  }),
)

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

describe('teamApi', () => {
  beforeEach(() => {
    getActiveSprint.mockReset()
    addTeamOfTeamsMembership.mockReset()
    updateTeamOfTeamsMembership.mockReset()
    removeTeamOfTeamsMembership.mockReset()
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('getActiveSprint', () => {
    it('returns the sprint the API answers with', async () => {
      // Arrange
      const sprint = { id: 'sprint-1', key: 12, name: 'Sprint 12' }
      getActiveSprint.mockResolvedValue(sprint)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toEqual(sprint)
      expect(getActiveSprint).toHaveBeenCalledWith('team-1')
    })

    it('reads a 404 as the team having no active sprint', async () => {
      // Arrange
      getActiveSprint.mockRejectedValue({ status: 404, title: 'Not Found' })
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
    })

    it('keeps any other failure as an error', async () => {
      // Arrange
      const problem = { status: 500, title: 'Internal Server Error' }
      getActiveSprint.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.data).toBeUndefined()
      expect(result.error).toEqual(problem)
    })
  })

  // The membership endpoints answer 204 with no body, which RTK Query rejects as a result.
  describe('team membership mutations', () => {
    it('createTeamMembership succeeds on an empty response', async () => {
      // Arrange
      addTeamOfTeamsMembership.mockResolvedValue(undefined)
      const store = createStore()
      const membership = {
        teamId: 'team-1',
        parentTeamId: 'parent-1',
        start: '2026-01-01',
      } as Parameters<
        typeof teamApi.endpoints.createTeamMembership.initiate
      >[0]['membership']

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.createTeamMembership.initiate({
          membership,
          teamType: 'Team of Teams',
        }),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(console.error).not.toHaveBeenCalled()
      expect(addTeamOfTeamsMembership).toHaveBeenCalledWith(
        'team-1',
        membership,
      )
    })

    it('updateTeamMembership succeeds on an empty response', async () => {
      // Arrange
      updateTeamOfTeamsMembership.mockResolvedValue(undefined)
      const store = createStore()
      const membership = {
        teamId: 'team-1',
        teamMembershipId: 'membership-1',
        start: '2026-01-01',
      } as Parameters<
        typeof teamApi.endpoints.updateTeamMembership.initiate
      >[0]['membership']

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.updateTeamMembership.initiate({
          membership,
          parentTeamId: 'parent-1',
          teamType: 'Team of Teams',
        }),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(console.error).not.toHaveBeenCalled()
    })

    it('deleteTeamMembership succeeds on an empty response', async () => {
      // Arrange
      removeTeamOfTeamsMembership.mockResolvedValue(undefined)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.deleteTeamMembership.initiate({
          teamMembershipId: 'membership-1',
          teamId: 'team-1',
          parentTeamId: 'parent-1',
          teamType: 'Team of Teams',
        }),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(console.error).not.toHaveBeenCalled()
      expect(removeTeamOfTeamsMembership).toHaveBeenCalledWith(
        'team-1',
        'membership-1',
      )
    })

    it('keeps a refused membership as an error', async () => {
      // Arrange
      const problem = { status: 400, detail: 'circular reference' }
      addTeamOfTeamsMembership.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.createTeamMembership.initiate({
          membership: {
            teamId: 'team-1',
            parentTeamId: 'parent-1',
          } as Parameters<
            typeof teamApi.endpoints.createTeamMembership.initiate
          >[0]['membership'],
          teamType: 'Team of Teams',
        }),
      )

      // Assert
      expect(result.data).toBeUndefined()
      expect(result.error).toEqual(problem)
    })
  })
})
