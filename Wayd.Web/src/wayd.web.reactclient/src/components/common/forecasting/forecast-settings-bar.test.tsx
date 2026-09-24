import { fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import ForecastSettingsBar, {
  DEFAULT_FORECAST_SETTINGS,
} from './forecast-settings-bar'

jest.unmock('dayjs')

describe('ForecastSettingsBar', () => {
  it('shows the history window, defaulting to the last 90 days', () => {
    render(
      <ForecastSettingsBar
        value={DEFAULT_FORECAST_SETTINGS}
        onChange={jest.fn()}
      />,
    )

    expect(screen.getByText('Last 90 days')).toBeInTheDocument()
  })

  it('hides the target date and dependency controls unless asked for', () => {
    render(
      <ForecastSettingsBar
        value={DEFAULT_FORECAST_SETTINGS}
        onChange={jest.fn()}
      />,
    )

    expect(screen.queryByText('Target date')).not.toBeInTheDocument()
    expect(screen.queryByText('Ignore dependencies')).not.toBeInTheDocument()
  })

  it('reports toggling dependencies off', () => {
    const onChange = jest.fn()
    render(
      <ForecastSettingsBar
        value={DEFAULT_FORECAST_SETTINGS}
        onChange={onChange}
        targetDateLabel="Target date"
        showIgnoreDependencies
      />,
    )

    fireEvent.click(screen.getByRole('switch', { name: 'Ignore dependencies' }))

    expect(onChange).toHaveBeenCalledWith({
      ...DEFAULT_FORECAST_SETTINGS,
      ignoreDependencies: true,
    })
    expect(screen.getByText('Target date')).toBeInTheDocument()
  })

  it('counts started work first by default, and reports turning it off', () => {
    const onChange = jest.fn()
    render(
      <ForecastSettingsBar
        value={DEFAULT_FORECAST_SETTINGS}
        onChange={onChange}
      />,
    )

    const toggle = screen.getByRole('switch', { name: 'Started work first' })
    expect(toggle).toBeChecked()

    fireEvent.click(toggle)

    expect(onChange).toHaveBeenCalledWith({
      ...DEFAULT_FORECAST_SETTINGS,
      startedWorkFirst: false,
    })
  })
})
