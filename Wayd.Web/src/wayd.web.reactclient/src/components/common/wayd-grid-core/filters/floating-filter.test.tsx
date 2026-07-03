jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { render, screen, fireEvent, act } from '@testing-library/react'
import FloatingFilter from './floating-filter'
import type { ColumnFilterModel } from './filter-model'

describe('FloatingFilter', () => {
  describe('text filter', () => {
    it('commits a text descriptor editing conditions[0] after debounce', () => {
      // Arrange
      jest.useFakeTimers()
      const onChange = jest.fn()
      render(
        <FloatingFilter filterType="text" value={undefined} onChange={onChange} />,
      )

      // Act
      fireEvent.change(screen.getByRole('textbox'), {
        target: { value: 'plan' },
      })
      act(() => {
        jest.runAllTimers()
      })

      // Assert
      expect(onChange).toHaveBeenCalledWith({
        type: 'text',
        conditions: [{ op: 'contains', value: 'plan' }],
        join: 'AND',
      })
      jest.useRealTimers()
    })

    it('preserves the operator already set on the first condition', () => {
      // Arrange
      jest.useFakeTimers()
      const onChange = jest.fn()
      const existing: ColumnFilterModel = {
        type: 'text',
        conditions: [{ op: 'startsWith', value: 'ro' }],
        join: 'AND',
      }
      render(
        <FloatingFilter filterType="text" value={existing} onChange={onChange} />,
      )

      // Act
      fireEvent.change(screen.getByRole('textbox'), {
        target: { value: 'road' },
      })
      act(() => {
        jest.runAllTimers()
      })

      // Assert — operator stays startsWith, only the value changes
      expect(onChange).toHaveBeenCalledWith({
        type: 'text',
        conditions: [{ op: 'startsWith', value: 'road' }],
        join: 'AND',
      })
      jest.useRealTimers()
    })

    it('clears the descriptor when the only condition is emptied', () => {
      // Arrange
      jest.useFakeTimers()
      const onChange = jest.fn()
      const existing: ColumnFilterModel = {
        type: 'text',
        conditions: [{ op: 'contains', value: 'plan' }],
        join: 'AND',
      }
      render(
        <FloatingFilter filterType="text" value={existing} onChange={onChange} />,
      )

      // Act
      fireEvent.change(screen.getByRole('textbox'), { target: { value: '' } })
      act(() => {
        jest.runAllTimers()
      })

      // Assert
      expect(onChange).toHaveBeenLastCalledWith(undefined)
      jest.useRealTimers()
    })

    it('keeps extra popup conditions when the floating value is cleared', () => {
      // Arrange
      jest.useFakeTimers()
      const onChange = jest.fn()
      const existing: ColumnFilterModel = {
        type: 'text',
        conditions: [
          { op: 'contains', value: 'plan' },
          { op: 'endsWith', value: 'er' },
        ],
        join: 'AND',
      }
      render(
        <FloatingFilter filterType="text" value={existing} onChange={onChange} />,
      )

      // Act
      fireEvent.change(screen.getByRole('textbox'), { target: { value: '' } })
      act(() => {
        jest.runAllTimers()
      })

      // Assert — descriptor is retained (not cleared) because a second
      // condition exists; conditions[0] is just emptied.
      const next = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
      expect(next).toBeDefined()
      expect(next.type).toBe('text')
      if (next.type === 'text') {
        expect(next.conditions).toHaveLength(2)
        expect(next.conditions[0].value).toBe('')
      }
      jest.useRealTimers()
    })
  })

  describe('mismatched descriptor (combined column)', () => {
    it('renders empty (does not throw) when handed a set descriptor', () => {
      // Arrange — on a combined text+set column, the active descriptor can be a
      // `set` while the floating input is a text FloatingFilter. It must not
      // read `.conditions` off the set descriptor (regression: TypeError).
      const setDescriptor: ColumnFilterModel = {
        type: 'set',
        values: ['Team Juice'],
      }

      // Act / Assert — no throw; the text input renders empty
      render(
        <FloatingFilter
          filterType="text"
          value={setDescriptor}
          onChange={jest.fn()}
        />,
      )
      expect(screen.getByRole('textbox')).toHaveValue('')
    })
  })
})
