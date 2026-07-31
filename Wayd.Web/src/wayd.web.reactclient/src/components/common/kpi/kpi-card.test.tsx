import { render, screen, fireEvent } from '@testing-library/react'
import KpiCard, { AddKpiCard, KpiCardData, KpiCardCheckpoint } from './kpi-card'
import {
  KpiHealth,
  KpiTargetDirection,
  KpiTrend,
} from '@/src/services/wayd-api'

// ─── Mock dayjs ──────────────────────────────────────────────────────────────
// Allows controlling "now" for checkpoint derivation tests.

jest.mock('dayjs', () => {
  const originalDayjs = jest.requireActual('dayjs')
  const mockDayjs = (date?: string | Date) => {
    if (date === undefined) {
      return originalDayjs(mockDayjs.mockedNow)
    }
    return originalDayjs(date)
  }
  mockDayjs.mockedNow = '2026-02-15T12:00:00'
  Object.assign(mockDayjs, originalDayjs)
  return mockDayjs
})

// ─── Mock useTheme ───────────────────────────────────────────────────────────

jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    currentTheme: 'wayd',
    currentMode: 'dark',
    token: {
      colorSuccess: '#49aa19',
      colorSuccessBg: '#162312',
      colorSuccessBorder: '#274916',
      colorWarning: '#d89614',
      colorWarningBg: '#2b2111',
      colorWarningBorder: '#594214',
      colorError: '#dc4446',
      colorErrorBg: '#2c1618',
      colorErrorBorder: '#5b2526',
      colorTextTertiary: 'rgba(255,255,255,0.45)',
      colorTextSecondary: 'rgba(255,255,255,0.65)',
      colorFillQuaternary: 'rgba(255,255,255,0.04)',
      colorBorderSecondary: '#424242',
      colorPrimary: '#1677ff',
    },
  }),
}))

// ─── Test data factories ─────────────────────────────────────────────────────

function makeKpiData(overrides: Partial<KpiCardData> = {}): KpiCardData {
  return {
    id: 'kpi-1',
    key: 100,
    name: 'Test KPI',
    targetValue: 80,
    suffix: '%',
    targetDirection: KpiTargetDirection.Increase,
    ...overrides,
  }
}

