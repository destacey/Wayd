---
"@wayd/mcp": minor
---

Arguments that pick from a fixed set now take names, as the API does, instead of integer codes: `status` on the portfolio, program, project, strategic initiative and task list tools (`Active`, `Completed`, …), `role` on the project tools (`Sponsor`, `Owner`, `Manager`, `Member`, `Assignee`), `state` on `ProjectLifecycles_GetProjectLifecycles`, `statusCategory` on the product, version, release and release package lists (`Proposed`, `Active`, `Done`, `Removed`), the environment `category` on the deployment environment tools and `environmentCategory` on `Deployments_GetDeployments` (`Development`, `Testing`, `Staging`, `Production`, `Other`), and `strength` on `Products_AddDependency` and `Products_ChangeDependencyTerms` (`Hard`, `Soft`). A call that still sends an integer is refused by validation. `StrategicInitiatives_GetKpis` now describes `targetDirection` as `Increase` or `Decrease`, as it is returned.
