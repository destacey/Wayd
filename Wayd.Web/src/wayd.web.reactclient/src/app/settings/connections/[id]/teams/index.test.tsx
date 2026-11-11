import type { Mock } from 'vitest'
import { render, waitFor } from '@testing-library/react'
import type { ItemType } from 'antd/es/menu/interface'
import { teamsDetailEntry } from './index'

const mockTest = vi.fn()
const mockMessage = { error: vi.fn(), success: vi.fn() }

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

vi.mock('@/src/store/features/app-integration/connections-api', () => ({
  useTestCommunicationConnectionMutation: () => [
    mockTest,
    { isLoading: false },
  ],
}))

const fallback =
  'The connection test failed. Confirm admin consent, permissions, and that your Wayd email exists in the tenant.'

const renderActions = () => {
  const setItems = vi.fn()
  const ExtraActions = teamsDetailEntry.ExtraActions
  if (!ExtraActions) throw new Error('Teams detail entry has no extra actions')

  render(
    <ExtraActions
      ctx={{
        connectionId: 'conn-1',
        connection: {} as never,
        reload: vi.fn(),
        canUpdate: true,
      }}
      setItems={setItems}
    />,
  )

  return setItems
}

const runTest = async (setItems: Mock) => {
  await waitFor(() => expect(setItems).toHaveBeenCalled())
  const items = setItems.mock.calls.at(-1)?.[0] as ItemType[]
  const action = items.find(
    (item): item is ItemType & { onClick: () => Promise<void> } =>
      !!item &&
      'key' in item &&
      item.key === 'test-connection' &&
      'onClick' in item,
  )
  if (!action?.onClick)
    throw new Error('Test connection action was not registered')
  await action.onClick()
}

describe('Teams connection detail', () => {
  beforeEach(() => {
    mockTest.mockReset()
    mockMessage.error.mockReset()
    mockMessage.success.mockReset()
  })

  it('shows the API error detail when Test connection fails', async () => {
    // Arrange
    mockTest.mockResolvedValue({
      error: {
        isApiException: true,
        status: 400,
        response: { detail: 'Admin consent has not been granted.' },
      },
    })
    const setItems = renderActions()

    // Act
    await runTest(setItems)

    // Assert
    await waitFor(() =>
      expect(mockMessage.error).toHaveBeenCalledWith(
        'Admin consent has not been granted.',
      ),
    )
    expect(mockMessage.error).not.toHaveBeenCalledWith(fallback)
  })

  it('uses the fallback message when the API error has no detail', async () => {
    // Arrange
    mockTest.mockResolvedValue({
      error: { isApiException: true, status: 500, response: {} },
    })
    const setItems = renderActions()

    // Act
    await runTest(setItems)

    // Assert
    await waitFor(() =>
      expect(mockMessage.error).toHaveBeenCalledWith(fallback),
    )
  })
})
