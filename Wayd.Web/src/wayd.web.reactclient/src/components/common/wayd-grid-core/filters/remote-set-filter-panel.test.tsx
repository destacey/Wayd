import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import RemoteSetFilterPanel from './remote-set-filter-panel'
import { SET_FILTER_BLANK, type ColumnFilterModel } from './filter-model'
import type {
  FilterOption,
  FilterOptionsPage,
  FilterOptionsRequest,
  FilterOptionsSource,
} from '../types'

const ADA: FilterOption = { value: 'id-ada', label: 'Ada Lovelace' }
const GRACE: FilterOption = { value: 'id-grace', label: 'Grace Hopper' }
const ALAN: FilterOption = { value: 'id-alan', label: 'Alan Turing' }
const PEOPLE = [ADA, ALAN, GRACE]

/** A source over PEOPLE that matches labels containing the search. */
const sourceOver = (
  people: FilterOption[] = PEOPLE,
  extra: Omit<FilterOptionsSource, 'load'> = {},
) => {
  const load = vi.fn(
    async ({ search }: FilterOptionsRequest): Promise<FilterOptionsPage> => ({
      options: people.filter((p) =>
        p.label.toLowerCase().includes(search.toLowerCase()),
      ),
      hasMore: false,
    }),
  )
  return { load, ...extra }
}

const checkboxFor = (label: string) =>
  screen
    .getByText(label)
    .closest('label')!
    .querySelector('input[type="checkbox"]') as HTMLInputElement

