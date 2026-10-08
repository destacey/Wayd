import { fireEvent, render, screen } from '@testing-library/react'
import { IsoDayOfWeek } from '@/src/services/wayd-api'
import WorkingDaysSelect, { formatWorkingDays } from './working-days-select'

describe('formatWorkingDays', () => {
  it('shows a run of consecutive days as a range', () => {
    // Act
    const text = formatWorkingDays([
      IsoDayOfWeek.Friday,
      IsoDayOfWeek.Monday,
      IsoDayOfWeek.Tuesday,
      IsoDayOfWeek.Wednesday,
      IsoDayOfWeek.Thursday,
    ])

    // Assert
    expect(text).toBe('Mon–Fri')
  })

  it('lists days that are not consecutive in week order', () => {
    // Act
    const text = formatWorkingDays([
      IsoDayOfWeek.Sunday,
      IsoDayOfWeek.Monday,
      IsoDayOfWeek.Wednesday,
    ])

    // Assert
    expect(text).toBe('Mon, Wed, Sun')
  })

  it('names a seven-day week', () => {
    // Act
    const text = formatWorkingDays(Object.values(IsoDayOfWeek))

    // Assert
    expect(text).toBe('Every day')
  })

  it('is empty when there are no days', () => {
    // Act
    const text = formatWorkingDays(undefined)

    // Assert
    expect(text).toBe('')
  })
})

describe('WorkingDaysSelect', () => {
  it('reports the chosen days in week order', () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <WorkingDaysSelect value={[IsoDayOfWeek.Tuesday]} onChange={onChange} />,
    )

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'Mon' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith([
      IsoDayOfWeek.Monday,
      IsoDayOfWeek.Tuesday,
    ])
  })
})
