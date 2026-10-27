import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { FormInstance } from 'antd'
import {
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
  NeedByBasis,
  NeedByBasisFamily,
} from '@/src/services/wayd-api'
import EditIntakeRequestForm from './edit-intake-request-form'

// jest.setup stubs dayjs down to formatting; the date pickers need the real module.
jest.unmock('dayjs')

// The markdown editor pulls in ESM-only packages Jest cannot parse.
jest.mock('@/src/components/common/markdown', () => ({
  MarkdownEditor: ({ value }: { value?: string }) => (
    <textarea aria-label="markdown" readOnly value={value ?? ''} />
  ),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

const formHolder: { current?: FormInstance } = {}

// In Jest, Form.useWatch does not refire after typing, so the real hook's validity never updates; as the
// other form suites do, keep the form instance real and submit through validateFields, which, like the real hook,
// returns only the fields that are mounted.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  const { useEffect } = jest.requireActual('react')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = Form.useForm()
      useEffect(() => {
        formHolder.current = form
      }, [form])
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          if (await onSubmit(await form.validateFields(), form)) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockUpdate = jest.fn()
// What the request's desk offers; the request's own channel is looked up among every channel.
let mockDeskChannels: {
  id: string
  name: string
  order: number
  isActive: boolean
}[] = [{ id: 'c-1', name: 'Security', order: 1, isActive: true }]
// Every channel, archived ones included; undefined while it loads.
let mockAllChannels:
  { id: string; name: string; order: number; isActive: boolean }[] | undefined
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useUpdateIntakeRequestMutation: () => [mockUpdate],
    useGetNeedByBasesQuery: () => ({
      data: [
        {
          id: 4,
          code: 'Commitment',
          name: 'Commitment',
          description: 'A commitment someone here made.',
          order: 4,
          family: 'ChosenByUs',
          recordsCommitment: true,
        },
      ],
    }),
    useGetNeedByBasisFamiliesQuery: () => ({
      data: [{ id: 2, code: 'ChosenByUs', name: 'We chose it', order: 2 }],
    }),
    // Active employees only; the committer below has left.
    useGetIntakeEmployeeOptionsQuery: () => ({
      data: [{ id: 'e-1', key: 1, name: 'Avery Example' }],
    }),
    useGetIntakeChannelsQuery: (args?: { deskId?: string }) => ({
      data: args?.deskId ? mockDeskChannels : mockAllChannels,
    }),
  }),
)

const request = {
  summary: {
    id: 'r-42',
    key: 42,
    title: 'Rotate the VPN certificates',
    desk: { id: 'd-1', key: 1, name: 'IT' },
    requester: { id: 'e-1', key: 1, name: 'Avery Example' },
    channel: { id: 'c-1', name: 'Security' },
    needBy: {
      date: '2026-12-31',
      basis: NeedByBasis.Commitment,
      family: NeedByBasisFamily.ChosenByUs,
      reference: 'Steering committee',
      committedBy: { id: 'e-9', key: 9, name: 'Morgan Former' },
      committedOn: '2026-09-03',
    },
  },
  substance: null,
  canEdit: true,
  canRemoveParticipants: false,
  canDelete: false,
  canAddParticipants: false,
} as unknown as IntakeRequestDetailsDto

const substance = {
  description: 'The certificates expire in March.',
  submittedBy: { id: 'e-1', key: 1, name: 'Avery Example' },
  participants: [],
} as unknown as IntakeRequestSubstanceDto

const renderForm = async (r: IntakeRequestDetailsDto = request) =>
  await act(async () =>
    render(
      <EditIntakeRequestForm
        request={r}
        substance={substance}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('EditIntakeRequestForm', () => {
  beforeEach(() => {
    mockUpdate.mockReset()
    mockDeskChannels = [
      { id: 'c-1', name: 'Security', order: 1, isActive: true },
    ]
    mockAllChannels = [
      { id: 'c-1', name: 'Security', order: 1, isActive: true },
      { id: 'c-2', name: 'Support', order: 2, isActive: true },
    ]
  })

  it('opens on a saved commitment with every follow-up shown and filled', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(
      await screen.findByText('Who did we promise it to?'),
    ).toBeInTheDocument()
    expect(screen.getByDisplayValue('Steering committee')).toBeInTheDocument()
    expect(screen.getByText('Who made the commitment?')).toBeInTheDocument()
  })

  it('names a committer who has since left rather than showing their id', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(await screen.findAllByText(/Morgan Former/)).toHaveLength(1)
    expect(screen.queryByText('e-9')).not.toBeInTheDocument()
  })

  it('sends no need-by once the date is cleared', async () => {
    // Arrange
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm()
    await act(async () =>
      formHolder.current!.setFieldValue(['needBy', 'date'], null),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'r-42', needBy: undefined }),
    )
  })

  it('puts a need-by validation error on its field', async () => {
    // Arrange
    mockUpdate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { 'NeedBy.Reference': ['Reference is too long.'] },
        }),
    })
    await renderForm()

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(formHolder.current!.getFieldError(['needBy', 'reference'])).toEqual([
      'Reference is too long.',
    ])
  })

  it('keeps a channel the desk no longer offers', async () => {
    // Arrange — the request moved to a desk that offers only Support
    mockDeskChannels = [
      { id: 'c-2', name: 'Support', order: 2, isActive: true },
    ]
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm()

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ channelId: 'c-1' }),
    )
  })

  it('asks nothing about the channel on a desk offering none, for a request with none', async () => {
    // Arrange
    mockDeskChannels = []
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm({
      ...request,
      summary: { ...request.summary, channel: undefined },
    } as unknown as IntakeRequestDetailsDto)

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(
      screen.queryByText('Where did this come from?'),
    ).not.toBeInTheDocument()
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ channelId: undefined }),
    )
  })

  it('keeps the channel on a desk offering none while the full list has not loaded', async () => {
    // Arrange — without the request's own channel in the options, the field would hide and saving would clear it
    mockDeskChannels = []
    mockAllChannels = undefined
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm()

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(screen.getByText('Where did this come from?')).toBeInTheDocument()
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ channelId: 'c-1' }),
    )
  })

  it('lets a channel be cleared on a desk offering none', async () => {
    // Arrange
    mockDeskChannels = []
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    await renderForm()

    // Act
    await act(async () =>
      formHolder.current!.setFieldValue('channelId', undefined),
    )
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ channelId: undefined }),
    )
  })

  it('requires a channel on a desk offering some', async () => {
    // Arrange
    await renderForm()

    await act(async () =>
      formHolder.current!.setFieldValue('channelId', undefined),
    )

    // Act
    const validation = act(() => formHolder.current!.validateFields())

    // Assert
    await expect(validation).rejects.toMatchObject({
      errorFields: [{ name: ['channelId'], errors: ['Choose a channel.'] }],
    })
  })
})