describe('RemoteSetFilterPanel', () => {
  it('loads the first values on open and checks none of them while unfiltered', async () => {
    // Arrange
    const source = sourceOver()

    // Act
    render(
      <RemoteSetFilterPanel
        source={source}
        value={undefined}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(await screen.findByText('Grace Hopper')).toBeInTheDocument()
    expect(source.load).toHaveBeenCalledTimes(1)
    expect(source.load.mock.calls[0][0].search).toBe('')
    PEOPLE.forEach((p) => expect(checkboxFor(p.label).checked).toBe(false))
  })

  it('picks a value with its label', async () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={undefined}
        onChange={onChange}
      />,
    )
    await screen.findByText('Grace Hopper')

    // Act
    await userEvent.click(checkboxFor('Grace Hopper'))

    // Assert
    expect(onChange).toHaveBeenCalledWith({
      type: 'set',
      values: ['id-grace'],
      labels: { 'id-grace': 'Grace Hopper' },
    })
  })

  it('adds a pick to those already made, keeping their labels', async () => {
    // Arrange
    const onChange = vi.fn()
    const value: ColumnFilterModel = {
      type: 'set',
      values: ['id-grace'],
      labels: { 'id-grace': 'Grace Hopper' },
    }
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={value}
        onChange={onChange}
      />,
    )
    await screen.findByText('Ada Lovelace')

    // Act
    await userEvent.click(checkboxFor('Ada Lovelace'))

    // Assert
    expect(onChange).toHaveBeenCalledWith({
      type: 'set',
      values: ['id-grace', 'id-ada'],
      labels: { 'id-grace': 'Grace Hopper', 'id-ada': 'Ada Lovelace' },
    })
  })

  it('keeps a picked value listed and checked when the search does not return it', async () => {
    // Arrange
    const value: ColumnFilterModel = {
      type: 'set',
      values: ['id-grace'],
      labels: { 'id-grace': 'Grace Hopper' },
    }
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={value}
        onChange={vi.fn()}
      />,
    )

    // Act
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'Ada')

    // Assert
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.queryByText('Alan Turing')).not.toBeInTheDocument()
    expect(checkboxFor('Grace Hopper').checked).toBe(true)
  })

  it('lists a picked value once, among the picks rather than the matches', async () => {
    // Arrange
    const value: ColumnFilterModel = {
      type: 'set',
      values: ['id-grace'],
      labels: { 'id-grace': 'Grace Hopper' },
    }

    // Act
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={value}
        onChange={vi.fn()}
      />,
    )
    await screen.findByText('Ada Lovelace')

    // Assert
    expect(screen.getAllByText('Grace Hopper')).toHaveLength(1)
  })

  it('clears the filter when the last pick is unchecked', async () => {
    // Arrange
    const onChange = vi.fn()
    const value: ColumnFilterModel = {
      type: 'set',
      values: ['id-grace'],
      labels: { 'id-grace': 'Grace Hopper' },
    }
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={value}
        onChange={onChange}
      />,
    )

    // Act
    await userEvent.click(checkboxFor('Grace Hopper'))

    // Assert
    expect(onChange).toHaveBeenCalledWith(undefined)
  })

  it('asks once for what was typed, after typing settles', async () => {
    // Arrange
    const source = sourceOver()
    render(
      <RemoteSetFilterPanel
        source={source}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await screen.findByText('Grace Hopper')

    // Act
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'gra')

    // Assert
    await waitFor(() => expect(source.load).toHaveBeenCalledTimes(2))
    expect(source.load.mock.calls[1][0].search).toBe('gra')
    expect(await screen.findByText('Grace Hopper')).toBeInTheDocument()
    expect(screen.queryByText('Ada Lovelace')).not.toBeInTheDocument()
  })

  it('aborts the request a new search replaces', async () => {
    // Arrange
    const source = sourceOver()
    render(
      <RemoteSetFilterPanel
        source={source}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await screen.findByText('Grace Hopper')
    const first = source.load.mock.calls[0][0].signal

    // Act
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'a')

    // Assert
    await waitFor(() => expect(first.aborted).toBe(true))
  })

  it('asks the user to narrow when more values match than were returned', async () => {
    // Arrange
    const source: FilterOptionsSource = {
      load: async () => ({ options: [ADA, ALAN], hasMore: true }),
    }

    // Act
    render(
      <RemoteSetFilterPanel
        source={source}
        value={undefined}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(
      await screen.findByText('Showing the first 2. Type to narrow.'),
    ).toBeInTheDocument()
  })

  it('offers a retry when the values fail to load', async () => {
    // Arrange
    const load = vi
      .fn<FilterOptionsSource['load']>()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValue({ options: [ADA], hasMore: false })
    render(
      <RemoteSetFilterPanel
        source={{ load }}
        value={undefined}
        onChange={vi.fn()}
      />,
    )

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Retry' }))

    // Assert
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument()
    expect(load).toHaveBeenCalledTimes(2)
  })

  it('offers (Blanks) when the source includes blanks', async () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <RemoteSetFilterPanel
        source={sourceOver(PEOPLE, { includeBlank: true })}
        value={undefined}
        onChange={onChange}
      />,
    )
    await screen.findByText('Grace Hopper')

    // Act
    await userEvent.click(checkboxFor('(Blanks)'))

    // Assert
    expect(onChange).toHaveBeenCalledWith({
      type: 'set',
      values: [SET_FILTER_BLANK],
      labels: { [SET_FILTER_BLANK]: '(Blanks)' },
    })
  })

  it('offers no (Blanks) unless the source includes blanks', async () => {
    // Act
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await screen.findByText('Grace Hopper')

    // Assert
    expect(screen.queryByText('(Blanks)')).not.toBeInTheDocument()
  })

  it('picks every match on Enter and closes', async () => {
    // Arrange
    const onChange = vi.fn()
    const onCommit = vi.fn()
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={undefined}
        onChange={onChange}
        onCommit={onCommit}
      />,
    )
    await screen.findByText('Grace Hopper')
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'a')
    await waitFor(() =>
      expect(screen.queryByText('Grace Hopper')).toBeInTheDocument(),
    )
    await waitFor(() =>
      expect(screen.queryByText('Loading values…')).toBeNull(),
    )

    // Act
    await userEvent.type(screen.getByPlaceholderText('Search...'), '{Enter}')

    // Assert
    expect(onChange).toHaveBeenCalledWith({
      type: 'set',
      values: ['id-ada', 'id-alan', 'id-grace'],
      labels: {
        'id-ada': 'Ada Lovelace',
        'id-alan': 'Alan Turing',
        'id-grace': 'Grace Hopper',
      },
    })
    expect(onCommit).toHaveBeenCalled()
  })

  it('clears every pick on Reset', async () => {
    // Arrange
    const onChange = vi.fn()
    const value: ColumnFilterModel = {
      type: 'set',
      values: ['id-grace', 'id-ada'],
      labels: { 'id-grace': 'Grace Hopper', 'id-ada': 'Ada Lovelace' },
    }
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={value}
        onChange={onChange}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Reset' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith(undefined)
  })

  it('picks the matches of a search Enter was pressed on before they arrived', async () => {
    // Arrange
    const onChange = vi.fn()
    const onCommit = vi.fn()
    render(
      <RemoteSetFilterPanel
        source={sourceOver()}
        value={undefined}
        onChange={onChange}
        onCommit={onCommit}
      />,
    )
    await screen.findByText('Grace Hopper')

    // Act — typed and entered well inside the debounce
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'gra{Enter}')

    // Assert
    await waitFor(() =>
      expect(onChange).toHaveBeenCalledWith({
        type: 'set',
        values: ['id-grace'],
        labels: { 'id-grace': 'Grace Hopper' },
      }),
    )
    expect(onCommit).toHaveBeenCalled()
  })

  it('picks nothing on Enter when the source holds more matches than it returned', async () => {
    // Arrange
    const onChange = vi.fn()
    const onCommit = vi.fn()
    const source: FilterOptionsSource = {
      load: async () => ({ options: [ADA, ALAN], hasMore: true }),
    }
    render(
      <RemoteSetFilterPanel
        source={source}
        value={undefined}
        onChange={onChange}
        onCommit={onCommit}
      />,
    )
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'a')
    await screen.findByText('Showing the first 2. Type to narrow.')

    // Act
    await userEvent.type(screen.getByPlaceholderText('Search...'), '{Enter}')

    // Assert
    expect(onChange).not.toHaveBeenCalled()
    expect(onCommit).not.toHaveBeenCalled()
  })

  it('shows it is loading again while a retry runs', async () => {
    // Arrange
    const load = vi
      .fn<FilterOptionsSource['load']>()
      .mockRejectedValueOnce(new Error('offline'))
      .mockReturnValue(new Promise(() => {}))
    render(
      <RemoteSetFilterPanel
        source={{ load }}
        value={undefined}
        onChange={vi.fn()}
      />,
    )

    // Act
    await userEvent.click(await screen.findByRole('button', { name: 'Retry' }))

    // Assert
    expect(screen.getByText('Loading values…')).toBeInTheDocument()
    expect(
      screen.queryByText(/Could not load the values/),
    ).not.toBeInTheDocument()
  })

  it('drops an answer that arrives after a newer search', async () => {
    // Arrange — each search answers when the test says
    const answers = new Map<string, (page: FilterOptionsPage) => void>()
    const load = vi.fn<FilterOptionsSource['load']>(
      ({ search }) =>
        new Promise((resolve) => {
          answers.set(search, resolve)
        }),
    )
    render(
      <RemoteSetFilterPanel
        source={{ load }}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await userEvent.type(screen.getByPlaceholderText('Search...'), 'gra')
    await waitFor(() => expect(answers.has('gra')).toBe(true))

    // Act — the newer search answers first, then the first one
    answers.get('gra')!({ options: [GRACE], hasMore: false })
    await screen.findByText('Grace Hopper')
    answers.get('')!({ options: [ADA], hasMore: false })

    // Assert
    await new Promise((r) => setTimeout(r, 0))
    expect(screen.queryByText('Ada Lovelace')).not.toBeInTheDocument()
    expect(screen.getByText('Grace Hopper')).toBeInTheDocument()
  })

  it('does not ask again when re-rendered with a new source object', async () => {
    // Arrange
    const first = sourceOver()
    const { rerender } = render(
      <RemoteSetFilterPanel
        source={first}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await screen.findByText('Grace Hopper')
    const second = sourceOver()

    // Act
    rerender(
      <RemoteSetFilterPanel
        source={second}
        value={undefined}
        onChange={vi.fn()}
      />,
    )
    await new Promise((r) => setTimeout(r, 300))

    // Assert
    expect(first.load).toHaveBeenCalledTimes(1)
    expect(second.load).not.toHaveBeenCalled()
  })
})
