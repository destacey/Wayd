import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConnectorType } from '@/src/types/connectors'
import CreateConnectionForm, {
  buildCreateRequest,
  CreateConnectionFormValues,
  getDiscriminator,
} from './create-connection-form'
import type { UseModalFormOptions } from '@/src/hooks'

const mockCreate = vi.fn()
const mockVerify = vi.fn()
const mockMessage = { error: vi.fn(), success: vi.fn() }

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true, hasClaim: () => true }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

vi.mock('@/src/components/common/markdown', () => ({
  MarkdownEditor: () => null,
}))

// Form.useWatch does not refire after typing under jsdom, so validity stays false and the
// footer buttons never enable. Keep the form instance real and submit through validateFields.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({ onSubmit }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          const values = await form.validateFields()
          await onSubmit(values, form)
        },
        handleCancel: vi.fn(),
      }
    },
    // WaydGrid in the Teams setup guide reads this tuple. The mock replaces the whole module.
    useRemainingHeight: () => [vi.fn(), 600],
  }
})

vi.mock('@/src/store/features/app-integration/connections-api', () => ({
  useCreateConnectionMutation: () => [mockCreate],
  useVerifyTeamsConnectionMutation: () => [mockVerify],
  useGetConnectorsQuery: () => ({
    data: [
      {
        id: 5,
        name: 'Microsoft Teams',
        description: 'Teams',
        capabilities: [{ name: 'Communication', category: 'Communication' }],
      },
    ],
    isLoading: false,
  }),
  useGetConnectionsQuery: () => ({ data: [], isLoading: false }),
}))

describe('create-connection-form helpers', () => {
  describe('getDiscriminator', () => {
    it('should return "azure-devops" for AzureDevOps connector', () => {
      expect(getDiscriminator(ConnectorType.AzureDevOps)).toBe('azure-devops')
    })

    it('should return "azure-openai" for AzureOpenAI connector', () => {
      expect(getDiscriminator(ConnectorType.AzureOpenAI)).toBe('azure-openai')
    })

    it('should return "entra" for Entra connector', () => {
      expect(getDiscriminator(ConnectorType.Entra)).toBe('entra')
    })

    it('should return "workday" for Workday connector', () => {
      expect(getDiscriminator(ConnectorType.Workday)).toBe('workday')
    })

    it('should handle all ConnectorType enum values', () => {
      // Verify we have a discriminator for every enum value
      const connectorTypes = Object.values(ConnectorType).filter(
        (v) => typeof v === 'number',
      ) as ConnectorType[]

      connectorTypes.forEach((type) => {
        const discriminator = getDiscriminator(type)
        expect(discriminator).toBeTruthy()
        expect(typeof discriminator).toBe('string')
      })
    })

    it('should return valid discriminator strings matching backend expectations', () => {
      // These discriminators must match the JsonDerivedType attributes in the backend
      const validDiscriminators = [
        'azure-devops',
        'azure-openai',
        'entra',
        'workday',
        'teams',
      ]

      Object.values(ConnectorType)
        .filter((v) => typeof v === 'number')
        .forEach((type) => {
          const discriminator = getDiscriminator(type as ConnectorType)
          expect(validDiscriminators).toContain(discriminator)
        })
    })

    it('should throw for a connector type with no discriminator', () => {
      // A silent fallback would submit a request the backend cannot deserialize
      expect(() => getDiscriminator(999 as ConnectorType)).toThrow(
        /No request discriminator is registered/,
      )
    })
  })

  describe('buildCreateRequest', () => {
    const azdoValues: CreateConnectionFormValues = {
      name: 'Acme Boards',
      organization: 'acme-org',
      personalAccessToken: 'acme-token',
    }

    it('builds a request for Azure DevOps, whose connector type is 0', () => {
      // Arrange
      const connector = ConnectorType.AzureDevOps

      // Act
      const request = buildCreateRequest(connector, azdoValues)

      // Assert
      expect(request).not.toBeNull()
      expect(request?.$type).toBe('azure-devops')
      expect(request?.organization).toBe('acme-org')
    })

    it('returns null when no connector is chosen', () => {
      // Arrange
      const connector = null

      // Act
      const request = buildCreateRequest(connector, azdoValues)

      // Assert
      expect(request).toBeNull()
    })

    it('puts the discriminator first', () => {
      // Arrange
      const connector = ConnectorType.Entra

      // Act
      const request = buildCreateRequest(connector, {
        name: 'Acme Entra',
        tenantId: 'acme-tenant',
      })

      // Assert
      expect(Object.keys(request!)[0]).toBe('$type')
    })

    it('drops partially filled org exclusions', () => {
      // Arrange
      const values: CreateConnectionFormValues = {
        name: 'Acme Workday',
        orgExclusions: [
          { organizationTypeId: 'cost-center', organizationReference: 'CC-1' },
          { organizationTypeId: 'cost-center', organizationReference: ' ' },
        ],
      }

      // Act
      const request = buildCreateRequest(ConnectorType.Workday, values)

      // Assert
      expect(request?.orgExclusions).toEqual([
        { organizationTypeId: 'cost-center', organizationReference: 'CC-1' },
      ])
    })
  })
})

