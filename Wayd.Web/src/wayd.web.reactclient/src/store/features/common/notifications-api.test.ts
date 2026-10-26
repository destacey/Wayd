import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import {
  buildNotification,
  buildNotifiedRecord,
} from '@/src/components/common/notifications/notification-test-data'
import { notificationsApi } from './notifications-api'

const getNotifications = jest.fn()
const getUnreadCount = jest.fn()
const markRecordRead = jest.fn()
const markAllRead = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getMyNotificationsClient: () => ({
    getNotifications,
    getUnreadCount,
    markRecordRead,
    markAllRead,
  }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

// The server sends null, not an absent field, on the last page.
const emptyPage = { items: [], nextCursor: null }

/** Loads the list and the count, as the bell does, and returns the store. */
const loadedStore = async () => {
  getNotifications.mockResolvedValue(emptyPage)
  getUnreadCount.mockResolvedValue({ count: 3, isCapped: false })
  const store = createStore()
  // Held subscriptions, so an invalidation refetches rather than evicts.
  store.dispatch(
    notificationsApi.endpoints.getMyNotifications.initiate({ pageSize: 10 }),
  )
  store.dispatch(
    notificationsApi.endpoints.getUnreadNotificationCount.initiate(),
  )
  await waitForCalls()
  jest.clearAllMocks()
  getNotifications.mockResolvedValue(emptyPage)
  getUnreadCount.mockResolvedValue({ count: 0, isCapped: false })
  return store
}

const waitForCalls = () => new Promise((resolve) => setTimeout(resolve, 0))

describe('notificationsApi', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('passes the filter and page to the list endpoint', async () => {
    // Arrange
    getNotifications.mockResolvedValue(emptyPage)
    const store = createStore()

    // Act
    await store.dispatch(
      notificationsApi.endpoints.getMyNotifications.initiate({
        unreadOnly: true,
        pageSize: 25,
      }),
    )

    // Assert
    expect(getNotifications).toHaveBeenCalledWith(true, null, 25)
  })

  it('refetches the list and the count after a record is marked read', async () => {
    // Arrange
    const store = await loadedStore()
    markRecordRead.mockResolvedValue(undefined)

    // Act
    await store.dispatch(
      notificationsApi.endpoints.markRecordNotificationsRead.initiate({
        ownerType: 'ppm.project',
        ownerId: 'p-1',
      }),
    )
    await waitForCalls()

    // Assert
    expect(markRecordRead).toHaveBeenCalledWith('ppm.project', 'p-1')
    expect(getNotifications).toHaveBeenCalledTimes(1)
    expect(getUnreadCount).toHaveBeenCalledTimes(1)
  })

  it('refetches the list and the count after everything is marked read', async () => {
    // Arrange
    const store = await loadedStore()
    markAllRead.mockResolvedValue(undefined)

    // Act
    await store.dispatch(
      notificationsApi.endpoints.markAllNotificationsRead.initiate(),
    )
    await waitForCalls()

    // Assert
    expect(markAllRead).toHaveBeenCalledTimes(1)
    expect(getNotifications).toHaveBeenCalledTimes(1)
    expect(getUnreadCount).toHaveBeenCalledTimes(1)
  })
})

describe('notificationsApi paging', () => {
  const entry = (id: string) =>
    buildNotification({ record: buildNotifiedRecord({ id }) })

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('asks for the next page with the cursor the last one gave', async () => {
    // Arrange
    getNotifications
      .mockResolvedValueOnce({ items: [entry('a')], nextCursor: 'cursor-1' })
      .mockResolvedValueOnce({ items: [entry('b')], nextCursor: undefined })
    const store = createStore()
    const arg = { pageSize: 1 }
    await store.dispatch(
      notificationsApi.endpoints.getMyNotifications.initiate(arg),
    )

    // Act
    await store.dispatch(
      notificationsApi.endpoints.getMyNotifications.initiate(arg, {
        direction: 'forward',
      }),
    )

    // Assert
    expect(getNotifications).toHaveBeenNthCalledWith(1, undefined, null, 1)
    expect(getNotifications).toHaveBeenNthCalledWith(
      2,
      undefined,
      'cursor-1',
      1,
    )
    const state = notificationsApi.endpoints.getMyNotifications.select(arg)(
      store.getState(),
    )
    expect(
      state.data?.pages.flatMap((page) => page.items.map((i) => i.record.id)),
    ).toEqual(['a', 'b'])
    expect(state.hasNextPage).toBe(false)
  })

  it('starts again from the first page after a mark-read, following the refetched cursors', async () => {
    // Arrange — two pages loaded and held, as the Notifications page holds them.
    getNotifications
      .mockResolvedValueOnce({ items: [entry('a')], nextCursor: 'cursor-a' })
      .mockResolvedValueOnce({ items: [entry('b')], nextCursor: null })
    const store = createStore()
    const arg = { pageSize: 1 }
    store.dispatch(notificationsApi.endpoints.getMyNotifications.initiate(arg))
    await waitForCalls()
    await store.dispatch(
      notificationsApi.endpoints.getMyNotifications.initiate(arg, {
        direction: 'forward',
      }),
    )
    // Since then a new change moved record c to the top.
    getNotifications
      .mockResolvedValueOnce({ items: [entry('c')], nextCursor: 'cursor-c' })
      .mockResolvedValueOnce({ items: [entry('a')], nextCursor: 'cursor-a' })
    markAllRead.mockResolvedValue(undefined)

    // Act
    await store.dispatch(
      notificationsApi.endpoints.markAllNotificationsRead.initiate(),
    )
    await waitForCalls()

    // Assert — the refetch asks for the first page again, then the page after
    // its new last entry, not the stale stored cursor.
    expect(getNotifications).toHaveBeenNthCalledWith(3, undefined, null, 1)
    expect(getNotifications).toHaveBeenNthCalledWith(
      4,
      undefined,
      'cursor-c',
      1,
    )
    const state = notificationsApi.endpoints.getMyNotifications.select(arg)(
      store.getState(),
    )
    expect(
      state.data?.pages.flatMap((page) => page.items.map((i) => i.record.id)),
    ).toEqual(['c', 'a'])
  })

  it('has no next page when the first page gives no cursor', async () => {
    // Arrange
    getNotifications.mockResolvedValue({
      items: [entry('a')],
      nextCursor: null,
    })
    const store = createStore()
    const arg = { pageSize: 1 }

    // Act
    await store.dispatch(
      notificationsApi.endpoints.getMyNotifications.initiate(arg),
    )

    // Assert
    const state = notificationsApi.endpoints.getMyNotifications.select(arg)(
      store.getState(),
    )
    expect(state.hasNextPage).toBe(false)
  })
})
