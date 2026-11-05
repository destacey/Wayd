import { render, screen } from '@testing-library/react'
import WorkStatusTag from './work-status-tag'
import { StatusCategoryId } from '../../types'

describe('WorkStatusTag', () => {
  describe('Proposed status', () => {
    it('should render tag with Proposed status and default color', () => {
      render(
        <WorkStatusTag
          status="Proposed"
          category={StatusCategoryId.Proposed}
        />,
      )
      const tag = screen.getByText('Proposed')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Active status', () => {
    it('should render tag with Active status and processing color', () => {
      render(
        <WorkStatusTag status="Active" category={StatusCategoryId.Active} />,
      )
      const tag = screen.getByText('Active')
      expect(tag).toBeInTheDocument()
    })

    it('should render tag with custom status text for Active category', () => {
      render(
        <WorkStatusTag
          status="In Progress"
          category={StatusCategoryId.Active}
        />,
      )
      const tag = screen.getByText('In Progress')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Done status', () => {
    it('should render tag with Done status and success color', () => {
      render(
        <WorkStatusTag status="Done" category={StatusCategoryId.Done} />,
      )
      const tag = screen.getByText('Done')
      expect(tag).toBeInTheDocument()
    })

    it('should render tag with custom status text for Done category', () => {
      render(
        <WorkStatusTag
          status="Completed"
          category={StatusCategoryId.Done}
        />,
      )
      const tag = screen.getByText('Completed')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Removed status', () => {
    it('should render tag with Removed status and warning color', () => {
      render(
        <WorkStatusTag
          status="Removed"
          category={StatusCategoryId.Removed}
        />,
      )
      const tag = screen.getByText('Removed')
      expect(tag).toBeInTheDocument()
    })

    it('should render tag with custom status text for Removed category', () => {
      render(
        <WorkStatusTag
          status="Cancelled"
          category={StatusCategoryId.Removed}
        />,
      )
      const tag = screen.getByText('Cancelled')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('getTagColor function', () => {
    it('should apply correct color class for Proposed category', () => {
      const { container } = render(
        <WorkStatusTag
          status="Proposed"
          category={StatusCategoryId.Proposed}
        />,
      )
      const tag = container.querySelector('.ant-tag-default')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for Active category', () => {
      const { container } = render(
        <WorkStatusTag status="Active" category={StatusCategoryId.Active} />,
      )
      const tag = container.querySelector('.ant-tag-processing')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for Done category', () => {
      const { container } = render(
        <WorkStatusTag status="Done" category={StatusCategoryId.Done} />,
      )
      const tag = container.querySelector('.ant-tag-success')
      expect(tag).toBeInTheDocument()
    })

    it('should apply correct color class for Removed category', () => {
      const { container } = render(
        <WorkStatusTag
          status="Removed"
          category={StatusCategoryId.Removed}
        />,
      )
      const tag = container.querySelector('.ant-tag-warning')
      expect(tag).toBeInTheDocument()
    })
  })

  describe('Edge cases', () => {
    it('should handle empty status string', () => {
      const { container } = render(
        <WorkStatusTag status="" category={StatusCategoryId.Active} />,
      )
      const tag = container.querySelector('.ant-tag')
      expect(tag).toBeInTheDocument()
      expect(tag).toHaveTextContent('')
    })

    it('should handle status with special characters', () => {
      render(
        <WorkStatusTag
          status="Status & More"
          category={StatusCategoryId.Active}
        />,
      )
      const tag = screen.getByText('Status & More')
      expect(tag).toBeInTheDocument()
    })

    it('should handle long status text', () => {
      const longStatus = 'This is a very long status text that might wrap'
      render(
        <WorkStatusTag
          status={longStatus}
          category={StatusCategoryId.Active}
        />,
      )
      const tag = screen.getByText(longStatus)
      expect(tag).toBeInTheDocument()
    })
  })
})
