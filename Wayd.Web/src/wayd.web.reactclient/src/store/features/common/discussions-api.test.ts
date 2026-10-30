import { configureStore } from '@reduxjs/toolkit'
import { waitFor } from '@testing-library/react'
import { DiscussionMessageKind } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { demandManagementApi } from '../demand-management/demand-management-api'
import { projectsApi } from '../ppm/projects-api'
import { DiscussionChannelTypes, discussionsApi } from './discussions-api'

const postMessage = jest.fn()
const getStatusOptions = jest.fn()
const getProject = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getDiscussionsClient: () => ({ postMessage }),
  getIntakeRequestsClient: () => ({ getStatusOptions }),
  getProjectsClient: () => ({ getProject }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

describe('discussionsApi', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    postMessage.mockResolvedValue('m-1')
    getStatusOptions.mockResolvedValue([])
    getProject.mockResolvedValue({ id: 'p-1', key: 'APOLLO' })
  })

  it('reloads the intake request after a post, since posting can add a participant', async () => {
    // Arrange — anything cached for the request, here its status options
    const store = createStore()
    const subscription = store.dispatch(
      demandManagementApi.endpoints.getIntakeRequestStatusOptions.initiate(
        'r-1',
      ),
    )
    await subscription
    expect(getStatusOptions).toHaveBeenCalledTimes(1)

    // Act
    await store.dispatch(
      discussionsApi.endpoints.postDiscussionMessage.initiate({
        channelType: DiscussionChannelTypes.IntakeRequest,
        ownerId: 'r-1',
        kind: DiscussionMessageKind.Public,
        body: 'Over to @[Dana Ortiz](employee:e-7)',
      }),
    )

    // Assert
    await waitFor(() => expect(getStatusOptions).toHaveBeenCalledTimes(2))
    subscription.unsubscribe()
  })
  it('leaves the project alone after a post, since posting changes nothing on it', async () => {
    // Arrange
    const store = createStore()
    const subscription = store.dispatch(
      projectsApi.endpoints.getProject.initiate('p-1'),
    )
    await subscription
    expect(getProject).toHaveBeenCalledTimes(1)

    // Act
    await store.dispatch(
      discussionsApi.endpoints.postDiscussionMessage.initiate({
        channelType: DiscussionChannelTypes.Project,
        ownerId: 'p-1',
        kind: DiscussionMessageKind.Public,
        body: 'Vendor contract signed',
      }),
    )

    // Assert
    expect(postMessage).toHaveBeenCalledWith('ppm.project', 'p-1', {
      kind: DiscussionMessageKind.Public,
      subject: undefined,
      body: 'Vendor contract signed',
    })
    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(getProject).toHaveBeenCalledTimes(1)
    subscription.unsubscribe()
  })
})
