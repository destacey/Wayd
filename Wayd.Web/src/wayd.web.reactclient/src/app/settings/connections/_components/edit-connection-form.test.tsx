import {
  AzureDevOpsConnectionDetailsDto,
  AzureOpenAIConnectionDetailsDto,
  ConnectionDetailsDto,
  EntraConnectionDetailsDto,
  UpdateAzureDevOpsConnectionRequest,
  UpdateAzureOpenAIConnectionRequest,
  UpdateEntraConnectionRequest,
  UpdateWorkdayConnectionRequest,
  WorkdayConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { buildRequest, seedFormValues } from './edit-connection-form'

// The API returns a fixed-width placeholder in place of every stored secret.
const MASK = '********'

const connector = (name: string) => ({ id: 1, name }) as never

const azdoConnection = {
  id: 'a1',
  name: 'Acme Boards',
  description: 'Original',
  connector: connector('Azure DevOps'),
  configuration: { organization: 'acme-org', personalAccessToken: MASK },
} as unknown as AzureDevOpsConnectionDetailsDto

const azureOpenAIConnection = {
  id: 'a2',
  name: 'Acme AI',
  connector: connector('Azure OpenAI'),
  configuration: {
    baseUrl: 'https://ai.acme.example',
    deploymentName: 'acme-deployment',
    apiKey: MASK,
  },
} as unknown as AzureOpenAIConnectionDetailsDto

const entraConnection = {
  id: 'a3',
  name: 'Acme Entra',
  connector: connector('Entra'),
  configuration: {
    tenantId: 'acme-tenant',
    clientId: 'acme-client',
    clientSecret: MASK,
  },
} as unknown as EntraConnectionDetailsDto

const workdayConnection = {
  id: 'a4',
  name: 'Acme Workday',
  connector: connector('Workday'),
  configuration: {
    wsdlUrl: 'https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl',
    isuUsername: 'wayd_isu@acme_corp',
    isuPassword: MASK,
  },
} as unknown as WorkdayConnectionDetailsDto

const cases: {
  label: string
  connection: ConnectionDetailsDto
  secretField: string
}[] = [
  {
    label: 'Azure DevOps',
    connection: azdoConnection,
    secretField: 'personalAccessToken',
  },
  {
    label: 'Azure OpenAI',
    connection: azureOpenAIConnection,
    secretField: 'apiKey',
  },
  { label: 'Entra', connection: entraConnection, secretField: 'clientSecret' },
  {
    label: 'Workday',
    connection: workdayConnection,
    secretField: 'isuPassword',
  },
]

describe('edit-connection-form', () => {
  describe('seedFormValues', () => {
    it.each(cases)(
      'does not seed the $label secret from the API response',
      ({ connection, secretField }) => {
        const values = seedFormValues(connection) as Record<string, unknown>

        // Seeding would put the mask in the input, which the admin would then submit
        // back as the new credential.
        expect(values[secretField]).toBeUndefined()
      },
    )

    it('still seeds the non-secret fields', () => {
      const values = seedFormValues(azdoConnection)

      expect(values.name).toBe('Acme Boards')
      expect(values.organization).toBe('acme-org')
    })
  })

  describe('buildRequest', () => {
    it.each(cases)(
      'omits the $label secret when the input is left blank',
      ({ connection, secretField }) => {
        const values = seedFormValues(connection)

        const request = buildRequest(connection, values) as unknown as Record<
          string,
          unknown
        >

        expect(request[secretField]).toBeUndefined()
      },
    )

    it.each(cases)(
      'omits the $label secret when the input holds only whitespace',
      ({ connection, secretField }) => {
        const values = {
          ...seedFormValues(connection),
          [secretField]: '   ',
        }

        const request = buildRequest(connection, values) as unknown as Record<
          string,
          unknown
        >

        expect(request[secretField]).toBeUndefined()
      },
    )

    it.each(cases)(
      'sends the $label secret when the admin enters a new one',
      ({ connection, secretField }) => {
        const rotated = 'rotated-secret-value'
        const values = {
          ...seedFormValues(connection),
          [secretField]: rotated,
        }

        const request = buildRequest(connection, values) as unknown as Record<
          string,
          unknown
        >

        expect(request[secretField]).toBe(rotated)
      },
    )

    it('preserves the non-secret fields it is asked to change', () => {
      const values = {
        ...seedFormValues(azdoConnection),
        description: 'A new description',
      }

      const request = buildRequest(
        azdoConnection,
        values,
      ) as UpdateAzureDevOpsConnectionRequest

      expect(request.description).toBe('A new description')
      expect(request.organization).toBe('acme-org')
    })

    it('carries the polymorphic discriminator for each connector', () => {
      expect(
        (buildRequest(azdoConnection, seedFormValues(azdoConnection)) as never)[
          '$type' as never
        ],
      ).toBe('azure-devops')
      expect(
        (
          buildRequest(
            azureOpenAIConnection,
            seedFormValues(azureOpenAIConnection),
          ) as UpdateAzureOpenAIConnectionRequest & { $type: string }
        ).$type,
      ).toBe('azure-openai')
      expect(
        (
          buildRequest(
            entraConnection,
            seedFormValues(entraConnection),
          ) as UpdateEntraConnectionRequest & { $type: string }
        ).$type,
      ).toBe('entra')
      expect(
        (
          buildRequest(
            workdayConnection,
            seedFormValues(workdayConnection),
          ) as UpdateWorkdayConnectionRequest & { $type: string }
        ).$type,
      ).toBe('workday')
    })

    it('returns null for a connector with no registered edit shape', () => {
      const unknown = {
        ...azdoConnection,
        connector: connector('Nonexistent'),
      } as ConnectionDetailsDto

      expect(buildRequest(unknown, seedFormValues(unknown))).toBeNull()
    })
  })
})
