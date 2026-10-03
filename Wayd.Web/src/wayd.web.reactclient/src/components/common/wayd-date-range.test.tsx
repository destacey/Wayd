// Calendar dates must parse as local days; the global mock only stubs format.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import WaydDateRange from './wayd-date-range'
import { DateRange } from '../types'

describe('WaydDateRange', () => {
  describe('null/undefined handling', () => {
    it('should return null when dateRange is undefined', () => {
      const { container } = render(
        <WaydDateRange dateRange={undefined as any} />,
      )
      expect(container.firstChild).toBeNull()
    })

    it('should return null when dateRange is null', () => {
      const { container } = render(<WaydDateRange dateRange={null as any} />)
      expect(container.firstChild).toBeNull()
    })

    it('should return null when start date is missing', () => {
      const dateRange: DateRange = {
        end: new Date('2024-12-31'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.firstChild).toBeNull()
    })

    it('should return null when end date is missing', () => {
      const dateRange: DateRange = {
        start: new Date('2024-01-01'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.firstChild).toBeNull()
    })

    it('should return null when both dates are missing', () => {
      const dateRange: DateRange = {}
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.firstChild).toBeNull()
    })
  })

  describe('same year formatting (without time)', () => {
    it('should format dates in same year with short format for start date', () => {
      const dateRange: DateRange = {
        start: new Date('2024-03-15T12:00:00'),
        end: new Date('2024-06-20T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Mar \d+ - Jun \d+, 2024/)
      expect(container.textContent).not.toMatch(/PM|AM/)
    })

    it('should handle dates at the beginning and end of the same year', () => {
      const dateRange: DateRange = {
        start: new Date('2024-01-01T12:00:00'),
        end: new Date('2024-12-31T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Jan \d+ - Dec \d+, 2024/)
    })

    it('should handle same date for start and end', () => {
      const dateRange: DateRange = {
        start: new Date('2024-07-04T12:00:00'),
        end: new Date('2024-07-04T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Jul \d+ - Jul \d+, 2024/)
    })
  })

  describe('different year formatting (without time)', () => {
    it('should format dates across different years with full format for both dates', () => {
      const dateRange: DateRange = {
        start: new Date('2023-11-15T12:00:00'),
        end: new Date('2024-02-20T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Nov \d+, 2023 - Feb \d+, 2024/)
      expect(container.textContent).not.toMatch(/PM|AM/)
    })

    it('should handle multi-year span', () => {
      const dateRange: DateRange = {
        start: new Date('2022-01-01T12:00:00'),
        end: new Date('2025-12-31T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Jan \d+, 2022 - Dec \d+, 2025/)
    })
  })

  describe('with time formatting', () => {
    it('should format dates in same year with time using short format for start date', () => {
      const dateRange: DateRange = {
        start: new Date('2024-03-15T14:30:00'),
        end: new Date('2024-06-20T16:45:00'),
      }
      const { container } = render(
        <WaydDateRange dateRange={dateRange} withTime={true} />,
      )
      expect(container.textContent).toMatch(/Mar \d+, \d+:\d+ [AP]M/)
      expect(container.textContent).toMatch(/Jun \d+, 2024 \d+:\d+ [AP]M/)
    })

    it('should format dates across different years with time using full format for both dates', () => {
      const dateRange: DateRange = {
        start: new Date('2023-11-15T09:00:00'),
        end: new Date('2024-02-20T17:30:00'),
      }
      const { container } = render(
        <WaydDateRange dateRange={dateRange} withTime={true} />,
      )
      expect(container.textContent).toMatch(/Nov \d+, 2023 \d+:\d+ [AP]M/)
      expect(container.textContent).toMatch(/Feb \d+, 2024 \d+:\d+ [AP]M/)
    })

    it('should handle midnight time', () => {
      const dateRange: DateRange = {
        start: new Date('2024-01-01T00:00:00'),
        end: new Date('2024-12-31T23:59:59'),
      }
      const { container } = render(
        <WaydDateRange dateRange={dateRange} withTime={true} />,
      )
      expect(container.textContent).toMatch(/Jan \d+, 12:00 AM/)
      expect(container.textContent).toMatch(/Dec \d+, 2024 11:59 PM/)
    })

    it('should handle noon time', () => {
      const dateRange: DateRange = {
        start: new Date('2024-06-15T12:00:00'),
        end: new Date('2024-06-15T12:30:00'),
      }
      const { container } = render(
        <WaydDateRange dateRange={dateRange} withTime={true} />,
      )
      expect(container.textContent).toMatch(/Jun \d+, 12:00 PM/)
      expect(container.textContent).toMatch(/Jun \d+, 2024 12:30 PM/)
    })
  })

  describe('withTime prop default value', () => {
    it('should default to withTime=false when not provided', () => {
      const dateRange: DateRange = {
        start: new Date('2024-03-15T14:30:00'),
        end: new Date('2024-06-20T16:45:00'),
      }
      render(<WaydDateRange dateRange={dateRange} />)
      // Should not show time
      const text = screen.getByText(/Mar 15 - Jun 20, 2024/)
      expect(text).toBeInTheDocument()
      expect(screen.queryByText(/PM/)).not.toBeInTheDocument()
    })

    it('should respect withTime=false when explicitly set', () => {
      const dateRange: DateRange = {
        start: new Date('2024-03-15T14:30:00'),
        end: new Date('2024-06-20T16:45:00'),
      }
      render(<WaydDateRange dateRange={dateRange} withTime={false} />)
      // Should not show time
      const text = screen.getByText(/Mar 15 - Jun 20, 2024/)
      expect(text).toBeInTheDocument()
      expect(screen.queryByText(/PM/)).not.toBeInTheDocument()
    })
  })

  describe('calendar dates', () => {
    it('shows calendar dates as written', () => {
      // Arrange
      const dateRange: DateRange = { start: '2024-03-15', end: '2024-06-20' }

      // Act
      render(<WaydDateRange dateRange={dateRange} />)

      // Assert
      expect(screen.getByText('Mar 15 - Jun 20, 2024')).toBeInTheDocument()
    })

    it('shows both years for calendar dates in different years', () => {
      // Arrange
      const dateRange: DateRange = { start: '2023-12-31', end: '2024-01-01' }

      // Act
      render(<WaydDateRange dateRange={dateRange} />)

      // Assert
      expect(screen.getByText('Dec 31, 2023 - Jan 1, 2024')).toBeInTheDocument()
    })
  })

  describe('edge cases', () => {
    it('should handle leap year date', () => {
      const dateRange: DateRange = {
        start: new Date('2024-02-29T12:00:00'),
        end: new Date('2024-03-01T12:00:00'),
      }
      const { container } = render(<WaydDateRange dateRange={dateRange} />)
      expect(container.textContent).toMatch(/Feb \d+ - Mar \d+, 2024/)
    })

    it('should handle year boundaries', () => {
      const dateRange: DateRange = {
        start: new Date('2023-12-31T23:59:59'),
        end: new Date('2024-01-01T00:00:00'),
      }
      const { container } = render(
        <WaydDateRange dateRange={dateRange} withTime={true} />,
      )
      expect(container.textContent).toMatch(/Dec \d+, 2023 11:59 PM/)
      expect(container.textContent).toMatch(/Jan \d+, 2024 12:00 AM/)
    })
  })
})
