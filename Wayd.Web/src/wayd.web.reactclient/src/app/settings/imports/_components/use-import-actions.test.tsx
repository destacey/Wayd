import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { Button } from 'antd'
import { ResumedImport } from '@/src/services/wayd-api'
import useImportActions from './use-import-actions'

const mockCancel = jest.fn()
const mockResume = jest.fn()
const mockRetryFailed = jest.fn()
const mockApply = jest.fn()
const mockPush = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

interface ConfirmConfig {
  title?: string
  content?: string
  okText?: string
  onOk?: () => void | Promise<void>
}

/** The last dialog an action opened, so its wording and its confirm can both be exercised. */
let lastConfirm: ConfirmConfig | undefined

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    App: {
      ...actual.App,
      // Rendering antd's confirm portal drags Next server internals into jsdom. Capturing the config
      // tests the same two things that matter here: what it says, and what confirming it does.
      useApp: () => ({
        modal: {
          confirm: (config: ConfirmConfig) => {
            lastConfirm = config
          },
        },
      }),
    },
  }
})

jest.mock('@/src/store/features/admin/imports-api', () => ({
  useCancelImportProcessMutation: () => [
    (id: string) => ({ unwrap: () => mockCancel(id) }),
  ],
  useResumeImportProcessMutation: () => [
    (id: string) => ({ unwrap: () => mockResume(id) }),
  ],
  useRetryFailedImportRowsMutation: () => [
    (id: string) => ({ unwrap: () => mockRetryFailed(id) }),
  ],
  useApplyImportPreflightMutation: () => [
    (id: string) => ({ unwrap: () => mockApply(id) }),
  ],
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const importProcess = {
  id: 'import-1',
  displayName: 'Employee Import',
  totalRowCount: 25,
  failedRowCount: 3,
  unappliedRowCount: 2,
}

const resumed = (overrides: Partial<ResumedImport> = {}): ResumedImport =>
  ({ queuedRowCount: 2, skippedRowCount: 0, ...overrides }) as ResumedImport

/** Renders one button per action so a test can invoke it. */
const Harness = ({ preflight = false }: { preflight?: boolean }) => {
  const { handleCancel, handleResume, handleRetryFailed, handleApply } =
    useImportActions()

  return (
    <>
      <Button
        onClick={() =>
          handleCancel({ ...importProcess, isPreflight: preflight })
        }
      >
        stop
      </Button>
      <Button onClick={() => handleResume(importProcess)}>resume</Button>
      <Button onClick={() => handleRetryFailed(importProcess)}>retry</Button>
      <Button onClick={() => handleApply(importProcess)}>apply</Button>
      <Button
        onClick={() =>
          handleApply({ ...importProcess, appliedImportProcessId: 'import-9' })
        }
      >
        apply again
      </Button>
    </>
  )
}

const clickAction = (name: string) => {
  render(<Harness />)
  fireEvent.click(screen.getByRole('button', { name }))
}

beforeEach(() => {
  jest.clearAllMocks()
  lastConfirm = undefined
  mockResume.mockResolvedValue(resumed())
  mockRetryFailed.mockResolvedValue(resumed())
  mockCancel.mockResolvedValue(undefined)
})

describe('useImportActions', () => {
  it('confirms before stopping a running import', async () => {
    // Act
    clickAction('stop')

    // Assert — stopping is not an undo, and the dialog has to say so
    expect(lastConfirm?.content).toMatch(
      /Rows already applied will stay applied/,
    )
    expect(mockCancel).not.toHaveBeenCalled()

    await lastConfirm?.onOk?.()
    await waitFor(() => expect(mockCancel).toHaveBeenCalledWith('import-1'))
  })

  it('does not promise a resume when stopping a preflight', () => {
    // Arrange
    render(<Harness preflight />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'stop' }))

    // Assert
    expect(lastConfirm?.title).toBe('Stop Check')
    expect(lastConfirm?.content).toMatch(/Nothing has been imported/)
    expect(lastConfirm?.content).not.toMatch(/resumed afterwards/)
  })

  it('resumes without a confirmation', async () => {
    // Act — nothing is undone by applying rows that were never tried
    clickAction('resume')

    // Assert
    await waitFor(() => expect(mockResume).toHaveBeenCalledWith('import-1'))
    expect(lastConfirm).toBeUndefined()
  })

  it('reports how many rows were queued', async () => {
    // Arrange
    mockResume.mockResolvedValue(resumed({ queuedRowCount: 2 }))

    // Act
    clickAction('resume')

    // Assert
    await waitFor(() =>
      expect(mockSuccess).toHaveBeenCalledWith(
        'Import resumed. 2 rows queued.',
      ),
    )
  })

  it('says when rows could not be queued because their data is gone', async () => {
    // Arrange — a resume past the retention window silently does less than asked otherwise
    mockResume.mockResolvedValue(
      resumed({ queuedRowCount: 1, skippedRowCount: 4 }),
    )

    // Act
    clickAction('resume')

    // Assert
    await waitFor(() =>
      expect(mockSuccess).toHaveBeenCalledWith(
        expect.stringContaining('4 skipped — past the retention window'),
      ),
    )
  })

  it('counts a single row without pluralising it', async () => {
    // Arrange
    mockResume.mockResolvedValue(resumed({ queuedRowCount: 1 }))

    // Act
    clickAction('resume')

    // Assert
    await waitFor(() =>
      expect(mockSuccess).toHaveBeenCalledWith('Import resumed. 1 row queued.'),
    )
  })

  it('confirms before retrying rejected rows', async () => {
    // Act
    clickAction('retry')

    // Assert
    expect(lastConfirm?.content).toMatch(
      /Rows that already succeeded are never reapplied/,
    )
    expect(lastConfirm?.content).toMatch(/3 rejected rows/)

    await lastConfirm?.onOk?.()
    await waitFor(() =>
      expect(mockRetryFailed).toHaveBeenCalledWith('import-1'),
    )
  })

  it('confirms before importing a preflight, and opens the run it starts', async () => {
    // Arrange
    mockApply.mockResolvedValue({ id: 'import-2', isTerminal: false })

    // Act
    clickAction('apply')

    // Assert — the rejected rows go in too, and the dialog has to say so
    expect(lastConfirm?.content).toMatch(/Import the 25 rows/)
    expect(lastConfirm?.content).toMatch(/rejected 3 of them/)
    expect(mockApply).not.toHaveBeenCalled()

    await lastConfirm?.onOk?.()
    await waitFor(() =>
      expect(mockPush).toHaveBeenCalledWith('/settings/imports/import-2'),
    )
    expect(mockApply).toHaveBeenCalledWith('import-1')
  })

  it('warns before importing a preflight that was already imported', async () => {
    // Arrange
    mockApply.mockResolvedValue({ id: 'import-10', isTerminal: true })

    // Act
    clickAction('apply again')

    // Assert — still allowed, since the earlier run may have failed, but not without saying what it risks
    expect(lastConfirm?.title).toBe('Import File Again')
    expect(lastConfirm?.content).toMatch(/already been imported/)
    expect(lastConfirm?.content).toMatch(/created a second time/)

    await lastConfirm?.onOk?.()
    await waitFor(() => expect(mockApply).toHaveBeenCalledWith('import-1'))
  })

  it('shows why a preflight could not be imported', async () => {
    // Arrange — past its retention window, say
    mockApply.mockRejectedValue({
      status: 400,
      detail: 'This preflight has passed its retention window.',
    })

    // Act
    clickAction('apply')
    await lastConfirm?.onOk?.()

    // Assert
    await waitFor(() =>
      expect(mockError).toHaveBeenCalledWith(
        'This preflight has passed its retention window.',
      ),
    )
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('surfaces a failure rather than reporting success', async () => {
    // Arrange
    mockResume.mockRejectedValue(new Error('nope'))

    // Act
    clickAction('resume')

    // Assert
    await waitFor(() =>
      expect(mockError).toHaveBeenCalledWith('Failed to resume the import.'),
    )
    expect(mockSuccess).not.toHaveBeenCalled()
  })
})
