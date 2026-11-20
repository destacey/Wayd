import type { UseModalFormOptions } from '@/src/hooks'
import {
  KpiCheckpointDto,
  KpiTargetDirection,
} from '@/src/services/api/wayd.schemas'
import { asCalendarDate } from '@/src/utils/calendar-date'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import KpiCheckpointPlanForm, {
  atRiskProblem,
} from './kpi-checkpoint-plan-form'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// Submit through validateFields, as the real hook does, with a real form instance.
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
          const values = await form.validateFields().catch(() => null)
          if (values && (await onSubmit(values, form))) onComplete()
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockChangePlan = vi.fn()
vi.mock('@/src/store/features/common/kpis-api', () => ({
  useChangeKpiCheckpointPlanMutation: () => [mockChangePlan],
}))

const target = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
  kpiId: 'kpi-1',
}

const q1: KpiCheckpointDto = {
  date: asCalendarDate('2026-03-31'),
  label: 'Q1',
  plannedValue: 12,
  atRiskValue: 11,
}

const q2: KpiCheckpointDto = {
  date: asCalendarDate('2026-06-30'),
  label: 'Q2',
  plannedValue: 15,
}

const renderForm = async (
  checkpoints: KpiCheckpointDto[],
  direction: KpiTargetDirection = KpiTargetDirection.Increase,
) => {
  const onFormComplete = vi.fn()
  await act(async () => {
    render(
      <KpiCheckpointPlanForm
        target={target}
        checkpoints={checkpoints}
        direction={direction}
        suffix="items"
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
  })
  return { onFormComplete }
}

const save = () => screen.getByRole('button', { name: 'Save' })

const clickSave = () => userEvent.click(save())

/** Saving is refused: the form does not validate, so nothing is sent. */
const expectSaveRefused = async () => {
  await clickSave()
  expect(mockChangePlan).not.toHaveBeenCalled()
}

const replaceValue = async (name: string, value: string) => {
  const input = screen.getByRole('spinbutton', { name })
  await userEvent.clear(input)
  await userEvent.type(input, value)
}

describe('KpiCheckpointPlanForm', () => {
  beforeEach(() => {
    mockChangePlan.mockReset()
    mockChangePlan.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('sends the whole plan back with only the change applied', async () => {
    // Arrange
    const { onFormComplete } = await renderForm([q1, q2])
    await replaceValue('Checkpoint 2 planned value', '16')

    // Act
    await clickSave()

    // Assert
    await waitFor(() =>
      expect(mockChangePlan).toHaveBeenCalledWith({
        ...target,
        request: {
          checkpoints: [
            {
              date: '2026-03-31',
              label: 'Q1',
              plannedValue: 12,
              atRiskValue: 11,
            },
            {
              date: '2026-06-30',
              label: 'Q2',
              plannedValue: 16,
              atRiskValue: undefined,
            },
          ],
        },
      }),
    )
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('clears the plan when every checkpoint is removed', async () => {
    // Arrange
    await renderForm([q1])

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Remove checkpoint 1' }),
    )
    await clickSave()

    // Assert
    await waitFor(() =>
      expect(mockChangePlan).toHaveBeenCalledWith({
        ...target,
        request: { checkpoints: [] },
      }),
    )
  })

  it('keeps what is being edited when the plan it opened with changes', async () => {
    // Arrange
    const onFormComplete = vi.fn()
    const props = {
      target,
      direction: KpiTargetDirection.Increase,
      onFormComplete,
      onFormCancel: vi.fn(),
    }
    const { rerender } = render(
      <KpiCheckpointPlanForm {...props} checkpoints={[q1]} />,
    )
    await replaceValue('Checkpoint 1 planned value', '13')

    // Act
    rerender(
      <KpiCheckpointPlanForm
        {...props}
        checkpoints={[{ ...q1, plannedValue: 20 }]}
      />,
    )

    // Assert
    expect(
      screen.getByRole('spinbutton', { name: 'Checkpoint 1 planned value' }),
    ).toHaveValue('13')
  })

  it('refuses an at-risk value on the wrong side of its planned value', async () => {
    // Arrange
    await renderForm([q1])

    // Act
    await replaceValue('Checkpoint 1 at-risk value', '13')

    // Assert
    expect(
      await screen.findByText('Must be below the planned value'),
    ).toBeInTheDocument()
    await expectSaveRefused()
  })

  it('wants an at-risk value above plan for a KPI that should go down', async () => {
    // Arrange
    await renderForm([{ ...q2, atRiskValue: 17 }], KpiTargetDirection.Decrease)

    // Act
    await replaceValue('Checkpoint 1 at-risk value', '14')

    // Assert
    expect(
      await screen.findByText('Must be above the planned value'),
    ).toBeInTheDocument()
    await expectSaveRefused()
  })

  it('refuses a blank label', async () => {
    // Arrange
    await renderForm([q1])

    // Act
    await userEvent.clear(
      screen.getByRole('textbox', { name: 'Checkpoint 1 label' }),
    )

    // Assert
    expect(await screen.findByText('Required')).toBeInTheDocument()
    await expectSaveRefused()
  })

  it('refuses two checkpoints on one day, and clears that once the other is removed', async () => {
    // Arrange — move the second checkpoint onto the first one's day
    await renderForm([q1, q2])
    const secondDate = screen.getByRole('textbox', {
      name: 'Checkpoint 2 date',
    })
    await userEvent.click(secondDate)
    await userEvent.clear(secondDate)
    await userEvent.type(secondDate, 'Mar 31, 2026{enter}')
    expect(await screen.findByText('One per day')).toBeInTheDocument()
    await expectSaveRefused()

    // Act — remove the first, so the row showing the error is left alone
    await userEvent.click(
      screen.getByRole('button', { name: 'Remove checkpoint 1' }),
    )

    // Assert
    await waitFor(() =>
      expect(screen.queryByText('One per day')).not.toBeInTheDocument(),
    )
    await clickSave()
    await waitFor(() => expect(mockChangePlan).toHaveBeenCalled())
  })

  it("puts the API's validation errors on the row that holds them", async () => {
    // Arrange
    mockChangePlan.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { 'Checkpoints[1].PlannedValue': ['Too far'] },
        }),
    })
    const { onFormComplete } = await renderForm([q1, q2])

    // Act
    await clickSave()

    // Assert
    expect(await screen.findByText('Too far')).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})

describe('atRiskProblem', () => {
  it('needs an at-risk value below plan when the KPI should go up', () => {
    // Arrange
    const direction = KpiTargetDirection.Increase

    // Act & Assert
    expect(atRiskProblem(direction, 10, 9)).toBeUndefined()
    expect(atRiskProblem(direction, 10, 10)).toBe(
      'Must be below the planned value',
    )
  })

  it('needs an at-risk value above plan when the KPI should go down', () => {
    // Arrange
    const direction = KpiTargetDirection.Decrease

    // Act & Assert
    expect(atRiskProblem(direction, 10, 11)).toBeUndefined()
    expect(atRiskProblem(direction, 10, 9)).toBe(
      'Must be above the planned value',
    )
  })

  it('accepts no at-risk value', () => {
    // Act & Assert
    expect(atRiskProblem(KpiTargetDirection.Increase, 10, null)).toBeUndefined()
  })
})
