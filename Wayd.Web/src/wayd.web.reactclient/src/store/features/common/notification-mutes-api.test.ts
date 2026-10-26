import { configureStore } from '@reduxjs/toolkit'
import { NotificationReason } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { notificationMutesApi } from './notification-mutes-api'
import { notificationsApi } from './notifications-api'

const getRecordMuteStatus = jest.fn()
const getMutedRecords = jest.fn()
const getReasonMutes = jest.fn()
const muteRecord = jest.fn()
const unmuteRecord = jest.fn()
const muteReason = jest.fn()
const unmuteReason = jest.fn()
const getNotifications = jest.fn()
const getUnreadCount = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getMyNotificationMutesClient: () => ({
    getRecordMuteStatus,
    getMutedRecords,
    getReasonMutes,
    muteRecord,
    unmuteRecord,
    muteReason,
    unmuteReason,
  }),
  getMyNotificationsClient: () => ({ getNotifications, getUnreadCount }),
}))

const target = { ownerType: 'ppm.project', ownerId: 'p-1' }

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const waitForCalls = () => new Promise((resolve) => setTimeout(resolve, 0))

const resolveReads = () => {
  getRecordMuteStatus.mockResolvedValue({ isMuted: false })
  getMutedRecords.mockResolvedValue({
    items: [],
    pageNumber: 1,
    hasNextPage: false,
  })
  getReasonMutes.mockResolvedValue([])
  getNotifications.mockResolvedValue({ items: [], nextCursor: null })
  getUnreadCount.mockResolvedValue({ count: 0, isCapped: false })
}

/** Holds every read a mute can change, so an invalidation refetches it. */
const loadedStore = async () => {
  resolveReads()
  const store = createStore()
  store.dispatch(
    notificationMutesApi.endpoints.getRecordMuteStatus.initiate(target),
  )
  store.dispatch(notificationMutesApi.endpoints.getMutedRecords.initiate())
  store.dispatch(notificationMutesApi.endpoints.getReasonMutes.initiate())
  store.dispatch(
    notificationsApi.endpoints.getMyNotifications.initiate({ pageSize: 10 }),
  )
  store.dispatch(
    notificationsApi.endpoints.getUnreadNotificationCount.initiate(),
  )
  await waitForCalls()
  jest.clearAllMocks()
  resolveReads()
  return store
}

const statusOf = (store: ReturnType<typeof createStore>) =>
  notificationMutesApi.endpoints.getRecordMuteStatus.select(target)(
    store.getState(),
  ).data

describe('notificationMutesApi', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('pages the muted records by number', async () => {
    // Arrange
    resolveReads()
    const store = createStore()

    // Act
    await store.dispatch(
      notificationMutesApi.endpoints.getMutedRecords.initiate(),
    )

    // Assert
    expect(getMutedRecords).toHaveBeenCalledWith(1, 25)
  })

  it('refetches the status, the mutes, the notifications and the count after muting a record', async () => {
    // Arrange
    const store = await loadedStore()
    muteRecord.mockResolvedValue({ wasAlreadyMuted: false })

    // Act
    await store.dispatch(
      notificationMutesApi.endpoints.muteRecord.initiate(target),
    )
    await waitForCalls()

    // Assert
    expect(muteRecord).toHaveBeenCalledWith('ppm.project', 'p-1')
    expect(getRecordMuteStatus).toHaveBeenCalledTimes(1)
    expect(getMutedRecords).toHaveBeenCalledTimes(1)
    expect(getNotifications).toHaveBeenCalledTimes(1)
    expect(getUnreadCount).toHaveBeenCalledTimes(1)
  })

  it('refetches the status and the mutes, not the notifications, after unmuting a record', async () => {
    // Arrange
    const store = await loadedStore()
    unmuteRecord.mockResolvedValue(undefined)

    // Act
    await store.dispatch(
      notificationMutesApi.endpoints.unmuteRecord.initiate(target),
    )
    await waitForCalls()

    // Assert
    expect(unmuteRecord).toHaveBeenCalledWith('ppm.project', 'p-1')
    expect(getRecordMuteStatus).toHaveBeenCalledTimes(1)
    expect(getMutedRecords).toHaveBeenCalledTimes(1)
    expect(getNotifications).not.toHaveBeenCalled()
    expect(getUnreadCount).not.toHaveBeenCalled()
  })

  it('shows a record muted at once, and rolls it back when muting fails', async () => {
    // Arrange
    const store = await loadedStore()
    let fail: (reason: unknown) => void = () => {}
    muteRecord.mockReturnValue(
      new Promise((_, reject) => {
        fail = reject
      }),
    )
    // The refetch after the failure answers as the server would: still unmuted.
    getRecordMuteStatus.mockResolvedValue({ isMuted: false })

    // Act
    const pending = store.dispatch(
      notificationMutesApi.endpoints.muteRecord.initiate(target),
    )
    await waitForCalls()

    // Assert
    expect(statusOf(store)?.isMuted).toBe(true)

    // Act
    fail({ status: 500 })
    await pending
    await waitForCalls()

    // Assert
    expect(statusOf(store)?.isMuted).toBe(false)
  })

  it('shows a reason muted at once, and rolls it back when muting fails', async () => {
    // Arrange
    const store = await loadedStore()
    getReasonMutes.mockResolvedValue([
      { reason: NotificationReason.Participant, isMuted: false },
    ])
    await store.dispatch(
      notificationMutesApi.endpoints.getReasonMutes.initiate(undefined, {
        forceRefetch: true,
      }),
    )
    let fail: (reason: unknown) => void = () => {}
    muteReason.mockReturnValue(
      new Promise((_, reject) => {
        fail = reject
      }),
    )
    const reasonsOf = () =>
      notificationMutesApi.endpoints.getReasonMutes.select()(store.getState())
        .data

    // Act
    const pending = store.dispatch(
      notificationMutesApi.endpoints.muteReason.initiate(
        NotificationReason.Participant,
      ),
    )
    await waitForCalls()

    // Assert
    expect(reasonsOf()?.[0].isMuted).toBe(true)

    // Act
    fail({ status: 500 })
    await pending
    await waitForCalls()

    // Assert
    expect(reasonsOf()?.[0].isMuted).toBe(false)
  })

  it.each([
    ['mute', 'muteReason', muteReason],
    ['unmute', 'unmuteReason', unmuteReason],
  ] as const)(
    'refetches the reasons after a reason is %sd',
    async (_, endpoint, call) => {
      // Arrange
      const store = await loadedStore()
      call.mockResolvedValue(undefined)

      // Act
      await store.dispatch(
        notificationMutesApi.endpoints[endpoint].initiate(
          NotificationReason.Participant,
        ),
      )
      await waitForCalls()

      // Assert
      expect(call).toHaveBeenCalledWith(NotificationReason.Participant)
      expect(getReasonMutes).toHaveBeenCalledTimes(1)
      expect(getNotifications).not.toHaveBeenCalled()
    },
  )
})
