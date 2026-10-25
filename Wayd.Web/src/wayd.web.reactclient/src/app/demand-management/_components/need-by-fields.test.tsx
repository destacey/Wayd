import { act, render, screen } from '@testing-library/react'
import { Form, FormInstance } from 'antd'
import dayjs from 'dayjs'
import {
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import { useEffect } from 'react'
import { NeedByFields } from './need-by'

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
    expect(screen.getByText('Need by')).toBeInTheDocument()
    expect(
      screen.queryByText('What makes it that date?'),
    ).not.toBeInTheDocument()
  })

  it('asks what makes it that date once there is a date, and the follow-up once a basis is chosen', async () => {
    // Arrange
    render(<Harness initial={{ needBy: { date: dayjs('2026-12-31') } }} />)
    expect(screen.getByText('What makes it that date?')).toBeInTheDocument()
    expect(
      screen.queryByText('What does the date rest on?'),
    ).not.toBeInTheDocument()

    // Act
    await act(async () =>
      formRef.current!.setFieldValue(
        ['needBy', 'basis'],
        NeedByBasis.Contractual,
      ),
    )

    // Assert — the basis's own description says what to give, and no commitment is asked for
    expect(
      await screen.findByText('What does the date rest on?'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('A contract clause sets the date.'),
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

  it('previews what reviewers will see, with the family named from the lookup', async () => {
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

    // Assert
    expect(await screen.findByText('Reviewers will see')).toBeInTheDocument()
    expect(screen.getByText('Dec 31, 2026')).toBeInTheDocument()
    expect(screen.getByText('Fixed for us')).toBeInTheDocument()
  })
})
