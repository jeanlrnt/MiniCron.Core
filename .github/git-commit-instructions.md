The commit message should follow the conventional commit format. Here is an example of a well-structured commit message:
<type>(<scope>): <subject> - <file1>, <file2>, ...

Where:
- type: The type of change being made
- scope: The scope of the change (optional)
- subject: A brief description of the change
- file1, file2, ... : A comma-separated list of files affected by the commit

Example types:
- feat: A new feature
- fix: A bug fix
- docs: Documentation changes
- style: Code style changes (formatting, missing semi-colons, etc.)
- refactor: Code changes that neither fix a bug nor add a feature
- test: Adding or updating tests
- chore: Changes to the build process or auxiliary tools and libraries
- perf: Performance improvements
- ci: Continuous Integration changes

Scope is optional and can be anything specifying the area of the codebase affected (e.g., component name, file name, etc.).
Subject should be a brief description of the change, written in imperative mood (e.g., "fix bug" not "fixed bug" or "fixes bug").

Example commit messages :
- feat(auth): Implement OAuth2 login flow - auth.js, oauth.js
- fix(api): Resolve null pointer exception in user endpoint - api.js
- docs(readme): Update installation instructions - README.md
- style(lint): Apply ESLint rules to codebase - *.js
- refactor(database): Optimize query performance for user retrieval - database.js
- test(auth): Add unit tests for login functionality - auth.test.js
- chore(deps): Update dependencies to latest versions - package.json, package-lock.json
- perf(cache): Improve caching mechanism for faster data retrieval - cache.js
- ci(github-actions): Add workflow for automated testing - .github/workflows/test.yml