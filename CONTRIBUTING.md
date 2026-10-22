# Contributing to Wayd

Thank you for your interest in contributing to Wayd! We welcome contributions from the community to improve the project and make it better for everyone.

## How to Contribute

### 1. Reporting Issues
If you encounter a bug, have a question, or want to suggest an enhancement, please [create an issue](https://github.com/destacey/Wayd/issues). When creating an issue:
- Provide a clear and descriptive title.
- Explain the issue in detail, including steps to reproduce if applicable.
- Attach relevant screenshots or code snippets if necessary.

### 2. Suggesting Features
We’re always looking for ways to improve Wayd. If you have an idea for a new feature:
- Check if it’s already been suggested by browsing existing issues.
- If it hasn’t been suggested, [open a new issue](https://github.com/destacey/Wayd/issues) and describe your idea clearly.

### 3. Submitting Code Changes
#### Fork and Clone the Repository
Non-maintainers will need to fork the repository before making changes. Pull requests are required for all contributions.
1. Fork the repository to your own GitHub account.
2. Clone your fork locally:
   ```bash
   git clone https://github.com/your-username/Wayd.git
   cd Wayd
   ```

#### Set Up Your Environment
Follow the [Quick Start](README.md#-quick-start) in the README, or the full [Prerequisites](docs/contributing/prerequisites.mdx) and [Installation](docs/contributing/installation.mdx) guides, to get the project running locally. Enable the repository's git hooks once per clone:
```bash
git config core.hooksPath .githooks
```

#### Create a Branch
Create a new branch for your changes:
```bash
git checkout -b feat/123-short-summary
```
Name it `<type>/<issue>-<short-summary>` — see [Branches and Commits](docs/contributing/git-workflow.mdx).

#### Make Your Changes
Make your changes to the codebase. Ensure that:
- Your code adheres to the project’s coding standards and style.
- You write tests for any new functionality.

#### Run Tests
Run the checks that cover your change to ensure it doesn’t break anything. The full list is under "Before You Finish" in [AGENTS.md](AGENTS.md#before-you-finish); the usual ones are:
```bash
# Backend - the integration suites start SQL Server containers, so Docker must be running
dotnet test Wayd.slnx

# Client
cd Wayd.Web/src/wayd.web.reactclient
npm run typecheck
npm run lint
npm test
```
A change to the `@wayd/mcp` package (`Wayd.Web/src/Wayd.Mcp`) also needs `npm test` there and a changeset — see [MCP Server](docs/contributing/mcp-server.mdx).

#### Commit Your Changes
Commit your changes with a meaningful commit message:
```bash
git add .
git commit -m "feat(ppm): add a short summary of the change (#123)"
```
Follow the `type(scope): summary (#issue)` format in [Branches and Commits](docs/contributing/git-workflow.mdx). To close an issue when the pull request merges, add `Fixes #<issue-number>` to the pull request description.

#### Push Your Changes
Push your branch to your fork:
```bash
git push origin feat/123-short-summary
```

#### Create a Pull Request
1. Go to the [original repository](https://github.com/destacey/Wayd) on GitHub.
2. Click the **New Pull Request** button.
3. Select your branch and provide a detailed description of your changes, including links to any related issues.

### 4. Reviewing and Feedback
Your pull request will be reviewed by the maintainers. Please be prepared to:
- Make changes if requested.
- Discuss your implementation or suggest alternatives.

## Code Style and Guidelines
To maintain consistency across the project:
- Follow the existing coding conventions in the project.
- Use meaningful variable and function names.
- Write clear, concise comments where necessary.

## Getting Help
If you need help while contributing, feel free to reach out by:
- Commenting on an existing issue related to your question.
- Opening a new issue with your question or concern.

Thank you for contributing to Wayd! Together, we can build something amazing.
