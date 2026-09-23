import { act, fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { BacklogHealthThresholds } from '@/src/services/wayd-api'
import BacklogHealthSettingsPopover from './backlog-health-settings-popover'

const thresholds: BacklogHealthThresholds = {
  staleDays: 90,
  oldProposedDays: 180,
  agingWipPercentile: 85,
  oversizedPercentile: 85,
  readinessWindowWeeks: 4,
  readinessFallbackItems: 20,
  atRiskPercent: 10,
  unhealthyPercent: 25,
  runwayAtRiskWeeks: 4,
  runwayUnhealthyWeeks: 2,
  runwayTooLongWeeks: 26,
  netFlowAtRisk: 1.2,
  netFlowUnhealthy: 1.5,
  wipLoadAtRisk: 1.5,
  wipLoadUnhealthy: 2,
}

const effective = { lookbackDays: 90, thresholds }

const openPopover = async () => {
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: /Thresholds/ }))
  })
}

const changeInput = async (label: string, value: string) => {
  const input = screen.getByLabelText(label)
  await act(async () => {
    fireEvent.change(input, { target: { value } })
    fireEvent.blur(input)
  })
}

describe('BacklogHealthSettingsPopover', () => {
  it('applies only the values that were changed', async () => {
    const onChange = jest.fn()
    render(
      <BacklogHealthSettingsPopover
        settings={{ thresholds: {} }}
        effective={effective}
        onChange={onChange}
      />,
    )

    await openPopover()
    await changeInput('Stale after', '30')
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    })

    expect(onChange).toHaveBeenCalledWith({
      lookbackDays: undefined,
      thresholds: { staleDays: 30 },
    })
  })

  it('keeps earlier overrides when applying another', async () => {
    const onChange = jest.fn()
    render(
      <BacklogHealthSettingsPopover
        settings={{ lookbackDays: 60, thresholds: { staleDays: 30 } }}
        effective={{
          lookbackDays: 60,
          thresholds: { ...thresholds, staleDays: 30 },
        }}
        onChange={onChange}
      />,
    )

    await openPopover()
    await changeInput('History', '120')
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    })

    expect(onChange).toHaveBeenCalledWith({
      lookbackDays: 120,
      thresholds: { staleDays: 30 },
    })
  })

  it('resets to the defaults', async () => {
    const onChange = jest.fn()
    render(
      <BacklogHealthSettingsPopover
        settings={{ thresholds: { staleDays: 30 } }}
        effective={effective}
        onChange={onChange}
      />,
    )

    await openPopover()
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Reset to defaults' }))
    })

    expect(onChange).toHaveBeenCalledWith({ thresholds: {} })
  })

  it('marks custom thresholds', () => {
    render(
      <BacklogHealthSettingsPopover
        settings={{ thresholds: { staleDays: 30 } }}
        effective={effective}
        onChange={jest.fn()}
      />,
    )

    expect(screen.getByText('Custom thresholds')).toBeInTheDocument()
  })

  it('is disabled until a report has loaded', () => {
    render(
      <BacklogHealthSettingsPopover
        settings={{ thresholds: {} }}
        onChange={jest.fn()}
      />,
    )

    expect(screen.getByRole('button', { name: /Thresholds/ })).toBeDisabled()
    expect(screen.queryByText('Custom thresholds')).not.toBeInTheDocument()
  })
})