const openVerifiedTeamsForm = async () => {
  render(<CreateConnectionForm onFormCreate={vi.fn()} onFormCancel={vi.fn()} />)
  await userEvent.click(
    screen.getByRole('heading', { name: 'Microsoft Teams' }),
  )
  await userEvent.type(screen.getByLabelText('Name'), 'Ops Teams')
  await userEvent.type(screen.getByLabelText('Tenant ID'), 'tenant-1')
  await userEvent.type(screen.getByLabelText('Client ID'), 'client-1')
  await userEvent.type(screen.getByLabelText('Client Secret'), 'secret-1')
  await userEvent.click(screen.getByRole('button', { name: 'Verify' }))
  expect(
    await screen.findByRole('button', { name: 'Create' }),
  ).toBeInTheDocument()
}

describe('CreateConnectionForm Teams verification', () => {
  beforeEach(() => {
    mockCreate.mockReset().mockResolvedValue({})
    mockVerify.mockReset().mockResolvedValue({})
    mockMessage.error.mockReset()
    mockMessage.success.mockReset()
  })

  it('creates the connection without calling verify again', async () => {
    // Arrange
    await openVerifiedTeamsForm()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Create' }))

    // Assert
    await waitFor(() => expect(mockCreate).toHaveBeenCalledTimes(1))
    expect(mockVerify).toHaveBeenCalledTimes(1)
    expect(mockCreate).toHaveBeenCalledWith(
      expect.objectContaining({
        $type: 'teams',
        name: 'Ops Teams',
        tenantId: 'tenant-1',
        clientId: 'client-1',
        clientSecret: 'secret-1',
      }),
    )
  })

  it('returns to Verify when a credential changes after verification', async () => {
    // Arrange
    await openVerifiedTeamsForm()

    // Act
    await userEvent.type(screen.getByLabelText('Client Secret'), 'x')

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Verify' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Create' }),
    ).not.toBeInTheDocument()
  })

  it('shows a non-422 create failure as a save error', async () => {
    // Arrange
    mockCreate.mockResolvedValue({
      error: {
        isApiException: true,
        status: 500,
        response: { detail: 'The connection could not be saved.' },
      },
    })
    await openVerifiedTeamsForm()

    // Act
    // The save finishes after await. Keep that continuation inside act so a
    // setState that puts the failure on the verify alert is on screen.
    await act(async () => {
      screen.getByRole('button', { name: 'Create' }).click()
      await Promise.resolve()
      await Promise.resolve()
    })

    // Assert
    expect(mockMessage.error).toHaveBeenCalledWith(
      'The connection could not be saved.',
    )
    expect(
      screen.queryByText('Connection verification failed'),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create' })).toBeInTheDocument()
    expect(mockVerify).toHaveBeenCalledTimes(1)
  })
})
