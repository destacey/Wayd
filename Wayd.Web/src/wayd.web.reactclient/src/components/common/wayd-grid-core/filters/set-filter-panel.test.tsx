import { render, screen, fireEvent } from '@testing-library/react'

import SetFilterPanel from './set-filter-panel'
import { SET_FILTER_BLANK, type ColumnFilterModel } from './filter-model'

const ALL = ['System', 'User', 'Other']

const row = (label: string) =>
  screen.getByText(label).closest('label') as HTMLLabelElement
const checkboxIn = (label: HTMLElement) =>
  label.querySelector('input[type="checkbox"]') as HTMLInputElement

describe('SetFilterPanel', () => {
  describe('checked state derivation', () => {
    it('checks everything when there is no filter (undefined)', () => {
      // Arrange / Act
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={jest.fn()} />,
      )

      // Assert — every value + Select All checked
      expect(checkboxIn(row('(Select All)')).checked).toBe(true)
      ALL.forEach((v) => expect(checkboxIn(row(v)).checked).toBe(true))
    })

    it('checks only the descriptor values when filtered to a subset', () => {
      // Arrange / Act
      render(
        <SetFilterPanel
          allValues={ALL}
          value={{ type: 'set', values: ['System'] }}
          onChange={jest.fn()}
        />,
      )

      // Assert
      expect(checkboxIn(row('System')).checked).toBe(true)
      expect(checkboxIn(row('User')).checked).toBe(false)
      expect(checkboxIn(row('Other')).checked).toBe(false)
    })

    it('renders the blank sentinel as a checkable (Blanks) entry', () => {
      // Arrange / Act
      const onChange = jest.fn()
      render(
        <SetFilterPanel
          allValues={[...ALL, SET_FILTER_BLANK]}
          value={undefined}
          onChange={onChange}
        />,
      )

      // Assert — labeled "(Blanks)", checked while unfiltered
      expect(checkboxIn(row('(Blanks)')).checked).toBe(true)

      // Act — unchecking it emits the remaining real values
      fireEvent.click(checkboxIn(row('(Blanks)')))
      expect(onChange).toHaveBeenCalledWith({
        type: 'set',
        values: ALL,
      })
    })
  })

  describe('toggling values', () => {
    it('unchecking one value emits the remaining checked subset', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )

      // Act — uncheck "User" (all were checked)
      fireEvent.click(checkboxIn(row('User')))

      // Assert — subset is the other two
      const next = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
      expect(next.type).toBe('set')
      if (next.type === 'set') {
        expect(new Set(next.values)).toEqual(new Set(['System', 'Other']))
      }
    })

    it('re-checking the last value collapses back to no filter (undefined)', () => {
      // Arrange — start with only System checked
      const onChange = jest.fn()
      render(
        <SetFilterPanel
          allValues={ALL}
          value={{ type: 'set', values: ['System', 'User'] }}
          onChange={onChange}
        />,
      )

      // Act — check the missing "Other" so all are checked
      fireEvent.click(checkboxIn(row('Other')))

      // Assert — all checked ⇒ unfiltered
      expect(onChange).toHaveBeenLastCalledWith(undefined)
    })
  })

  describe('Select All', () => {
    it('unchecking Select All emits an empty set (nothing passes)', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )

      // Act
      fireEvent.click(checkboxIn(row('(Select All)')))

      // Assert — empty selection
      const next = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
      expect(next.type).toBe('set')
      if (next.type === 'set') expect(next.values).toEqual([])
    })

    it('checking Select All (from a subset) collapses to no filter', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel
          allValues={ALL}
          value={{ type: 'set', values: ['System'] }}
          onChange={onChange}
        />,
      )

      // Act
      fireEvent.click(checkboxIn(row('(Select All)')))

      // Assert
      expect(onChange).toHaveBeenLastCalledWith(undefined)
    })

    it('shows Select All indeterminate when only some are checked', () => {
      // Arrange / Act
      render(
        <SetFilterPanel
          allValues={ALL}
          value={{ type: 'set', values: ['System'] }}
          onChange={jest.fn()}
        />,
      )

      // Assert — antd marks the wrapper with ant-checkbox-indeterminate
      const selectAll = row('(Select All)')
      expect(
        selectAll.querySelector('.ant-checkbox-indeterminate'),
      ).toBeInTheDocument()
    })
  })

  describe('search', () => {
    it('filters the visible values and hides Select All while searching', () => {
      // Arrange
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={jest.fn()} />,
      )

      // Act
      fireEvent.change(screen.getByPlaceholderText('Search...'), {
        target: { value: 'sys' },
      })

      // Assert — only System shows; Select All hidden during search
      expect(screen.getByText('System')).toBeInTheDocument()
      expect(screen.queryByText('User')).not.toBeInTheDocument()
      expect(screen.queryByText('(Select All)')).not.toBeInTheDocument()
    })

    it('matches on labels, not raw values', () => {
      // Arrange
      render(
        <SetFilterPanel
          allValues={['1', '2']}
          labels={[
            { value: '1', label: 'Enabled' },
            { value: '2', label: 'Disabled' },
          ]}
          value={undefined}
          onChange={jest.fn()}
        />,
      )

      // Act
      fireEvent.change(screen.getByPlaceholderText('Search...'), {
        target: { value: 'ena' },
      })

      // Assert
      expect(screen.getByText('Enabled')).toBeInTheDocument()
      expect(screen.queryByText('Disabled')).not.toBeInTheDocument()
    })
  })

  describe('focus', () => {
    it('focuses the search box on mount so typing works immediately', () => {
      // Arrange / Act
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={jest.fn()} />,
      )

      // Assert — the host remounts this panel per open (destroyOnHidden), so
      // focusing on mount is what makes every reopen land in the search box.
      expect(screen.getByPlaceholderText('Search...')).toHaveFocus()
    })
  })

  describe('committing the search with Enter', () => {
    it('emits the visible matches as the selection and clears the search', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'sys' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert — the typed query reaches the grid as a set descriptor
      expect(onChange).toHaveBeenCalledWith({ type: 'set', values: ['System'] })
      expect((input as HTMLInputElement).value).toBe('')
    })

    it('signals the host to close the popup after committing', () => {
      // Arrange
      const onCommit = jest.fn()
      render(
        <SetFilterPanel
          allValues={ALL}
          value={undefined}
          onChange={jest.fn()}
          onCommit={onCommit}
        />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'sys' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert
      expect(onCommit).toHaveBeenCalledTimes(1)
    })

    it('does not close the popup when Enter is a no-op', () => {
      // Arrange
      const onCommit = jest.fn()
      render(
        <SetFilterPanel
          allValues={ALL}
          value={undefined}
          onChange={jest.fn()}
          onCommit={onCommit}
        />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'zzz' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert — no matches ⇒ nothing committed, so the panel stays open
      expect(onCommit).not.toHaveBeenCalled()
    })

    it('commits every match when the search hits more than one value', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel
          allValues={['Approved', 'Approving', 'Rejected']}
          value={undefined}
          onChange={onChange}
        />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'appro' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert
      expect(onChange).toHaveBeenCalledWith({
        type: 'set',
        values: ['Approved', 'Approving'],
      })
    })

    it('does nothing when the search is empty', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )
      const input = screen.getByPlaceholderText('Search...')

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert — Enter on an empty box must not wipe the filter
      expect(onChange).not.toHaveBeenCalled()
    })

    it('does nothing when the search matches no values', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'zzz' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert — committing "no matches" would select nothing and blank the grid
      expect(onChange).not.toHaveBeenCalled()
    })

    it('collapses to no filter when the search matches everything', () => {
      // Arrange
      const onChange = jest.fn()
      render(
        <SetFilterPanel
          allValues={['Alpha', 'Alpine']}
          value={undefined}
          onChange={onChange}
        />,
      )
      const input = screen.getByPlaceholderText('Search...')
      fireEvent.change(input, { target: { value: 'alp' } })

      // Act
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', charCode: 13 })

      // Assert — all values selected ⇒ unfiltered, same as the checkbox path
      expect(onChange).toHaveBeenCalledWith(undefined)
    })
  })

  describe('reset', () => {
    it('clears the filter (undefined) and is disabled when already unfiltered', () => {
      // Arrange
      const onChange = jest.fn()
      const { rerender } = render(
        <SetFilterPanel
          allValues={ALL}
          value={{ type: 'set', values: ['System'] }}
          onChange={onChange}
        />,
      )

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Reset' }))

      // Assert — reset clears
      expect(onChange).toHaveBeenLastCalledWith(undefined)

      // And when unfiltered, Reset is disabled
      rerender(
        <SetFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
      )
      expect(screen.getByRole('button', { name: 'Reset' })).toBeDisabled()
    })
  })
})
