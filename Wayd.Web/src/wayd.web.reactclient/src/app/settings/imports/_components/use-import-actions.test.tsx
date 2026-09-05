import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { Button } from 'antd'
import { ResumedImport } from '@/src/services/wayd-api'
import useImportActions from './use-import-actions'

const mockCancel = jest.fn()
const mockResume = jest.fn()
const mockRetryFailed = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()

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
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

const importProcess = {
  id: 'import-1',
  displayName: 'Employee Import',
  failedRowCount: 3,
  unappliedRowCount: 2,
}

const resumed = (overrides: Partial<ResumedImport> = {}): ResumedImport =>
  ({ queuedRowCount: 2, skippedRowCount: 0, ...overrides }) as ResumedImport

/** Renders one button per action so a test can invoke it. */
const Harness = () => {
  const { handleCancel, handleResume, handleRetryFailed } = useImportActions()

  return (
    <>
      <Button onClick={() => handleCancel(importProcess)}>stop</Button>
      <Button onClick={() => handleResume(importProcess)}>resume</Button>
      <Button onClick={() => handleRetryFailed(importProcess)}>retry</Button>
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
    expect(lastConfirm?.content).toMatch(/Rows already applied will stay applied/)
    expect(mockCancel).not.toHaveBeenCalled()

    await lastConfirm?.onOk?.()
    await waitFor(() => expect(mockCancel).toHaveBeenCalledWith('import-1'))
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
      expect(mockSuccess).toHaveBeenCalledWith('Import resumed. 2 rows queued.'),
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
    await waitFor(() => expect(mockRetryFailed).toHaveBeenCalledWith('import-1'))
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
