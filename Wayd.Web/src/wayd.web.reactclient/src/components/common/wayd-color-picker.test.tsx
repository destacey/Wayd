import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import WaydColorPicker from './wayd-color-picker'

const latestColorPickerProps: { current: any | null } = { current: null }

vi.mock('antd', () => {
  return {
    ColorPicker: (props: any) => {
      latestColorPickerProps.current = props

      return (
        <div>
          <button
            type="button"
            data-testid="color-picker-trigger"
            className="ant-color-picker-trigger"
            tabIndex={0}
          >
            Trigger
          </button>
          <button
            type="button"
            data-testid="color-picker-open"
            onClick={() => props.onOpenChange?.(true)}
          >
            Open
          </button>
          <button
            type="button"
            data-testid="color-picker-close"
            onClick={() => props.onOpenChange?.(false)}
          >
            Close
          </button>
          <button
            type="button"
            data-testid="color-picker-select"
            onClick={() =>
              props.onChange?.({ toHexString: () => '#123456' } as any)
            }
          >
            Select
          </button>
          <div data-testid="color-picker-open-state">
            {props.open ? 'open' : 'closed'}
          </div>
        </div>
      )
    },
    Space: (props: any) => <div>{props.children}</div>,
  }
})

vi.mock('../contexts/theme', () => ({
  default: () => ({
    token: {
      colorPrimary: '#1890ff',
    },
  }),
}))

describe('WaydColorPicker', () => {
  beforeEach(() => {
    latestColorPickerProps.current = null
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation(
      (cb: FrameRequestCallback) => {
        cb(0)
        return 1
      },
    )
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders closed by default', () => {
    render(<WaydColorPicker value="#abcdef" onChange={vi.fn()} />)

    expect(screen.getByTestId('color-picker-trigger')).toBeInTheDocument()
    expect(screen.getByTestId('color-picker-open-state')).toHaveTextContent(
      'closed',
    )
  })

  it('calls onChange with selected color when different from current value', () => {
    const onChange = vi.fn()
    render(<WaydColorPicker value="#000000" onChange={onChange} />)

    fireEvent.click(screen.getByTestId('color-picker-select'))

    expect(onChange).toHaveBeenCalledWith('#123456')
  })

  it('clears color when selecting same value', () => {
    const onChange = vi.fn()
    render(<WaydColorPicker value="#123456" onChange={onChange} />)

    fireEvent.click(screen.getByTestId('color-picker-select'))

    expect(onChange).toHaveBeenCalledWith(undefined)
  })

  it('supports usage without onChange handler', () => {
    render(<WaydColorPicker value="#000000" />)

    expect(() => {
      fireEvent.click(screen.getByTestId('color-picker-select'))
    }).not.toThrow()
  })

  it('closes after selection and refocuses trigger', async () => {
    render(<WaydColorPicker value={undefined} onChange={vi.fn()} />)

    fireEvent.click(screen.getByTestId('color-picker-open'))
    expect(screen.getByTestId('color-picker-open-state')).toHaveTextContent(
      'open',
    )

    fireEvent.click(screen.getByTestId('color-picker-select'))

    await waitFor(() => {
      expect(screen.getByTestId('color-picker-open-state')).toHaveTextContent(
        'closed',
      )
    })

    await waitFor(() => {
      expect(document.activeElement).toBe(
        screen.getByTestId('color-picker-trigger'),
      )
    })
  })

  it('keeps open state controlled by WaydColorPicker', () => {
    render(<WaydColorPicker value={undefined} onChange={vi.fn()} />)

    expect(latestColorPickerProps.current).toBeTruthy()
    expect(latestColorPickerProps.current.open).toBe(false)
  })
})
