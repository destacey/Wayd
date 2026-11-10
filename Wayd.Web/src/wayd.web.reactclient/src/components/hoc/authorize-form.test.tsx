import type { Mock } from 'vitest'
import React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { message } from 'antd'
import authorizeForm from './authorize-form'
import { MessageProvider } from '../contexts/messaging'

const DummyForm: React.FC = () => <div>Dummy Form</div>
DummyForm.displayName = 'DummyForm'

const mockHasClaim = vi.fn()
vi.mock('../contexts/auth', () => ({
  default: () => ({
    hasClaim: mockHasClaim,
  }),
}))

vi.mock(import('antd'), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    // The HOC only ever shows an error, so the other message methods are left out.
    message: {
      error: vi.fn(),
      useMessage: vi.fn(() => [
        {
          error: vi.fn(),
        },
        <div key="contextHolder" />,
      ]),
    } as unknown as typeof actual.message,
  }
})

describe('authorizeForm HOC', () => {
  let onNotAuthorized: Mock

  beforeEach(() => {
    onNotAuthorized = vi.fn()
    mockHasClaim.mockReset()
    vi.clearAllMocks()
  })

  const renderWithMessageProvider = (ui: React.ReactElement) => {
    return render(<MessageProvider>{ui}</MessageProvider>)
  }

  test('calls onNotAuthorized and shows error when unauthorized', async () => {
    mockHasClaim.mockReturnValue(false)

    const AuthorizedDummyForm = authorizeForm(
      DummyForm,
      onNotAuthorized,
      'Permission',
      'test-value',
    )

    renderWithMessageProvider(<AuthorizedDummyForm />)

    expect(screen.queryByText('Dummy Form')).toBeNull()

    await waitFor(() => {
      expect(onNotAuthorized).toHaveBeenCalledTimes(1)

      const [mockMessageApi] = (message.useMessage as Mock).mock.results[0]
        .value
      expect(mockMessageApi.error).toHaveBeenCalledWith(
        'You do not have the correct permissions to access this form.',
      )
    })
  })
})
