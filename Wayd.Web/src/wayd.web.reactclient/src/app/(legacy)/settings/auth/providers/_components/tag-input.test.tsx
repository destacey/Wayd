import { fireEvent, render, screen } from '@testing-library/react'
import TagInput from './tag-input'

// Ant Design's Tag and Input have internal complexity; stub them to simple elements
// so tests focus on TagInput's own logic rather than Ant Design's internals.
jest.mock('antd', () => ({
  Tag: ({ children, closable, onClose, onClick, style }: any) => {
    // The add-trigger Tag has an onClick but no closable prop
    if (onClick && !closable) {
      return (
        <button type="button" data-testid="add-trigger" onClick={onClick} style={style}>
          {children}
        </button>
      )
    }
    return (
      <span data-testid="tag">
        {children}
        {closable && (
          <button type="button" data-testid="tag-close" onClick={onClose}>
            ×
          </button>
        )}
      </span>
    )
  },
  Input: ({ value, onChange, onBlur, onPressEnter, placeholder }: any) => (
    <input
      data-testid="tag-input"
      value={value}
      placeholder={placeholder}
      onChange={onChange}
      onBlur={onBlur}
      onKeyDown={(e) => e.key === 'Enter' && onPressEnter?.(e)}
    />
  ),
  InputRef: {},
}))

jest.mock('@ant-design/icons', () => ({
  PlusOutlined: () => <span />,
}))

const renderTagInput = (props: Partial<Parameters<typeof TagInput>[0]> = {}) => {
  const onChange = jest.fn()
  const utils = render(<TagInput value={[]} onChange={onChange} {...props} />)
  return { ...utils, onChange }
}

const openInput = () => {
  fireEvent.click(screen.getByTestId('add-trigger'))
}

describe('TagInput', () => {
  describe('rendering', () => {
    it('renders existing tags', () => {
      renderTagInput({ value: ['openid', 'profile'] })
      expect(screen.getAllByTestId('tag')).toHaveLength(2)
      expect(screen.getByText('openid')).toBeInTheDocument()
      expect(screen.getByText('profile')).toBeInTheDocument()
    })

    it('renders the add trigger when input is not visible', () => {
      renderTagInput()
      expect(screen.queryByTestId('tag-input')).not.toBeInTheDocument()
      expect(screen.getByTestId('add-trigger')).toBeInTheDocument()
    })

    it('shows the text input after clicking the add trigger', () => {
      renderTagInput()
      openInput()
      expect(screen.getByTestId('tag-input')).toBeInTheDocument()
    })

    it('passes placeholder to the text input', () => {
      renderTagInput({ placeholder: 'Add scope and press Enter' })
      openInput()
      expect(screen.getByPlaceholderText('Add scope and press Enter')).toBeInTheDocument()
    })
  })

  describe('adding tags', () => {
    it('adds a tag on Enter and closes the input', () => {
      const { onChange } = renderTagInput({ value: [] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: 'email' },
      })
      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).toHaveBeenCalledWith(['email'])
      expect(screen.queryByTestId('tag-input')).not.toBeInTheDocument()
    })

    it('adds a tag on blur and closes the input', () => {
      const { onChange } = renderTagInput({ value: [] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: 'profile' },
      })
      fireEvent.blur(screen.getByTestId('tag-input'))

      expect(onChange).toHaveBeenCalledWith(['profile'])
    })

    it('appends to existing tags', () => {
      const { onChange } = renderTagInput({ value: ['openid', 'profile'] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: 'email' },
      })
      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).toHaveBeenCalledWith(['openid', 'profile', 'email'])
    })

    it('trims whitespace before adding', () => {
      const { onChange } = renderTagInput({ value: [] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: '  email  ' },
      })
      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).toHaveBeenCalledWith(['email'])
    })
  })

  describe('duplicate prevention', () => {
    it('does not add a duplicate tag', () => {
      const { onChange } = renderTagInput({ value: ['openid'] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: 'openid' },
      })
      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).not.toHaveBeenCalled()
    })
  })

  describe('empty input', () => {
    it('does not call onChange when Enter is pressed on empty input', () => {
      const { onChange } = renderTagInput({ value: [] })
      openInput()

      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).not.toHaveBeenCalled()
    })

    it('does not call onChange when input is only whitespace', () => {
      const { onChange } = renderTagInput({ value: [] })
      openInput()

      fireEvent.change(screen.getByTestId('tag-input'), {
        target: { value: '   ' },
      })
      fireEvent.keyDown(screen.getByTestId('tag-input'), { key: 'Enter' })

      expect(onChange).not.toHaveBeenCalled()
    })
  })

  describe('removing tags', () => {
    it('removes a tag when its close button is clicked', () => {
      const { onChange } = renderTagInput({ value: ['openid', 'profile', 'email'] })

      const closeBtns = screen.getAllByTestId('tag-close')
      fireEvent.click(closeBtns[1]) // click close on 'profile'

      expect(onChange).toHaveBeenCalledWith(['openid', 'email'])
    })

    it('removes the correct tag when multiple exist', () => {
      const { onChange } = renderTagInput({ value: ['a', 'b', 'c'] })

      fireEvent.click(screen.getAllByTestId('tag-close')[0])

      expect(onChange).toHaveBeenCalledWith(['b', 'c'])
    })
  })
})
