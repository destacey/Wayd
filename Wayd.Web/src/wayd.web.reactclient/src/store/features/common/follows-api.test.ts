import { configureStore } from '@reduxjs/toolkit'
import { waitFor } from '@testing-library/react'
import { apiSlice } from '../apiSlice'
import { followsApi, FollowTarget } from './follows-api'

const getFollowStatus = jest.fn()
const follow = jest.fn()
const unfollow = jest.fn()
const getFollowedRecord = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getMyFollowsClient: () => ({
    getFollowStatus,
    follow,
    unfollow,
    getFollowedRecord,
  }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const target: FollowTarget = { ownerType: 'ppm.project', ownerId: 'p-1' }

const cachedStatus = (store: ReturnType<typeof createStore>) =>
  followsApi.endpoints.getFollowStatus.select(target)(store.getState()).data

/** A promise the test settles by hand, to look at the cache mid-request. */
const deferred = () => {
  let resolve!: () => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<void>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

describe('followsApi', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows the record as followed while the follow is still in flight', async () => {
    // Arrange
    getFollowStatus.mockResolvedValue({ isFollowing: false })
    const store = createStore()
    await store.dispatch(followsApi.endpoints.getFollowStatus.initiate(target))
    const request = deferred()
    follow.mockReturnValue(request.promise)

    // Act
    const pending = store.dispatch(followsApi.endpoints.follow.initiate(target))

    // Assert
    expect(cachedStatus(store)?.isFollowing).toBe(true)
    request.resolve()
    await pending
    expect(follow).toHaveBeenCalledWith('ppm.project', 'p-1')
  })

  it('puts the status back when the follow fails', async () => {
    // Arrange
    getFollowStatus.mockResolvedValue({ isFollowing: false })
    const store = createStore()
    await store.dispatch(followsApi.endpoints.getFollowStatus.initiate(target))
    const request = deferred()
    follow.mockReturnValue(request.promise)
    getFollowStatus.mockReturnValue(new Promise(() => {}))

    // Act
    const pending = store.dispatch(followsApi.endpoints.follow.initiate(target))
    request.reject({ status: 403, detail: 'Not linked.' })
    await pending

    // Assert
    expect(cachedStatus(store)?.isFollowing).toBe(false)
  })

  it('shows the record as not followed while the unfollow is still in flight', async () => {
    // Arrange
    getFollowStatus.mockResolvedValue({
      isFollowing: true,
      followedOn: new Date('2026-10-23T09:30:00Z'),
    })
    const store = createStore()
    await store.dispatch(followsApi.endpoints.getFollowStatus.initiate(target))
    const request = deferred()
    unfollow.mockReturnValue(request.promise)

    // Act
    const pending = store.dispatch(
      followsApi.endpoints.unfollow.initiate(target),
    )

    // Assert
    expect(cachedStatus(store)).toEqual({
      isFollowing: false,
      followedOn: undefined,
    })
    request.resolve()
    await pending
    expect(unfollow).toHaveBeenCalledWith('ppm.project', 'p-1')
  })

  it('does not reload the detail of a record just unfollowed', async () => {
    // Arrange
    getFollowedRecord.mockResolvedValue({ record: { name: 'Apollo' } })
    unfollow.mockResolvedValue(undefined)
    getFollowStatus.mockResolvedValue({ isFollowing: false })
    const store = createStore()
    const detail = store.dispatch(
      followsApi.endpoints.getFollowedRecord.initiate(target),
    )
    await detail

    // Act
    await store.dispatch(followsApi.endpoints.unfollow.initiate(target))

    // Assert
    expect(getFollowedRecord).toHaveBeenCalledTimes(1)
    detail.unsubscribe()
  })

  it('reloads the detail of a record followed again', async () => {
    // Arrange
    getFollowedRecord.mockResolvedValue({ record: { name: 'Apollo' } })
    follow.mockResolvedValue(undefined)
    getFollowStatus.mockResolvedValue({ isFollowing: true })
    const store = createStore()
    const detail = store.dispatch(
      followsApi.endpoints.getFollowedRecord.initiate(target),
    )
    await detail

    // Act
    await store.dispatch(followsApi.endpoints.follow.initiate(target))

    // Assert
    await waitFor(() => expect(getFollowedRecord).toHaveBeenCalledTimes(2))
    detail.unsubscribe()
  })
})
