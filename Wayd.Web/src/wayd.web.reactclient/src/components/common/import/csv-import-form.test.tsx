import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  ImportAtomicity,
  ImportDefinitionDto,
  ImportProcessDto,
  ImportProcessStatus,
} from '@/src/services/wayd-api'
import CsvImportForm from './csv-import-form'
import { importTemplates } from './import-templates.generated'

const push = jest.fn()
const successMessage = jest.fn()
const infoMessage = jest.fn()
const submitImport = jest.fn()
const downloadCsv = jest.fn()

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

jest.mock('@/src/store/features/admin/imports-api', () => ({
  useSubmitImportMutation: () => [submitImport],
}))

jest.mock('@/src/utils/csv-utils', () => ({
  ...jest.requireActual('@/src/utils/csv-utils'),
  downloadCsv: (...args: unknown[]) => downloadCsv(...args),
}))

const definition = (
  key: string,
  displayName: string,
  overrides: Partial<ImportDefinitionDto> = {},
): ImportDefinitionDto => ({
  key,
  displayName,
  atomicity: ImportAtomicity.Atomic,
  maxRows: 10_000,
  canSubmit: true,
  ...overrides,
})

const definitions = [
  definition('strategic.themes', 'Strategic Themes'),
  definition('product-management.release-packages', 'Release Packages', {
    atomicity: ImportAtomicity.PerRow,
  }),
  definition('employees', 'Employees', { canSubmit: false }),
  // Known to the server but not to this client: nothing to post it with, so it is not offered.
  definition('not-generated', 'Not Generated'),
]

