import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  ImportAtomicity,
  ImportProcessDto,
  ImportProcessStatus,
} from '@/src/services/wayd-api'
import CsvImportForm from './csv-import-form'

const push = jest.fn()
const successMessage = jest.fn()
const infoMessage = jest.fn()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
}))

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: successMessage,
    info: infoMessage,
    error: jest.fn(),
  }),
}))

const run = (
  status: ImportProcessStatus,
  counts: { succeeded?: number; failed?: number } = {},
): ImportProcessDto => ({
  id: 'run-1',
  importType: 'versions',
  displayName: 'Versions',
  atomicity: ImportAtomicity.Atomic,
  status,
  submittedByUserId: 'user-1',
  submittedOn: new Date('2026-09-10T09:00:00Z'),
  totalRowCount: 25,
  succeededRowCount: counts.succeeded ?? 0,
  failedRowCount: counts.failed ?? 0,
  unappliedRowCount: 25 - (counts.succeeded ?? 0) - (counts.failed ?? 0),
  isTerminal:
    status !== ImportProcessStatus.Queued &&
    status !== ImportProcessStatus.Processing &&
    status !== ImportProcessStatus.Cancelling,
  canManage: true,
})

const onFormComplete = jest.fn()

const renderForm = (answer: ImportProcessDto) =>
  render(
    <CsvImportForm
      title="Import Versions"
      columns="ImportId,Name"
      onImport={async () => answer}
      successMessage="Versions imported."
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

const importFile = async () => {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement
  await userEvent.upload(
    input,
    new File(['ImportId,Name\n1,One'], 'versions.csv', { type: 'text/csv' }),
  )

  // Inside act: the outcome is set after the import resolves, and jest.setup stubs the MessageChannel
  // React schedules with, so an update landing outside act is never rendered.
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Import' }))
  })
}

describe('CsvImportForm', () => {
  beforeEach(() => {
    push.mockReset()
    successMessage.mockReset()
    infoMessage.mockReset()
    onFormComplete.mockReset()
  })

  it('closes with the success message when every row was applied', async () => {
    // Arrange
    renderForm(run(ImportProcessStatus.Succeeded, { succeeded: 25 }))

    // Act
    await importFile()

    // Assert
    expect(onFormComplete).toHaveBeenCalled()
    expect(successMessage).toHaveBeenCalledWith('Versions imported.')
    expect(push).not.toHaveBeenCalled()
  })

  it('stays open to report a run that applied nothing, linking to its rows', async () => {
    // Arrange — the case a bare "submitted" used to hide
    renderForm(run(ImportProcessStatus.Failed, { failed: 25 }))

    // Act
    await importFile()

    // Assert
    expect(screen.getByText('Nothing was imported')).toBeInTheDocument()
    expect(screen.getByText('25 rows were rejected.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View Details' })).toHaveAttribute(
      'href',
      '/settings/imports/run-1',
    )
    expect(onFormComplete).not.toHaveBeenCalled()
    expect(successMessage).not.toHaveBeenCalled()
  })

  it('reports how much of a partly applied run landed', async () => {
    // Arrange
    renderForm(
      run(ImportProcessStatus.PartiallySucceeded, { succeeded: 18, failed: 7 }),
    )

    // Act
    await importFile()

    // Assert
    expect(screen.getByText('18 of 25 rows were imported')).toBeInTheDocument()
    expect(screen.getByText('7 rows were rejected.')).toBeInTheDocument()
  })

  it('hands a run that is still going over to its page', async () => {
    // Arrange
    renderForm(run(ImportProcessStatus.Processing))

    // Act
    await importFile()

    // Assert
    expect(push).toHaveBeenCalledWith('/settings/imports/run-1')
    expect(onFormComplete).toHaveBeenCalled()
    expect(successMessage).not.toHaveBeenCalled()
  })
})
