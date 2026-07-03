import { render, screen, fireEvent } from '@testing-library/react'
import FilterPopup from './filter-popup'
import type { ColumnFilterModel } from './filter-model'

describe('FilterPopup', () => {
  describe('text filter', () => {
    it('emits a text descriptor as the user types a value', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <FilterPopup filterType="text" value={undefined} onChange={onChange} />,
      )

      // Act
      fireEvent.change(screen.getByPlaceholderText('Value'), {
        target: { value: 'plan' },
      })

      // Assert
      expect(onChange).toHaveBeenCalledWith({
        type: 'text',
        conditions: [{ op: 'contains', value: 'plan' }],
        join: 'AND',
      })
    })

    it('adds a second condition up to the max, then hides the add button', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <FilterPopup
          filterType="text"
          value={{
            type: 'text',
            conditions: [{ op: 'contains', value: 'a' }],
            join: 'AND',
          }}
          onChange={onChange}
          maxConditions={2}
        />,
      )

      // Act
      fireEvent.click(screen.getByText('Add condition'))

      // Assert — descriptor grows to two conditions
      const next = onChange.mock.calls[0][0] as ColumnFilterModel
      expect(next.type).toBe('text')
      if (next.type === 'text') {
        expect(next.conditions).toHaveLength(2)
      }
    })

    it('does not show the add button once the max is reached', () => {
      // Arrange
      render(
        <FilterPopup
          filterType="text"
          value={{
            type: 'text',
            conditions: [
              { op: 'contains', value: 'a' },
              { op: 'contains', value: 'b' },
            ],
            join: 'AND',
          }}
          onChange={jest.fn()}
          maxConditions={2}
        />,
      )

      // Act / Assert
      expect(screen.queryByText('Add condition')).not.toBeInTheDocument()
    })

    it('clears the filter via Clear', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <FilterPopup
          filterType="text"
          value={{
            type: 'text',
            conditions: [{ op: 'contains', value: 'a' }],
            join: 'AND',
          }}
          onChange={onChange}
        />,
      )

      // Act
      fireEvent.click(screen.getByText('Clear'))

      // Assert
      expect(onChange).toHaveBeenCalledWith(undefined)
    })
  })

  // Set columns are handled by SetFilterPanel (see set-filter-panel.test.tsx),
  // not FilterPopup — FilterPopup only covers condition-based filters.
})
