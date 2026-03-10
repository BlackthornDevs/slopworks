# GitHub automation for slopworks

This document explains the CI/CD automation added to this repo.

## What runs on every pull request

When you open a PR targeting `master`, two automated reviews fire:

**Claude code review** (`pr-review.yml`): Reviews the diff against slopworks-specific rules. Checks for hard rule violations (ScriptableObject mutation, missing server authority guards, RPC vs SyncVar misuse, GetComponent in Update, cross-scene references), networking correctness, performance concerns, and code quality. Posts a structured comment with blocking issues and suggestions.

**CodeQL security scan** (`codeql.yml`): Static analysis for C# security vulnerabilities. Runs in parallel with the Claude review. Also runs on pushes to `master`, `joe/main`, and `kevin/main`, and on a weekly Monday schedule.

**Copilot code review**: Enabled in repo Settings (not a workflow file). Reviews the same diff from Copilot's perspective.

## What runs when an issue is labeled `copilot`

`issue-copilot.yml` assigns the issue to the Copilot coding agent, which will open a PR implementing the issue autonomously.

## What runs when a PR merges to master

`post-merge.yml` POSTs the merge details to the houseofjawn dashboard. This closes any matching Board item and sends a Telegram notification. Non-fatal if the dashboard is unreachable.

## Secrets

Two secrets are set in repo settings:
- `WEBHOOK_SECRET` — authenticates the post-merge webhook call
- `CLAUDE_CODE_OAUTH_TOKEN` — authenticates the Claude code review action

## Manual steps

Enable Copilot code review: repo **Settings → Copilot → Code review → Enable**.
