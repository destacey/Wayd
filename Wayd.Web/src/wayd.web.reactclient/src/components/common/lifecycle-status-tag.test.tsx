import { render, screen } from '@testing-library/react'
import LifecycleStatusTag from './lifecycle-status-tag'
import { LifecycleNavigationDto } from '@/src/services/wayd-api'

describe('LifecycleStatusTag', () => {
  describe('NotStarted phase', () => {
    it('should render tag with NotStarted status and default color', () => {
      const status: LifecycleNavigationDto = {
        id: 1,
        name: 'Proposed',
        lifecycleCategory: 'NotStarted',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('Proposed')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for NotStarted phase', () => {
      const status: LifecycleNavigationDto = {
        id: 1,
        name: 'Proposed',
        lifecycleCategory: 'NotStarted',
      }
      const { container } = render(<LifecycleStatusTag status={status} />)
      const tag = container.querySelector('.ant-tag-default')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Active phase', () => {
    it('should render tag with Active status and processing color', () => {
      const status: LifecycleNavigationDto = {
        id: 2,
        name: 'In Progress',
        lifecycleCategory: 'Active',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('In Progress')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for Active phase', () => {
      const status: LifecycleNavigationDto = {
        id: 2,
        name: 'Active',
        lifecycleCategory: 'Active',
      }
      const { container } = render(<LifecycleStatusTag status={status} />)
      const tag = container.querySelector('.ant-tag-processing')
      expect(tag).toBeInTheDocument()
    })

    it('should render tag with custom status text for Active phase', () => {
      const status: LifecycleNavigationDto = {
        id: 2,
        name: 'In Development',
        lifecycleCategory: 'Active',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('In Development')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Done phase', () => {
    it('should render tag with Done status and success color', () => {
      const status: LifecycleNavigationDto = {
        id: 3,
        name: 'Completed',
        lifecycleCategory: 'Done',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('Completed')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for Done phase', () => {
      const status: LifecycleNavigationDto = {
        id: 3,
        name: 'Completed',
        lifecycleCategory: 'Done',
      }
      const { container } = render(<LifecycleStatusTag status={status} />)
      const tag = container.querySelector('.ant-tag-success')
      expect(tag).toBeInTheDocument()
    })

    it('should render tag with custom status text for Done phase', () => {
      const status: LifecycleNavigationDto = {
        id: 3,
        name: 'Finished',
        lifecycleCategory: 'Done',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('Finished')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Edge cases', () => {
    it('should handle empty status name', () => {
      const status: LifecycleNavigationDto = {
        id: 1,
        name: '',
        lifecycleCategory: 'Active',
      }
      const { container } = render(<LifecycleStatusTag status={status} />)
      const tag = container.querySelector('.ant-tag')
      expect(tag).toBeInTheDocument()
      expect(tag).toHaveTextContent('')
    })

    it('should handle status name with special characters', () => {
      const status: LifecycleNavigationDto = {
        id: 2,
        name: 'Status & More',
        lifecycleCategory: 'Active',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText('Status & More')
      expect(tag).toBeInTheDocument()
    })

    it('should handle long status name', () => {
      const longName = 'This is a very long status name that might wrap'
      const status: LifecycleNavigationDto = {
        id: 3,
        name: longName,
        lifecycleCategory: 'Done',
      }
      render(<LifecycleStatusTag status={status} />)
      const tag = screen.getByText(longName)
      expect(tag).toBeInTheDocument()
    })

    it('should handle invalid lifecycleCategory', () => {
      const status: LifecycleNavigationDto = {
        id: 4,
        name: 'Unknown',
        lifecycleCategory: 'InvalidPhase' as any,
      }
      const { container } = render(<LifecycleStatusTag status={status} />)
      const tag = container.querySelector('.ant-tag-default')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Component memoization', () => {
    it('should render correctly with same props', () => {
      const status: LifecycleNavigationDto = {
        id: 1,
        name: 'Active',
        lifecycleCategory: 'Active',
      }
      const { rerender } = render(<LifecycleStatusTag status={status} />)
      expect(screen.getByText('Active')).toBeInTheDocument()

      // Rerender with same props
      rerender(<LifecycleStatusTag status={status} />)
      expect(screen.getByText('Active')).toBeInTheDocument()
    })
  })
})
