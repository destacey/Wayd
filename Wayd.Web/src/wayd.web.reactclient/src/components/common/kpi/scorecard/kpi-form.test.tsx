import type { UseModalFormOptions } from '@/src/hooks'
import {
  KpiExpressedAs,
  KpiSource,
  KpiStatus,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
  KpiTrend,
} from '@/src/services/api/wayd.schemas'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import KpiForm from './kpi-form'

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

const mockAdd = vi.fn()
const mockUpdate = vi.fn()
vi.mock('@/src/store/features/common/kpis-api', () => ({
  useAddKpiMutation: () => [mockAdd],
  useUpdateKpiMutation: () => [mockUpdate],
  useGetKpiCategoriesQuery: () => ({ data: [] }),
}))

const subject = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
}

const kpi: KpiSummaryDto = {
  id: 'kpi-1',
  key: 7,
  name: 'Throughput',
  suffix: 'items',
  source: KpiSource.Manual,
  expressedAs: KpiExpressedAs.Value,
  status: KpiStatus.Active,
  order: 0,
  goal: {
    direction: KpiTargetDirection.Increase,
    baselineValue: 10,
    targetKind: KpiTargetKind.Absolute,
    targetValue: 20,
    resolvedTargetValue: 20,
  },
  reading: { trend: KpiTrend.NoData },
  recentValues: [],
}

const renderForm = async (editing?: KpiSummaryDto) => {
  const onFormComplete = vi.fn()
  await act(async () => {
    render(
      <KpiForm
        subject={subject}
        kpi={editing}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
  })
  return { onFormComplete }
}

describe('KpiForm', () => {
  beforeEach(() => {
    mockAdd.mockReset()
    mockUpdate.mockReset()
    mockAdd.mockReturnValue({ unwrap: () => Promise.resolve({ id: 'new' }) })
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('saves an edit in one request carrying the details, baseline and target', async () => {
    // Arrange
    const { onFormComplete } = await renderForm(kpi)
    const name = screen.getByRole('textbox', { name: /Name/ })
    await userEvent.clear(name)
    await userEvent.type(name, 'Items delivered')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockAdd).not.toHaveBeenCalled()
    expect(mockUpdate).toHaveBeenCalledTimes(1)
    expect(mockUpdate).toHaveBeenCalledWith({
      ...subject,
      kpiId: 'kpi-1',
      request: {
        name: 'Items delivered',
        description: undefined,
        categoryId: undefined,
        prefix: undefined,
        suffix: 'items',
        expressedAs: KpiExpressedAs.Value,
        baseline: { value: 10, windowStart: undefined, windowEnd: undefined },
        target: {
          direction: KpiTargetDirection.Increase,
          kind: KpiTargetKind.Absolute,
          value: 20,
          atRiskKind: undefined,
          atRiskValue: undefined,
        },
      },
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('adds a KPI with no baseline and no at-risk threshold', async () => {
    // Arrange
    await renderForm()
    await userEvent.type(
      screen.getByRole('textbox', { name: /Name/ }),
      'Escaped defects',
    )
    await userEvent.type(
      screen.getByRole('spinbutton', { name: /Target value/ }),
      '3',
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(mockAdd).toHaveBeenCalledTimes(1)
    const { request } = mockAdd.mock.calls[0][0]
    expect(request.name).toBe('Escaped defects')
    expect(request.baseline.value).toBeUndefined()
    expect(request.target).toEqual({
      direction: KpiTargetDirection.Increase,
      kind: KpiTargetKind.Absolute,
      value: 3,
      atRiskKind: undefined,
      atRiskValue: undefined,
    })
  })

  it('marks the fields a 422 names and keeps the form open', async () => {
    // Arrange
    mockUpdate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { Name: ['A KPI name can be at most 128 characters.'] },
        }),
    })
    const { onFormComplete } = await renderForm(kpi)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(
      await screen.findByText('A KPI name can be at most 128 characters.'),
    ).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
