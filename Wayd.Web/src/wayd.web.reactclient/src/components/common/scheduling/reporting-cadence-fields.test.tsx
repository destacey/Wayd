import { render, screen } from '@testing-library/react'
import { Form } from 'antd'
import dayjs from 'dayjs'
import { Methodology } from '@/src/services/wayd-api'
import ReportingCadenceFields, {
  formatReportingCadence,
  toReportingCadenceRequest,
} from './reporting-cadence-fields'

// The anchor's DatePicker needs the real dayjs.
jest.unmock('dayjs')

const renderInForm = (initialValues: Record<string, unknown>) =>
  render(
    <Form initialValues={initialValues}>
      <Form.Item name="methodology" hidden>
        <input />
      </Form.Item>
      <ReportingCadenceFields />
    </Form>,
  )

describe('formatReportingCadence', () => {
  it('is empty with no cadence', () => {
    // Act
    const text = formatReportingCadence(undefined)

    // Assert
    expect(text).toBe('')
  })

  it('names a one-week cadence in the singular', () => {
    // Act
    const text = formatReportingCadence(1)

    // Assert
    expect(text).toBe('Every week')
  })

  it('names the length in weeks', () => {
    // Act
    const text = formatReportingCadence(2)

    // Assert
    expect(text).toBe('Every 2 weeks')
  })
})

describe('toReportingCadenceRequest', () => {
  it('sends the length and the anchor as a calendar date for a Kanban team', () => {
    // Act
    const request = toReportingCadenceRequest(Methodology.Kanban, {
      cadenceWeeks: 2,
      cadenceAnchor: dayjs('2026-10-05'),
    })

    // Assert
    expect(request).toEqual({ cadenceWeeks: 2, cadenceAnchor: '2026-10-05' })
  })

  it('sends no cadence for a Scrum team', () => {
    // Act
    const request = toReportingCadenceRequest(Methodology.Scrum, {
      cadenceWeeks: 2,
      cadenceAnchor: dayjs('2026-10-05'),
    })

    // Assert
    expect(request).toEqual({})
  })

  it('sends no cadence without a length', () => {
    // Act
    const request = toReportingCadenceRequest(Methodology.Kanban, {
      cadenceWeeks: null,
      cadenceAnchor: dayjs('2026-10-05'),
    })

    // Assert
    expect(request).toEqual({})
  })
})

describe('ReportingCadenceFields', () => {
  it('renders nothing for a Scrum team', () => {
    // Arrange & Act
    renderInForm({ methodology: Methodology.Scrum })

    // Assert
    expect(screen.queryByLabelText('Cadence (weeks)')).not.toBeInTheDocument()
  })

  it('asks for an anchor only once a Kanban team has a length', async () => {
    // Arrange & Act
    renderInForm({ methodology: Methodology.Kanban, cadenceWeeks: 2 })

    // Assert
    expect(screen.getByLabelText('Cadence (weeks)')).toBeInTheDocument()
    expect(await screen.findByLabelText('Cadence Anchor')).toBeInTheDocument()
  })

  it('hides the anchor for a Kanban team with no length', () => {
    // Arrange & Act
    renderInForm({ methodology: Methodology.Kanban })

    // Assert
    expect(screen.getByLabelText('Cadence (weeks)')).toBeInTheDocument()
    expect(screen.queryByLabelText('Cadence Anchor')).not.toBeInTheDocument()
  })
})
