import type { McpToolDefinition } from '../types.js';
import { definitions as portfolios } from './portfolios.js';
import { definitions as programs } from './programs.js';
import { definitions as projectLifecycles } from './project-lifecycles.js';
import { definitions as projects } from './projects.js';
import { definitions as releases } from './releases.js';
import { definitions as versions } from './versions.js';
import { definitions as releasePackages } from './release-packages.js';
import { definitions as deployments } from './deployments.js';
import { definitions as deliveryEnvironments } from './delivery-environments.js';
import { definitions as products } from './products.js';
import { definitions as productCatalogConfig } from './product-catalog-config.js';
import { definitions as projectHealthChecks } from './project-health-checks.js';
import { definitions as projectScores } from './project-scores.js';
import { definitions as strategicInitiatives } from './strategic-initiatives.js';
import { definitions as lifecycleTransitions } from './lifecycle-transitions.js';
import { definitions as recordManagement } from './record-management.js';
import { definitions as roadmaps } from './roadmaps.js';
import { definitions as planningIntervals } from './planning-intervals.js';
import { definitions as objectiveHealthChecks } from './objective-health-checks.js';
import { definitions as storyMaps } from './story-maps.js';
import { definitions as tasks } from './tasks.js';
import { definitions as teams } from './teams.js';
import { definitions as users } from './users.js';

export const toolDefinitionMap: Map<string, McpToolDefinition> = new Map([
  ...portfolios,
  ...programs,
  ...projectLifecycles,
  ...projects,
  ...releases,
  ...versions,
  ...releasePackages,
  ...deployments,
  ...deliveryEnvironments,
  ...products,
  ...productCatalogConfig,
  ...projectHealthChecks,
  ...projectScores,
  ...strategicInitiatives,
  ...lifecycleTransitions,
  ...recordManagement,
  ...roadmaps,
  ...planningIntervals,
  ...objectiveHealthChecks,
  ...storyMaps,
  ...tasks,
  ...teams,
  ...users,
]);
