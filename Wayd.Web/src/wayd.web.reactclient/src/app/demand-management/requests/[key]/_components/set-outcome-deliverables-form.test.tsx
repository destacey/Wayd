import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import SetOutcomeDeliverablesForm from './set-outcome-deliverables-form'
import type { UseModalFormOptions } from '@/src/hooks'

const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: vi.fn() }),
}))

// As in the other form suites: keep the form real and submit through validateFields.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({
      onSubmit,
      onComplete,
    }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          let values: TValues
          try {
            values = await form.validateFields()
          } catch {
            return
          }
          if (await onSubmit(values, form)) onComplete()
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockSet = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useSetIntakeRequestOutcomeDeliverablesMutation: () => [mockSet],
  useGetIntakeRequestProjectDeliverablesQuery: () => ({
    data: [
      {
        id: 'd-2',
        name: 'Reports',
        statusCategory: StatusCategory.Proposed,
        statusAlias: ProjectDeliverableStatusAlias.NotStarted,
      },
    ],
    isFetching: false,
    isError: false,
  }),
}))

const outcome = {
  kind: IntakeRequestOutcomeKind.ExistingProjectScope,
  targetId: 'p-1',
  project: { id: 'p-1', key: 'ATLAS', name: 'Atlas' },
  // Deleted from the project since it was mapped, so the picker no longer offers it.
  deliverables: [{ id: 'd-gone', isOnProject: false }],
} as unknown as IntakeRequestOutcomeDto

const renderForm = (onFormComplete = vi.fn(), shown = outcome) =>
  act(async () =>
    render(
      <SetOutcomeDeliverablesForm
        requestId="r-42"
        outcome={shown}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )

describe('SetOutcomeDeliverablesForm', () => {
  beforeEach(() => {
    mockSet.mockReset()
    mockError.mockReset()
  })

  it('sends the outcome whole mapping with the mapping it opened on, keeping a deleted deliverable', async () => {
    // Arrange
    mockSet.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    const onFormComplete = vi.fn()
    await renderForm(onFormComplete)
    expect(screen.getByText('Deleted from the project')).toBeInTheDocument()
    const picker = screen
      .getByText('Deliverables')
      .closest('.ant-form-item')!
      .querySelector('input')!
    await userEvent.click(picker)
    await userEvent.click(screen.getByTitle('Reports · Not Started'))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockSet).toHaveBeenCalledWith({
      requestId: 'r-42',
      targetId: 'p-1',
      deliverableIds: ['d-gone', 'd-2'],
      expectedDeliverableIds: ['d-gone'],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('refuses to leave an outcome with no note mapped to nothing', async () => {
    // Arrange
    await renderForm(vi.fn(), { ...outcome, note: undefined, deliverables: [] })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockSet).not.toHaveBeenCalled()
    expect(
      await screen.findByText(/This outcome has no note/),
    ).toBeInTheDocument()
  })

  it('refuses to leave an outcome with no note mapped only to deliverables deleted from the project', async () => {
    // Arrange
    await renderForm(vi.fn(), { ...outcome, note: undefined })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockSet).not.toHaveBeenCalled()
    expect(
      await screen.findByText(/This outcome has no note/),
    ).toBeInTheDocument()
  })

  it('clears the mapping of an outcome with a note', async () => {
    // Arrange
    mockSet.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm(vi.fn(), {
      ...outcome,
      note: 'The export screen',
      deliverables: [],
    })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockSet).toHaveBeenCalledWith(
      expect.objectContaining({ deliverableIds: [] }),
    )
  })

  it('closes and says why when another change landed first', async () => {
    // Arrange
    mockSet.mockReturnValue({
      unwrap: () =>
        Promise.reject({ status: 409, detail: 'The deliverables changed.' }),
    })
    const onFormComplete = vi.fn()
    await renderForm(onFormComplete, { ...outcome, note: 'Export' })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockError).toHaveBeenCalledWith('The deliverables changed.')
    expect(onFormComplete).toHaveBeenCalled()
  })
})
