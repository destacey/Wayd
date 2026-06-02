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
          type: 'doc',
          id: 'user-guide/organizations/index',
          label: 'Organizations',
        },
        {
          type: 'category',
          label: 'Planning',
          link: {type: 'doc', id: 'user-guide/planning/index'},
          items: [
            'user-guide/planning/planning-intervals',
            'user-guide/planning/sprints',
            'user-guide/planning/risks',
            'user-guide/planning/roadmaps',
            'user-guide/planning/planning-poker',
          ],
        },
        {
          type: 'category',
          label: 'Work Management',
          link: {type: 'doc', id: 'user-guide/work-management/index'},
          items: [
            'user-guide/work-management/work-items',
            'user-guide/work-management/work-configuration',
            'user-guide/work-management/cycle-time-report',
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
          ],
        },
        {
          type: 'doc',
          id: 'user-guide/strategic-management/index',
          label: 'Strategic Management',
        },
        {
          type: 'doc',
          id: 'user-guide/settings/index',
          label: 'Administration',
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
            'contributing/local-development',
          ],
        },
        {
          type: 'category',
          label: 'Development Guide',
          items: [
            'contributing/architecture',
            'contributing/coding-standards',
            'contributing/testing',
            'contributing/adding-a-feature',
            'contributing/database',
            'contributing/frontend',
            'contributing/api',
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