function makeCheckpoint(
  overrides: Partial<KpiCardCheckpoint> = {},
): KpiCardCheckpoint {
  return {
    label: 'Jan',
    date: new Date('2025-01-31').toISOString(),
    targetValue: 50,
    ...overrides,
  }
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('KpiCard', () => {
  // ─── Basic rendering ─────────────────────────────────────────────────────

  it('renders key, name, and target value', () => {
    render(<KpiCard data={makeKpiData()} />)

    expect(screen.getByText('#100')).toBeInTheDocument()
    expect(screen.getByText('Test KPI')).toBeInTheDocument()
    expect(screen.getByText('80%')).toBeInTheDocument()
  })

  it('renders actual value when provided', () => {
    render(<KpiCard data={makeKpiData({ actualValue: 65 })} />)

    expect(screen.getByText('65%')).toBeInTheDocument()
  })

  it('renders dash when no actual value', () => {
    render(<KpiCard data={makeKpiData({ actualValue: undefined })} />)

    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('renders Increase direction badge', () => {
    render(
      <KpiCard
        data={makeKpiData({ targetDirection: KpiTargetDirection.Increase })}
      />,
    )

    expect(screen.getByText('Increase')).toBeInTheDocument()
  })

  it('renders Decrease direction badge', () => {
    render(
      <KpiCard
        data={makeKpiData({ targetDirection: KpiTargetDirection.Decrease })}
      />,
    )

    expect(screen.getByText('Decrease')).toBeInTheDocument()
  })

  // ─── Value formatting ────────────────────────────────────────────────────

  it('formats values with prefix', () => {
    render(
      <KpiCard
        data={makeKpiData({
          prefix: '$',
          suffix: undefined,
          targetValue: 1000,
          actualValue: 750,
        })}
      />,
    )

    expect(screen.getByText('$750')).toBeInTheDocument()
    expect(screen.getByText('$1,000')).toBeInTheDocument()
  })

  it('formats values with suffix', () => {
    render(
      <KpiCard data={makeKpiData({ actualValue: 42 })} />,
    )

    expect(screen.getByText('42%')).toBeInTheDocument()
  })

  it('formats values without prefix or suffix', () => {
    render(
      <KpiCard
        data={makeKpiData({
          prefix: undefined,
          suffix: undefined,
          targetValue: 100,
          actualValue: 75,
        })}
      />,
    )

    expect(screen.getByText('75')).toBeInTheDocument()
    expect(screen.getByText('100')).toBeInTheDocument()
  })

  // ─── isRegressed logic ───────────────────────────────────────────────────

  describe('regression detection', () => {
    it('does not regress when startingValue is undefined', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: undefined,
            actualValue: 10,
            targetDirection: KpiTargetDirection.Increase,
          })}
        />,
      )

      // Should not show regression text
      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })

    it('does not regress when startingValue is null', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: null as unknown as undefined,
            actualValue: 10,
            targetDirection: KpiTargetDirection.Increase,
          })}
        />,
      )

      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })

    it('does not regress when actualValue is undefined', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 50,
            actualValue: undefined,
          })}
        />,
      )

      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })

    it('does not regress when actualValue is null', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 50,
            actualValue: null as unknown as undefined,
          })}
        />,
      )

      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })

    it('does not regress for Increase KPI when actual >= starting', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 30,
            actualValue: 50,
            targetDirection: KpiTargetDirection.Increase,
            progress: 0.5,
          })}
        />,
      )

      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })

    it('does not regress for Decrease KPI when actual <= starting', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 80,
            actualValue: 60,
            targetDirection: KpiTargetDirection.Decrease,
            progress: 0.5,
          })}
        />,
      )

      expect(screen.queryByText(/Regressed/)).not.toBeInTheDocument()
    })
  })

  // ─── Progress states ─────────────────────────────────────────────────────

  describe('progress section', () => {
    it('shows "Awaiting first measurement" when no actual value', () => {
      render(<KpiCard data={makeKpiData({ actualValue: undefined })} />)

      expect(
        screen.getByText('Awaiting first measurement'),
      ).toBeInTheDocument()
    })

    it('shows "No baseline" when actual exists but no progress', () => {
      render(
        <KpiCard
          data={makeKpiData({ actualValue: 50, progress: undefined })}
        />,
      )

      expect(
        screen.getByText(/No baseline · progress unavailable/),
      ).toBeInTheDocument()
    })

    it('shows normal progress bar for positive progress', () => {
      const { container } = render(
        <KpiCard
          data={makeKpiData({
            startingValue: 20,
            actualValue: 60,
            progress: 0.67,
          })}
        />,
      )

      expect(container.querySelector('.ant-progress')).toBeInTheDocument()
      expect(screen.getByText('67%')).toBeInTheDocument()
      expect(screen.getByText('Baseline: 20%')).toBeInTheDocument()
    })

    it('shows severe regression for progress below -15%', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 50,
            actualValue: 30,
            progress: -0.33,
            targetDirection: KpiTargetDirection.Increase,
          })}
        />,
      )

      expect(screen.getByText(/Regressed/)).toBeInTheDocument()
      expect(screen.getByText(/30%.*vs baseline.*50%/)).toBeInTheDocument()
    })

    it('shows slight regression for progress between -15% and 0%', () => {
      render(
        <KpiCard
          data={makeKpiData({
            startingValue: 50,
            actualValue: 45,
            progress: -0.08,
          })}
        />,
      )

      expect(screen.getByText(/Below baseline/)).toBeInTheDocument()
    })
  })

  // ─── Health badge ────────────────────────────────────────────────────────

  describe('health badge', () => {
    it('does not show health badge when no health available', () => {
      render(<KpiCard data={makeKpiData()} />)

      expect(screen.queryByText('No Data')).not.toBeInTheDocument()
      expect(screen.queryByText('Healthy')).not.toBeInTheDocument()
      expect(screen.queryByText('At Risk')).not.toBeInTheDocument()
      expect(screen.queryByText('Unhealthy')).not.toBeInTheDocument()
    })

    it('shows "Healthy" when data.health is Healthy', () => {
      render(
        <KpiCard data={makeKpiData({ health: KpiHealth.Healthy })} />,
      )

      expect(screen.getByText('Healthy')).toBeInTheDocument()
    })

    it('shows "At Risk" when data.health is AtRisk', () => {
      render(
        <KpiCard data={makeKpiData({ health: KpiHealth.AtRisk })} />,
      )

      expect(screen.getByText('At Risk')).toBeInTheDocument()
    })

    it('shows "Unhealthy" when data.health is Unhealthy', () => {
      render(
        <KpiCard data={makeKpiData({ health: KpiHealth.Unhealthy })} />,
      )

      expect(screen.getByText('Unhealthy')).toBeInTheDocument()
    })
  })

  // ─── Checkpoint derivation ───────────────────────────────────────────────

  describe('checkpoint derivation', () => {
    const pastDate = new Date('2026-01-31').toISOString()
    const futureDate = new Date('2026-03-31').toISOString()

    it('shows last measured checkpoint in checkpoint strip', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Jan',
          date: pastDate,
          targetValue: 50,
          actualValue: 49,
          health: KpiHealth.Healthy,
        }),
        makeCheckpoint({
          label: 'Feb',
          date: futureDate,
          targetValue: 45,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 49 })}
          checkpoints={checkpoints}
        />,
      )

      // Checkpoint strip shows the past measured checkpoint
      expect(screen.getByText(/Target: 50%/)).toBeInTheDocument()
      expect(screen.getByText(/✓ Met \(49%\)/)).toBeInTheDocument()
    })

    it('shows next checkpoint in footer', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Jan',
          date: pastDate,
          targetValue: 50,
          actualValue: 49,
          health: KpiHealth.Healthy,
        }),
        makeCheckpoint({
          label: 'Feb',
          date: futureDate,
          targetValue: 45,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 49 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/Next checkpoint: Feb/)).toBeInTheDocument()
      expect(screen.getByText(/45%/)).toBeInTheDocument()
    })

    it('shows "No checkpoints" when no checkpoints provided', () => {
      render(<KpiCard data={makeKpiData()} />)

      expect(screen.getByText('No checkpoints')).toBeInTheDocument()
    })

    it('shows loading skeleton when checkpoints are loading', () => {
      const { container } = render(
        <KpiCard data={makeKpiData()} checkpointLoading />,
      )

      expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    })
  })

  // ─── Health source priority ──────────────────────────────────────────────

  describe('health source priority', () => {
    const pastDate = new Date('2026-01-31').toISOString()
    const futureDate = new Date('2026-03-31').toISOString()

    it('uses data.health when available (priority 1)', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          actualValue: 60,
          health: KpiHealth.Unhealthy,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({
            actualValue: 60,
            health: KpiHealth.Healthy,
          })}
          checkpoints={checkpoints}
        />,
      )

      // data.health (Healthy) should win over checkpoint (Unhealthy)
      expect(screen.getByText('Healthy')).toBeInTheDocument()
    })

    it('uses nextCheckpoint health when it has measurement (priority 2)', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Jan',
          date: pastDate,
          actualValue: 45,
          health: KpiHealth.Unhealthy,
        }),
        makeCheckpoint({
          label: 'Feb',
          date: futureDate,
          targetValue: 55,
          actualValue: 60,
          health: KpiHealth.Healthy,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 60 })}
          checkpoints={checkpoints}
        />,
      )

      // nextCheckpoint has measurement + health, should win over checkpoint
      expect(screen.getByText('Healthy')).toBeInTheDocument()
    })

    it('falls back to checkpoint health when nextCheckpoint has no measurement (priority 3)', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Jan',
          date: pastDate,
          actualValue: 49,
          health: KpiHealth.Healthy,
        }),
        makeCheckpoint({
          label: 'Feb',
          date: futureDate,
          targetValue: 45,
          // No measurement — no actualValue, health, or trend
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 49 })}
          checkpoints={checkpoints}
        />,
      )

      // Should fall back to checkpoint (Jan) health = Healthy
      expect(screen.getByText('Healthy')).toBeInTheDocument()
    })

    it('does not show health badge when no health source exists', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Feb',
          date: futureDate,
          targetValue: 45,
          // No measurement
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 30 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.queryByText('No Data')).not.toBeInTheDocument()
      expect(screen.queryByText('Healthy')).not.toBeInTheDocument()
      expect(screen.queryByText('At Risk')).not.toBeInTheDocument()
      expect(screen.queryByText('Unhealthy')).not.toBeInTheDocument()
    })
  })

  // ─── Trend label ─────────────────────────────────────────────────────────

  describe('trend label', () => {
    const pastDate = new Date('2026-01-31').toISOString()

    it('does not render trend when no trend data', () => {
      render(<KpiCard data={makeKpiData({ actualValue: 50 })} />)

      expect(
        screen.queryByText(/since last checkpoint/),
      ).not.toBeInTheDocument()
    })

    it('does not render trend for NoData', () => {
      render(
        <KpiCard
          data={makeKpiData({ actualValue: 50, trend: KpiTrend.NoData })}
        />,
      )

      expect(
        screen.queryByText(/since last checkpoint/),
      ).not.toBeInTheDocument()
    })

    it('renders Improving trend from checkpoint', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          actualValue: 50,
          health: KpiHealth.Healthy,
          trend: KpiTrend.Improving,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 50 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/Improving/)).toBeInTheDocument()
      expect(
        screen.getByText(/since last checkpoint/),
      ).toBeInTheDocument()
    })

    it('renders Worsening trend', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          actualValue: 50,
          health: KpiHealth.Unhealthy,
          trend: KpiTrend.Worsening,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 50 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/Worsening/)).toBeInTheDocument()
    })

    it('renders Stable trend', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          actualValue: 50,
          health: KpiHealth.Healthy,
          trend: KpiTrend.Stable,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 50 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/Stable/)).toBeInTheDocument()
    })
  })

  // ─── Checkpoint strip ────────────────────────────────────────────────────

  describe('checkpoint strip', () => {
    const pastDate = new Date('2026-01-31').toISOString()

    it('shows "Met" for healthy checkpoint', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          targetValue: 50,
          actualValue: 55,
          health: KpiHealth.Healthy,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 55 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/✓ Met/)).toBeInTheDocument()
    })

    it('shows "Missed" for at-risk checkpoint', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          targetValue: 60,
          actualValue: 55,
          health: KpiHealth.AtRisk,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 55 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/! Missed/)).toBeInTheDocument()
    })

    it('shows "Off Track" for unhealthy checkpoint', () => {
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          date: pastDate,
          targetValue: 70,
          actualValue: 40,
          health: KpiHealth.Unhealthy,
        }),
      ]

      render(
        <KpiCard
          data={makeKpiData({ actualValue: 40 })}
          checkpoints={checkpoints}
        />,
      )

      expect(screen.getByText(/✕ Off Track/)).toBeInTheDocument()
    })

    it('shows "Upcoming" for checkpoint with no measurement', () => {
      const futureDate = new Date('2026-03-31').toISOString()
      const checkpoints: KpiCardCheckpoint[] = [
        makeCheckpoint({
          label: 'Mar',
          date: futureDate,
          targetValue: 60,
          // no actualValue
        }),
      ]

      render(
        <KpiCard data={makeKpiData()} checkpoints={checkpoints} />,
      )

      // nextCheckpoint goes to footer; no past checkpoint → no strip
      expect(screen.getByText(/Next checkpoint: Mar/)).toBeInTheDocument()
    })
  })

  // ─── Footer ──────────────────────────────────────────────────────────────

  describe('footer', () => {
    it('shows "Measured" when has actual value', () => {
      render(<KpiCard data={makeKpiData({ actualValue: 50 })} />)

      expect(screen.getByText('Measured')).toBeInTheDocument()
    })

    it('shows "Never measured" when no actual value', () => {
      render(<KpiCard data={makeKpiData()} />)

      expect(screen.getByText('Never measured')).toBeInTheDocument()
    })
  })

  // ─── Click behavior ──────────────────────────────────────────────────────

  describe('click behavior', () => {
    it('calls onPress with KPI id when clicked', () => {
      const onPress = jest.fn()
      render(<KpiCard data={makeKpiData()} onPress={onPress} />)

      const card = screen.getByLabelText(/KPI 100/)
      fireEvent.click(card)

      expect(onPress).toHaveBeenCalledWith('kpi-1')
    })

    it('does not call onPress when not provided', () => {
      render(<KpiCard data={makeKpiData()} />)

      // Should not throw when clicking
      const card = screen.getByLabelText(/KPI 100/)
      fireEvent.click(card)
    })
  })

  // ─── Aria label ──────────────────────────────────────────────────────────

  it('has accessible aria-label with health info', () => {
    render(
      <KpiCard data={makeKpiData({ health: KpiHealth.Healthy })} />,
    )

    expect(
      screen.getByLabelText('KPI 100: Test KPI, Healthy'),
    ).toBeInTheDocument()
  })

  it('has accessible aria-label with No Data when no health', () => {
    render(<KpiCard data={makeKpiData()} />)

    expect(
      screen.getByLabelText('KPI 100: Test KPI, No Data'),
    ).toBeInTheDocument()
  })
})

// ─── AddKpiCard ──────────────────────────────────────────────────────────────

describe('AddKpiCard', () => {
  it('renders Add KPI label', () => {
    render(<AddKpiCard onClick={jest.fn()} />)

    expect(screen.getByText('Add KPI')).toBeInTheDocument()
  })

  it('calls onClick when clicked', () => {
    const onClick = jest.fn()
    render(<AddKpiCard onClick={onClick} />)

    fireEvent.click(screen.getByText('Add KPI'))

    expect(onClick).toHaveBeenCalledTimes(1)
  })
})
