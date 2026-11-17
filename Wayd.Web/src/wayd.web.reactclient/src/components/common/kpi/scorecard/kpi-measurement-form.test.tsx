import type { UseModalFormOptions } from '@/src/hooks'
import {
  KpiMeasurementDto,
  KpiMeasurementSource,
  KpiPeriodRecordKind,
} from '@/src/services/api/wayd.schemas'
import { asCalendarDate } from '@/src/utils/calendar-date'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import KpiMeasurementForm from './kpi-measurement-form'

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

const mockRecord = vi.fn()
const mockChange = vi.fn()
vi.mock('@/src/store/features/common/kpis-api', () => ({
  useRecordKpiMeasurementMutation: () => [mockRecord],
  useChangeKpiMeasurementMutation: () => [mockChange],
}))

const target = {
  subjectType: 'organization.team' as const,
  subjectId: 'team-1',
  kpiId: 'kpi-1',
}

const measurement = {
  id: 'm-1',
  value: 14,
  periodStart: asCalendarDate('2026-10-01'),
  periodEnd: asCalendarDate('2026-10-14'),
  periodRecordKind: KpiPeriodRecordKind.Sprint,
  periodRecordId: 'sprint-9',
  measuredOn: asCalendarDate('2026-10-15'),
  source: KpiMeasurementSource.Manual,
  sampleSize: 8,
  note: 'Sprint 9',
} as KpiMeasurementDto

const renderForm = async () => {
  const onFormComplete = vi.fn()
  await act(async () => {
    render(
      <KpiMeasurementForm
        target={target}
        suffix="items"
        measurement={measurement}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
  })
  return { onFormComplete }
}

describe('KpiMeasurementForm', () => {
  beforeEach(() => {
    mockRecord.mockReset()
    mockChange.mockReset()
    mockChange.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('corrects a measurement, keeping the sprint it covers and the day it was taken', async () => {
    // Arrange
    const { onFormComplete } = await renderForm()
    const value = screen.getByRole('spinbutton', { name: /Value/ })
    await userEvent.clear(value)
    await userEvent.type(value, '15')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockRecord).not.toHaveBeenCalled()
    expect(mockChange).toHaveBeenCalledWith({
      ...target,
      measurementId: 'm-1',
      request: {
        value: 15,
        periodStart: '2026-10-01',
        periodEnd: '2026-10-14',
        sampleSize: 8,
        note: 'Sprint 9',
        periodRecordKind: KpiPeriodRecordKind.Sprint,
        periodRecordId: 'sprint-9',
        measuredOn: '2026-10-15',
      },
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('shows a period error the API reports against either end on the period field', async () => {
    // Arrange
    mockChange.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: {
            'Measurement.PeriodEnd': ['The period ends before it starts.'],
          },
        }),
    })
    const { onFormComplete } = await renderForm()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(
      await screen.findByText('The period ends before it starts.'),
    ).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
