import type { McpToolDefinition } from '../types.js';
import { definitions as portfolios } from './portfolios.js';
import { definitions as programs } from './programs.js';
import { definitions as projectLifecycles } from './project-lifecycles.js';
import { definitions as projects } from './projects.js';
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
