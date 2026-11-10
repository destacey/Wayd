import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeChannelStateForm from './change-intake-channel-state-form'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/hooks', () => ({
  useConfirmModal: ({
    onSubmit,
    onComplete,
  }: {
    onSubmit: () => Promise<boolean>
    onComplete: () => void
  }) => ({
    isOpen: true,
    isSaving: false,
    handleOk: async () => {
      if (await onSubmit()) onComplete()
    },
    handleCancel: vi.fn(),
  }),
}))

const mockArchive = vi.fn()
const mockRestore = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useArchiveIntakeChannelMutation: () => [mockArchive],
  useRestoreIntakeChannelMutation: () => [mockRestore],
}))

const channel = { id: 'c-1', name: 'Support', order: 1, isActive: true }

describe('ChangeIntakeChannelStateForm', () => {
  beforeEach(() => {
    mockArchive.mockReset()
    mockRestore.mockReset()
    mockArchive.mockReturnValue({ unwrap: () => Promise.resolve() })
    mockRestore.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('archives the channel, saying requests keep it', async () => {
    // Arrange
    const onComplete = vi.fn()
    await act(async () =>
      render(
        <ChangeIntakeChannelStateForm
          channel={channel}
          archive
          onFormComplete={onComplete}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Archive' }))

    // Assert
    expect(
      screen.getByText(/Requests already from this channel keep it/),
    ).toBeInTheDocument()
    expect(mockArchive).toHaveBeenCalledWith('c-1')
    expect(mockRestore).not.toHaveBeenCalled()
    expect(onComplete).toHaveBeenCalled()
  })

  it('restores an archived channel', async () => {
    // Arrange
    await act(async () =>
      render(
        <ChangeIntakeChannelStateForm
          channel={{ ...channel, isActive: false }}
          archive={false}
          onFormComplete={vi.fn()}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Restore' }))

    // Assert
    expect(mockRestore).toHaveBeenCalledWith('c-1')
    expect(mockArchive).not.toHaveBeenCalled()
  })
})
