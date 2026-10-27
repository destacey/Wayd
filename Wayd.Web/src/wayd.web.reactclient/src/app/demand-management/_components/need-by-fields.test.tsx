import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Form, FormInstance } from 'antd'
import dayjs from 'dayjs'
import {
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import { useEffect } from 'react'
import { NeedByFields } from './need-by-fields'

// jest.setup stubs dayjs down to formatting; the date pickers need the real module.
jest.unmock('dayjs')

const BASES: NeedByBasisDto[] = [
  {
    id: 2,
    code: NeedByBasis.Contractual,
    name: 'Contractual',
    description: 'A contract clause sets the date.',
    order: 2,
    family: NeedByBasisFamily.FixedForUs,
    recordsCommitment: false,
  },
  {
    id: 4,
    code: NeedByBasis.Commitment,
    name: 'Commitment',
    description: 'A commitment someone here made.',
    order: 4,
    family: NeedByBasisFamily.ChosenByUs,
    recordsCommitment: true,
  },
]

const FAMILIES: NeedByBasisFamilyDto[] = [
  { id: 1, code: NeedByBasisFamily.FixedForUs, name: 'Fixed for us', order: 1 },
  { id: 2, code: NeedByBasisFamily.ChosenByUs, name: 'We chose it', order: 2 },
]

const formRef: { current?: FormInstance } = {}

const Harness = ({ initial }: { initial?: object }) => {
  const [form] = Form.useForm()
  useEffect(() => {
    formRef.current = form
  }, [form])
  return (
    <Form form={form} initialValues={initial}>
      <NeedByFields bases={BASES} families={FAMILIES} employees={[]} />
    </Form>
  )
}

describe('NeedByFields', () => {
  it('asks only for the date until one is given', () => {
    // Arrange / Act
    render(<Harness />)

    // Assert
    expect(
      screen.getByText('Does it have to land by a date?'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('What makes it that date?'),
    ).not.toBeInTheDocument()
  })

  it('asks what makes the date in two groups, fixed for us and we chose it', () => {
    // Arrange / Act
    render(<Harness initial={{ needBy: { date: dayjs('2026-12-31') } }} />)

    // Assert
    expect(screen.getByText('What makes it that date?')).toBeInTheDocument()
    expect(
      screen.getByText('Fixed for us — something outside this company sets it'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('We chose it — a person here set the date'),
    ).toBeInTheDocument()
    expect(screen.getByText('A contract names it')).toBeInTheDocument()
    expect(screen.getByText('We promised it to someone')).toBeInTheDocument()
  })

  it('asks the one follow-up the chosen basis needs, and no commitment for a contract', async () => {
    // Arrange
    render(<Harness initial={{ needBy: { date: dayjs('2026-12-31') } }} />)

    // Act
    await act(() => userEvent.click(screen.getByText('A contract names it')))

    // Assert
    expect(
      await screen.findByText('Which contract and clause?'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Who made the commitment?'),
    ).not.toBeInTheDocument()
  })

  it('asks who made a commitment and when only for a basis that records one', async () => {
    // Arrange
    render(<Harness initial={{ needBy: { date: dayjs('2026-12-31') } }} />)

    // Act
    await act(async () =>
      formRef.current!.setFieldValue(
        ['needBy', 'basis'],
        NeedByBasis.Commitment,
      ),
    )

    // Assert
    expect(
      await screen.findByText('Who made the commitment?'),
    ).toBeInTheDocument()
    expect(screen.getByText('When was it made?')).toBeInTheDocument()
  })

  it('previews what reviewers will see, and how the date will be treated', async () => {
    // Arrange / Act
    render(
      <Harness
        initial={{
          needBy: {
            date: dayjs('2026-12-31'),
            basis: NeedByBasis.Contractual,
            reference: 'MSA clause 12',
          },
        }}
      />,
    )

    // Assert
    expect(await screen.findByText('Reviewers will see')).toBeInTheDocument()
    expect(
      screen.getByText('By Dec 31, 2026 · a contract names it'),
    ).toBeInTheDocument()
    expect(screen.getByText('treated as immovable')).toBeInTheDocument()
  })

  it('clears the date and everything resting on it for No particular date', async () => {
    // Arrange
    render(
      <Harness
        initial={{
          needBy: {
            date: dayjs('2026-12-31'),
            basis: NeedByBasis.Contractual,
            reference: 'MSA clause 12',
          },
        }}
      />,
    )

    // Act
    await act(() =>
      userEvent.click(
        screen.getByRole('button', { name: 'No particular date' }),
      ),
    )

    // Assert
    expect(formRef.current!.getFieldValue('needBy')).toEqual({})
    expect(
      screen.queryByText('What makes it that date?'),
    ).not.toBeInTheDocument()
  })

  it('drops the last answer when the basis changes, since each asks its own question', async () => {
    // Arrange
    render(
      <Harness
        initial={{
          needBy: {
            date: dayjs('2026-12-31'),
            basis: NeedByBasis.Commitment,
            reference: 'Northwind Group',
            committedById: 'e-1',
          },
        }}
      />,
    )

    // Act
    await act(() => userEvent.click(screen.getByText('A contract names it')))

    // Assert
    expect(formRef.current!.getFieldValue(['needBy', 'basis'])).toBe(
      NeedByBasis.Contractual,
    )
    expect(
      formRef.current!.getFieldValue(['needBy', 'reference']),
    ).toBeUndefined()
    expect(
      formRef.current!.getFieldValue(['needBy', 'committedById']),
    ).toBeUndefined()
  })
})
