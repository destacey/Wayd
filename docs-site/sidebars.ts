import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    {
      type: 'doc',
      id: 'index',
      label: 'Welcome',
    },
    {
      type: 'category',
      label: 'Getting Started',
      link: {type: 'doc', id: 'getting-started/index'},
      items: [
        'getting-started/logging-in',
        'getting-started/your-profile',
        'getting-started/navigation',
        'getting-started/mcp-server',
      ],
    },
    {
      type: 'category',
      label: 'User Guide',
      link: {type: 'doc', id: 'user-guide/index'},
      items: [
        {
          type: 'category',
          label: 'Organizations',
          link: {type: 'doc', id: 'user-guide/organizations/index'},
          items: [
            'user-guide/organizations/bulk-import',
          ],
        },
        {
          type: 'category',
          label: 'Planning',
          link: {type: 'doc', id: 'user-guide/planning/index'},
          items: [
            'user-guide/planning/planning-intervals',
            'user-guide/planning/risks',
            'user-guide/planning/roadmaps',
            'user-guide/planning/planning-poker',
            'user-guide/planning/story-maps',
            'user-guide/planning/bulk-import',
          ],
        },
        {
          type: 'category',
          label: 'Work Management',
          link: {type: 'doc', id: 'user-guide/work-management/index'},
          items: [
            'user-guide/work-management/work-items',
            'user-guide/work-management/sprints',
            'user-guide/work-management/work-configuration',
            'user-guide/work-management/cycle-time-report',
            'user-guide/work-management/backlog-health-report',
            'user-guide/work-management/work-distribution-report',
            'user-guide/work-management/delivery-forecasting',
          ],
        },
        {
          type: 'category',
          label: 'PPM',
          link: {type: 'doc', id: 'user-guide/ppm/index'},
          items: [
            'user-guide/ppm/portfolios-programs',
            'user-guide/ppm/projects',
            'user-guide/ppm/strategic-initiatives',
            'user-guide/ppm/my-projects',
            'user-guide/ppm/bulk-import',
          ],
        },
        {
          type: 'category',
          label: 'Demand Management',
          link: {type: 'doc', id: 'user-guide/demand-management/index'},
          items: [
            'user-guide/demand-management/bulk-import',
          ],
        },
        {
          type: 'doc',
          id: 'user-guide/strategic-management/index',
          label: 'Strategic Management',
        },
        {
          type: 'category',
          label: 'Product Management',
          link: {type: 'doc', id: 'user-guide/product-management/index'},
          items: [
            'user-guide/product-management/products',
            'user-guide/product-management/delivery',
            'user-guide/product-management/bulk-import',
          ],
        },
        {
          type: 'category',
          label: 'Administration',
          link: {type: 'doc', id: 'user-guide/settings/index'},
          items: [
            'user-guide/settings/permissions',
            'user-guide/settings/imports',
            'user-guide/settings/status-workflows',
            'user-guide/settings/scoring-models',
            'user-guide/settings/appearance',
          ],
        },
        {
          type: 'doc',
          id: 'user-guide/activity-log',
          label: 'Activity Log',
        },
      ],
    },
  ],

  contributingSidebar: [
    {
      type: 'category',
      label: 'Contributing',
      link: {type: 'doc', id: 'contributing/index'},
      items: [
        {
          type: 'category',
          label: 'Development Setup',
          items: [
            'contributing/prerequisites',
            'contributing/installation',
            'contributing/configuration',
            'contributing/first-run-setup',
            'contributing/entra-app-registration',
            'contributing/local-development',
          ],
        },
        {
          type: 'category',
          label: 'Development Guide',
          items: [
            'contributing/architecture',
            'contributing/domain-events',
            'contributing/coding-standards',
            'contributing/git-workflow',
            'contributing/testing',
            'contributing/adding-a-feature',
            'contributing/database',
            'contributing/frontend',
            'contributing/record-pages',
            'contributing/wayd-grid',
            'contributing/api',
            'contributing/mcp-server',
          ],
        },
        {
          type: 'category',
          label: 'Tools',
          link: {type: 'doc', id: 'contributing/tools/index'},
          items: [
            'contributing/tools/data-generation',
          ],
        },
        {
          type: 'category',
          label: 'Migration',
          items: [
            'contributing/migrating-from-moda',
          ],
        },
      ],
    },
  ],

  referenceSidebar: [
    {
      type: 'category',
      label: 'Reference',
      link: {type: 'doc', id: 'reference/index'},
      items: [
        'reference/api',
        'reference/feature-flags',
        {
          type: 'category',
          label: 'Integrations',
          link: {type: 'doc', id: 'reference/integrations/index'},
          items: [
            'reference/integrations/azure-devops',
            'reference/integrations/entra',
            'reference/integrations/workday',
            'reference/integrations/azure-openai',
          ],
        },
        'reference/technology-stack',
        'reference/attribution',
      ],
    },
    {
      type: 'category',
      label: 'AI Context',
      items: [
        'ai/domain-glossary',
      ],
    },
  ],
};

export default sidebars;
