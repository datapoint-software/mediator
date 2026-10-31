# Claude

## Mediator

This project is a NuGet package that implements the Mediator pattern. It lets components send requests and publish notifications through a single mediator, instead of referencing each other's handlers directly.

The package will be used in Datapoint's existing and future projects, so it needs to be easy to adopt and stable over time.

## Branches, commits and merge requests

- Write branch names, commit messages and merge requests in English.
- `main` is the only long-lived branch.
- Name branches following [Conventional Branch 1.1.0](https://conventionalbranch.org/): `<type>/<description>`, using lowercase letters, digits and hyphens only.
- Write commit messages following [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `<type>[optional scope][!]: <description>`, with an optional body and footers.
- Commit often, one commit per self-contained change.
- Do not include attribution trailers, such as `Co-Authored-By`, in commit messages.
- Do not include attribution lines, such as "Generated with Claude Code", in merge request descriptions.
