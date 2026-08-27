import { render, screen, fireEvent } from '@testing-library/react'
import { ConfigListConstants } from '@/src/config/theme/theme-constants'
import ConfigListPanel, { CONFIG_PANEL_WIDTH_KEY } from './config-list-panel'

const mockBreakpoint = jest.fn()
/** Captures the props the Drawer was given, so deprecated ones are catchable. */
const mockDrawerProps = jest.fn()

// Two stubs, both narrow:
//
// Drawer — antd's reaches for Next's server-side AsyncLocalStorage, which
// jsdom has no runtime for, so a bare <Drawer> fails to render on its own.
//
// Grid.useBreakpoint — the component destructures it at module load, so a
// spy installed later would never be seen. It has to be replaced in the
// factory. Everything else stays real.
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  const MockDrawer = ({ title, open, children, ...rest }: any) => {
    mockDrawerProps(rest)
    return open ? (
      <div data-testid="drawer">
        <div>{title}</div>
        {children}
      </div>
    ) : null
  }
  MockDrawer.displayName = 'MockDrawer'
  return {
    ...actual,
    Drawer: MockDrawer,
    Grid: { ...actual.Grid, useBreakpoint: () => mockBreakpoint() },
  }
})

/** Desktop — the panel shares the row with the list. */
const wide = () =>
  mockBreakpoint.mockReturnValue({
    xs: true,
    sm: true,
    md: true,
    lg: true,
    xl: true,
    xxl: false,
  })

/** Below md — the panel becomes a Drawer. */
const narrow = () =>
  mockBreakpoint.mockReturnValue({
    xs: true,
    sm: true,
    md: false,
    lg: false,
    xl: false,
    xxl: false,
  })

const renderPanel = (props: Partial<React.ComponentProps<typeof ConfigListPanel>> = {}) =>
  render(
    <ConfigListPanel
      open
      onClose={jest.fn()}
      title="Capital"
      details={<div>Accounting Code 4100</div>}
      {...props}
    >
      <div>the list</div>
    </ConfigListPanel>,
  )

