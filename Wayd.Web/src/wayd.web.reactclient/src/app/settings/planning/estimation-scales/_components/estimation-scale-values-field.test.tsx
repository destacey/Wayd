import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Form } from 'antd'
import { EstimationScaleValueRequest } from '@/src/services/wayd-api'
import EstimationScaleValuesField, {
  estimationScaleValueErrorFieldFor,
  validateEstimationScaleValues,
} from './estimation-scale-values-field'

const estimate = (value: string): EstimationScaleValueRequest => ({
  value,
  isEstimate: true,
})
const card = (value: string): EstimationScaleValueRequest => ({
  value,
  isEstimate: false,
})

describe('validateEstimationScaleValues', () => {
  it('accepts two estimates and a card', () => {
    // Act
    const error = validateEstimationScaleValues([
      estimate('1'),
      estimate('2'),
      card('?'),
    ])

    // Assert
    expect(error).toBeUndefined()
  })

  it.each([
    ['one estimate', [estimate('1'), card('?'), card('☕')]],
    ['only cards', [card('?'), card('∞')]],
    ['no values', []],
  ])('refuses %s', (_, values) => {
    // Act
    const error = validateEstimationScaleValues(values)

    // Assert
    expect(error).toBe('At least 2 values must be estimates')
  })

  it('refuses a value that appears twice once trimmed', () => {
    // Act
    const error = validateEstimationScaleValues([
      estimate('1'),
      estimate('2'),
      card(' 1 '),
    ])

    // Assert
    expect(error).toBe('"1" appears more than once')
  })
})

describe('estimationScaleValueErrorFieldFor', () => {
  it.each([
    ['values[2].value', ['values', 2, 'value']],
    ['values[0].isEstimate', ['values', 0, 'value']],
  ])('puts %s on that row', (key, path) => {
    // Act / Assert
    expect(estimationScaleValueErrorFieldFor(key)).toEqual(path)
  })

  it('leaves other keys to the default matching', () => {
    // Act / Assert
    expect(estimationScaleValueErrorFieldFor('values')).toBeUndefined()
    expect(estimationScaleValueErrorFieldFor('name')).toBeUndefined()
  })
})

describe('EstimationScaleValuesField', () => {
  const Harness = ({
    initial,
    onValues,
  }: {
    initial: EstimationScaleValueRequest[]
    onValues: (values: EstimationScaleValueRequest[]) => void
  }) => {
    const [form] = Form.useForm()
    return (
      <Form
        form={form}
        initialValues={{ values: initial }}
        onValuesChange={(_, all) => onValues(all.values)}
      >
        <EstimationScaleValuesField />
      </Form>
    )
  }

  it('marks a value as a card when Card is chosen', async () => {
    // Arrange
    const onValues = jest.fn()
    render(
      <Harness
        initial={[estimate('1'), estimate('2'), estimate('?')]}
        onValues={onValues}
      />,
    )

    // Act
    const kinds = screen.getAllByText('Card')
    fireEvent.click(kinds[2])

    // Assert
    await waitFor(() =>
      expect(onValues).toHaveBeenLastCalledWith([
        estimate('1'),
        estimate('2'),
        card('?'),
      ]),
    )
  })

  it('shows a loaded card as Card', () => {
    // Arrange / Act — the edit form loads flags from the API, so a false
    // flag must read back as Card
    const { container } = render(
      <Harness
        initial={[estimate('1'), estimate('2'), card('?')]}
        onValues={jest.fn()}
      />,
    )

    // Assert
    const selected = Array.from(
      container.querySelectorAll('.ant-segmented-item-selected'),
    ).map((item) => item.textContent)
    expect(selected).toEqual(['Estimate', 'Estimate', 'Card'])
  })

  it('shows the list error as soon as a flag leaves one estimate', async () => {
    // Arrange
    render(
      <Harness
        initial={[estimate('1'), estimate('2'), card('?')]}
        onValues={jest.fn()}
      />,
    )
    expect(
      screen.queryByText('At least 2 values must be estimates'),
    ).not.toBeInTheDocument()

    // Act — no row is added or removed, which is all antd re-checks on its own
    fireEvent.click(screen.getAllByText('Card')[0])

    // Assert
    expect(
      await screen.findByText('At least 2 values must be estimates'),
    ).toBeInTheDocument()
  })

  it('shows the list error as soon as a value is typed twice', async () => {
    // Arrange
    render(
      <Harness
        initial={[estimate('1'), estimate('2'), estimate('3')]}
        onValues={jest.fn()}
      />,
    )

    // Act
    fireEvent.change(screen.getByLabelText('Value 3'), {
      target: { value: '1' },
    })

    // Assert
    expect(
      await screen.findByText('"1" appears more than once'),
    ).toBeInTheDocument()
  })

  it('refuses a value of only spaces', async () => {
    // Arrange
    render(
      <Harness initial={[estimate('1'), estimate('2')]} onValues={jest.fn()} />,
    )

    // Act — the API refuses a blank value, so the form must too
    await act(async () => {
      fireEvent.change(screen.getByLabelText('Value 2'), {
        target: { value: '   ' },
      })
    })

    // Assert
    await waitFor(() =>
      expect(
        screen.getByLabelText('Value 2').closest('.ant-form-item'),
      ).toHaveClass('ant-form-item-has-error'),
    )
  })

  it('adds a new value as an estimate', async () => {
    // Arrange
    const onValues = jest.fn()
    render(
      <Harness initial={[estimate('1'), estimate('2')]} onValues={onValues} />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: /Add Value/ }))

    // Assert
    await waitFor(() =>
      expect(onValues).toHaveBeenLastCalledWith([
        estimate('1'),
        estimate('2'),
        estimate(''),
      ]),
    )
  })
})
