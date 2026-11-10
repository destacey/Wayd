import React from 'react'
import { render, screen, fireEvent } from '@testing-library/react'

import ControlItemSwitch, {
  ControlItemSwitchProps,
} from './control-item-switch'

describe('ControlItemSwitch', () => {
  const defaultProps: ControlItemSwitchProps = {
    label: 'Test Label',
    checked: false,
    onChange: vi.fn(),
  }

  it('renders correctly with given props', () => {
    render(<ControlItemSwitch {...defaultProps} />)
    expect(screen.getByText('Test Label')).toBeInTheDocument()
    expect(screen.getByRole('switch')).toBeInTheDocument()
  })

  it('switch is checked based on the checked prop', () => {
    render(
      <ControlItemSwitch
        {...defaultProps}
        checked={true}
        data-testid="test1"
      />,
    )
    expect(screen.getByTestId('test1')).toBeChecked()

    render(
      <ControlItemSwitch
        {...defaultProps}
        checked={false}
        data-testid="test2"
      />,
    )
    expect(screen.getByTestId('test2')).not.toBeChecked()
  })

  it('calls onChange handler when switch is toggled', () => {
    render(<ControlItemSwitch {...defaultProps} />)
    const switchElement = screen.getByRole('switch')
    fireEvent.click(switchElement)
    expect(defaultProps.onChange).toHaveBeenCalled()
  })
})