describe('ConfigListPanel', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    wide()
  })

  describe('panel', () => {
    it('renders the list and the open record together', () => {
      // Arrange / Act — the point of the shape: the list stays on screen
      renderPanel()

      // Assert
      expect(screen.getByText('the list')).toBeInTheDocument()
      expect(screen.getByText('Accounting Code 4100')).toBeInTheDocument()
    })

    it('shows the list alone when nothing is selected', () => {
      // Arrange / Act
      renderPanel({ open: false })

      // Assert
      expect(screen.getByText('the list')).toBeInTheDocument()
      expect(screen.queryByText('Accounting Code 4100')).not.toBeInTheDocument()
    })

    it('titles the panel with the record name', () => {
      // Arrange / Act
      renderPanel()

      // Assert
      expect(screen.getByText('Capital')).toBeInTheDocument()
      expect(
        screen.getByRole('complementary', { name: 'Capital details' }),
      ).toBeInTheDocument()
    })

    it('closes from the panel header', () => {
      // Arrange
      const onClose = jest.fn()
      renderPanel({ onClose })

      // Act
      fireEvent.click(
        screen.getByRole('button', { name: 'Close details panel' }),
      )

      // Assert
      expect(onClose).toHaveBeenCalledTimes(1)
    })

    it('shows a skeleton while the record loads', () => {
      // Arrange / Act — the panel's frame is already correct, so the shape is
      // known and a skeleton beats a spinner.
      renderPanel({ isLoading: true })

      // Assert
      expect(document.querySelector('.ant-skeleton')).toBeInTheDocument()
      expect(screen.queryByText('Accounting Code 4100')).not.toBeInTheDocument()
    })

    it('puts record actions behind a ⋯ in the header', () => {
      // Arrange / Act — beside the name, where the record's identity is
      renderPanel({ actionItems: [{ key: 'edit', label: 'Edit' }] })

      // Assert
      const menu = screen.getByRole('button', { name: 'Record actions' })
      expect(menu).toBeInTheDocument()
      expect(menu.closest(`.${'panelHeader'}`)).toBeInTheDocument()
    })

    it('opens the action items from the ⋯', () => {
      // Arrange
      const onClick = jest.fn()
      renderPanel({ actionItems: [{ key: 'edit', label: 'Edit', onClick }] })

      // Act
      fireEvent.click(screen.getByRole('button', { name: 'Record actions' }))
      fireEvent.click(screen.getByText('Edit'))

      // Assert
      expect(onClick).toHaveBeenCalledTimes(1)
    })

    it('shows no ⋯ when the viewer can do nothing', () => {
      // Arrange / Act — an affordance opening onto an empty menu is worse
      // than none.
      renderPanel({ actionItems: [] })

      // Assert
      expect(
        screen.queryByRole('button', { name: 'Record actions' }),
      ).not.toBeInTheDocument()
    })

    it('shows no ⋯ when no action items are given at all', () => {
      // Arrange / Act
      renderPanel()

      // Assert
      expect(
        screen.queryByRole('button', { name: 'Record actions' }),
      ).not.toBeInTheDocument()
    })
  })

  describe('resize', () => {
    it('keeps the handle out of the scrolling container', () => {
      // Arrange / Act — the handle is positioned in the gap OUTSIDE the panel
      // box, so an `overflow` between it and the panel clips it: rendered, in
      // the DOM, and invisible. That shipped once.
      renderPanel()

      // Assert — the scroll container is a sibling of the handle, not its
      // ancestor
      const handle = screen.getByRole('separator', {
        name: 'Resize details panel',
      })
      expect(handle.closest(`.${'panelScroll'}`)).toBeNull()
    })

    it('exposes the panel as a keyboard-operable separator', () => {
      // Arrange / Act
      renderPanel()

      // Assert
      const handle = screen.getByRole('separator', {
        name: 'Resize details panel',
      })
      expect(handle).toHaveAttribute('tabindex', '0')
      expect(handle).toHaveAttribute(
        'aria-valuemin',
        String(ConfigListConstants.PANEL_MIN_WIDTH),
      )
      expect(handle).toHaveAttribute(
        'aria-valuemax',
        String(ConfigListConstants.PANEL_MAX_WIDTH),
      )
    })

    it('widens on ArrowLeft, because the panel is anchored right', () => {
      // Arrange
      renderPanel()
      const handle = screen.getByRole('separator', {
        name: 'Resize details panel',
      })
      const before = Number(handle.getAttribute('aria-valuenow'))

      // Act
      fireEvent.keyDown(handle, { key: 'ArrowLeft' })

      // Assert
      expect(
        Number(
          screen
            .getByRole('separator', { name: 'Resize details panel' })
            .getAttribute('aria-valuenow'),
        ),
      ).toBeGreaterThan(before)
    })

    it('ignores keys that are not the resize arrows', () => {
      // Arrange
      renderPanel()
      const handle = screen.getByRole('separator', {
        name: 'Resize details panel',
      })
      const before = handle.getAttribute('aria-valuenow')

      // Act
      fireEvent.keyDown(handle, { key: 'ArrowUp' })

      // Assert
      expect(handle).toHaveAttribute('aria-valuenow', before!)
    })

    it('clamps a stored width that would squeeze the list out', () => {
      // Arrange — jest.setup stubs localStorage with bare jest.fn()s and no
      // backing store, so a persistence test has to install a real one. The
      // version lives in the key (`key:v1`); the value is the bare JSON.
      const store: Record<string, string> = {
        [`${CONFIG_PANEL_WIDTH_KEY}:v1`]: JSON.stringify(5000),
      }
      const getItem = jest
        .spyOn(window.localStorage, 'getItem')
        .mockImplementation((key: string) => store[key] ?? null)

      // Act
      renderPanel()

      // Assert — bounds are enforced on read, not just on drag
      expect(
        screen.getByRole('separator', { name: 'Resize details panel' }),
      ).toHaveAttribute(
        'aria-valuenow',
        String(ConfigListConstants.PANEL_MAX_WIDTH),
      )
      getItem.mockRestore()
    })
  })

  describe('below md', () => {
    it('moves the record into a drawer over the list', () => {
      // Arrange
      narrow()

      // Act
      renderPanel()

      // Assert — same content, different container
      expect(screen.getByTestId('drawer')).toBeInTheDocument()
      expect(screen.getByText('Accounting Code 4100')).toBeInTheDocument()
      expect(screen.getByText('the list')).toBeInTheDocument()
    })

    it('keeps the list alone when nothing is selected', () => {
      // Arrange
      narrow()

      // Act
      renderPanel({ open: false })

      // Assert
      expect(screen.queryByTestId('drawer')).not.toBeInTheDocument()
      expect(screen.getByText('the list')).toBeInTheDocument()
    })

    it('has no resize handle — the drawer is not resizable', () => {
      // Arrange
      narrow()

      // Act
      renderPanel()

      // Assert
      expect(
        screen.queryByRole('separator', { name: 'Resize details panel' }),
      ).not.toBeInTheDocument()
    })

    it('still offers record actions, in the drawer header', () => {
      // Arrange — nothing is dropped on mobile, it only moves
      narrow()

      // Act
      renderPanel({ actionItems: [{ key: 'edit', label: 'Edit' }] })

      // Assert — the drawer's `extra` slot, which the stub captures
      const props = mockDrawerProps.mock.calls.at(-1)![0]
      expect(props.extra).not.toBeNull()
    })

    it('sizes the drawer without the deprecated width prop', () => {
      // Arrange — the stub renders no real Drawer, so antd's own deprecation
      // warning never fires here. Assert on the props instead, or the next
      // deprecated prop is again only findable in a browser.
      narrow()

      // Act
      renderPanel()

      // Assert
      const props = mockDrawerProps.mock.calls.at(-1)![0]
      expect(props).not.toHaveProperty('width')
      expect(props.size).toEqual(expect.any(Number))
    })
  })
})
