import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import {
  buildNotification,
  buildNotifiedRecord,
} from '@/src/components/common/notifications/notification-test-data'
import { notificationEntries, notificationsApi } from './notifications-api'

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

const emptyPage = {
  items: [],
  pageNumber: 1,
  pageSize: 10,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
}

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
    expect(getNotifications).toHaveBeenCalledWith(true, 1, 25)
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

describe('notificationEntries', () => {
  const entry = (id: string) =>
    buildNotification({ record: buildNotifiedRecord({ id }) })
  const page = (items: ReturnType<typeof entry>[]) => ({
    ...emptyPage,
    items,
  })

  it('keeps every entry of every page in order', () => {
    // Act
    const entries = notificationEntries([
      page([entry('a'), entry('b')]),
      page([entry('c')]),
    ])

    // Assert
    expect(entries.map((e) => e.record.id)).toEqual(['a', 'b', 'c'])
  })

  it('keeps the first sighting of a record that reached two pages', () => {
    // Act
    const entries = notificationEntries([
      page([entry('a'), entry('b')]),
      page([entry('b'), entry('c')]),
    ])

    // Assert
    expect(entries.map((e) => e.record.id)).toEqual(['a', 'b', 'c'])
  })

  it('keeps records that share an id across owner types', () => {
    // Act
    const entries = notificationEntries([
      page([
        entry('a'),
        buildNotification({
          record: buildNotifiedRecord({ id: 'a', ownerType: 'ppm.program' }),
        }),
      ]),
    ])

    // Assert
    expect(entries).toHaveLength(2)
  })

  it('gives nothing before the first page arrives', () => {
    // Act & Assert
    expect(notificationEntries(undefined)).toEqual([])
  })
})
