---
"@wayd/mcp": minor
---

Add `DeploymentEnvironments_Delete`, which permanently deletes an environment and every deployment into it, with their status history. It needs the new environment Delete permission, plus the delivery Delete permission when the environment has deployments. Retiring an environment is still the way to take it out of use while keeping its deployments.