const run = (
  status: ImportProcessStatus,
  counts: { succeeded?: number; failed?: number } = {},
): ImportProcessDto => ({
  id: 'run-1',
  importType: 'strategic.themes',
  displayName: 'Strategic Themes',
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

const renderForm = () =>
  render(
    <CsvImportForm
      definitions={definitions}
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

const answerWith = (answer: ImportProcessDto) =>
  submitImport.mockReturnValue({ unwrap: () => Promise.resolve(answer) })

const refuseWith = (error: unknown) =>
  submitImport.mockReturnValue({ unwrap: () => Promise.reject(error) })

const chooseImport = async (displayName: string) => {
  await userEvent.click(screen.getByRole('combobox'))
  await userEvent.click(await screen.findByTitle(displayName))
}

const csv = (name: string) =>
  new File(['Name\nOne'], name, { type: 'text/csv' })

const fileInputs = () =>
  Array.from(
    document.querySelectorAll('input[type="file"]'),
  ) as HTMLInputElement[]

const importButton = () => screen.getByRole('button', { name: 'Import' })

const importThemesFile = async () => {
  await chooseImport('Strategic Themes')
  await userEvent.upload(fileInputs()[0], csv('themes.csv'))

  // Inside act: the outcome is set after the import resolves, and jest.setup stubs the MessageChannel
  // React schedules with, so an update landing outside act is never rendered.
  await act(async () => {
    fireEvent.click(importButton())
  })
}

describe('CsvImportForm', () => {
  beforeEach(() => {
    push.mockReset()
    successMessage.mockReset()
    infoMessage.mockReset()
    submitImport.mockReset()
    downloadCsv.mockReset()
    onFormComplete.mockReset()
  })

  it('offers only the imports the viewer may submit and the client can post, by name', async () => {
    // Arrange
    renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert — the visible items; antd's accessible listbox names options by value
    await screen.findByTitle('Strategic Themes')
    const options = Array.from(
      document.querySelectorAll('.ant-select-item-option'),
    ).map((o) => o.getAttribute('title'))
    expect(options).toEqual(['Release Packages', 'Strategic Themes'])
  })

  it('lists imports under their area, with strategic themes under PPM', async () => {
    // Arrange
    renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    await screen.findByTitle('Strategic Themes')
    const items = Array.from(
      document.querySelectorAll(
        '.ant-select-item-group, .ant-select-item-option',
      ),
    ).map((item) => item.getAttribute('title'))
    expect(items).toEqual([
      'Product Management',
      'Release Packages',
      'Project Portfolio Management',
      'Strategic Themes',
    ])
  })

  it('searches imports by name, not by the area they are grouped under', async () => {
    // Arrange
    renderForm()
    const search = screen.getByRole('combobox')

    // Act — "Management" names both groups but neither import
    await userEvent.type(search, 'Management')

    // Assert
    expect(document.querySelectorAll('.ant-select-item-option')).toHaveLength(0)
    await userEvent.clear(search)
    await userEvent.type(search, 'themes')
    const options = Array.from(
      document.querySelectorAll('.ant-select-item-option'),
    ).map((o) => o.getAttribute('title'))
    expect(options).toEqual(['Strategic Themes'])
  })

  it('hands over a template carrying every column of the file', async () => {
    // Arrange
    renderForm()
    await chooseImport('Strategic Themes')

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: /Download Template/ }),
    )

    // Assert
    const [content, fileName] = downloadCsv.mock.calls[0]
    const expected = importTemplates['strategic.themes'].files[0].columns
      .map((c) => c.name)
      .join(',')
    expect(content).toBe(expected)
    expect(fileName).toBe('strategic-themes-import-template.csv')
  })

  it('names a secondary file template after its import as well', async () => {
    // Arrange
    renderForm()
    await chooseImport('Release Packages')

    // Act
    await userEvent.click(
      screen.getAllByRole('button', { name: /Download Template/ })[1],
    )

    // Assert
    expect(downloadCsv.mock.calls[0][1]).toBe(
      'release-packages-manifest-import-template.csv',
    )
  })

  it('will not import until every required file is chosen', async () => {
    // Arrange — the packages import needs its manifest as well
    renderForm()
    await chooseImport('Release Packages')

    // Act
    await userEvent.upload(fileInputs()[0], csv('packages.csv'))

    // Assert
    expect(importButton()).toBeDisabled()
    await userEvent.upload(fileInputs()[1], csv('manifest.csv'))
    expect(importButton()).toBeEnabled()
  })

  it('posts each file under the field its endpoint reads', async () => {
    // Arrange
    answerWith(run(ImportProcessStatus.Succeeded, { succeeded: 25 }))
    renderForm()
    await chooseImport('Release Packages')
    const packages = csv('packages.csv')
    const manifest = csv('manifest.csv')
    await userEvent.upload(fileInputs()[0], packages)
    await userEvent.upload(fileInputs()[1], manifest)

    // Act
    await act(async () => {
      fireEvent.click(importButton())
    })

    // Assert
    expect(submitImport).toHaveBeenCalledWith({
      importKey: 'product-management.release-packages',
      files: { file: packages, manifestFile: manifest },
    })
  })

  it('says whether a rejected row stops the whole file', async () => {
    // Arrange
    renderForm()

    // Act
    await chooseImport('Strategic Themes')

    // Assert
    expect(screen.getByText('All or nothing')).toBeInTheDocument()
  })

  it('closes with the success message when every row was applied', async () => {
    // Arrange
    answerWith(run(ImportProcessStatus.Succeeded, { succeeded: 25 }))
    renderForm()

    // Act
    await importThemesFile()

    // Assert
    expect(onFormComplete).toHaveBeenCalled()
    expect(successMessage).toHaveBeenCalledWith('Strategic Themes imported.')
    expect(push).not.toHaveBeenCalled()
  })

  it('stays open to report a run that applied nothing, linking to its rows', async () => {
    // Arrange
    answerWith(run(ImportProcessStatus.Failed, { failed: 25 }))
    renderForm()

    // Act
    await importThemesFile()

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
    answerWith(
      run(ImportProcessStatus.PartiallySucceeded, { succeeded: 18, failed: 7 }),
    )
    renderForm()

    // Act
    await importThemesFile()

    // Assert
    expect(screen.getByText('18 of 25 rows were imported')).toBeInTheDocument()
    expect(screen.getByText('7 rows were rejected.')).toBeInTheDocument()
  })

  it('hands a run that is still going over to its page', async () => {
    // Arrange
    answerWith(run(ImportProcessStatus.Processing))
    renderForm()

    // Act
    await importThemesFile()

    // Assert
    expect(push).toHaveBeenCalledWith('/settings/imports/run-1')
    expect(onFormComplete).toHaveBeenCalled()
    expect(successMessage).not.toHaveBeenCalled()
  })

  it('shows every row error of a refused file, so each can be fixed', async () => {
    // Arrange
    refuseWith({
      status: 422,
      errors: { Name: ["'Name' must not be empty. (Row 3)"] },
    })
    renderForm()

    // Act
    await importThemesFile()

    // Assert
    const alert = screen
      .getByText('The import was rejected')
      .closest('.ant-alert')
    expect(
      within(alert as HTMLElement).getByText(
        "'Name' must not be empty. (Row 3)",
      ),
    ).toBeInTheDocument()
  })
})
