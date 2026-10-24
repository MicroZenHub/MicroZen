# Spec: PR & issue templates (.github/)

**Tracking issue:** #9 (part of epic #3, Tier 2)
**Branch:** `spec/issue-9-github-templates`

## Problem

Without templates, PRs and issues arrive incomplete, giving the next agent (or
reviewer) poor context. Templates nudge agents and humans toward complete,
reviewable contributions.

## Goal

Add a PR template and issue templates that prompt for the information needed to
review changes and triage reports, consistent with the README Contributing section.

## Requirements

1. **`.github/PULL_REQUEST_TEMPLATE.md`** with sections:
   - Summary / what changed
   - How tested (reference the gate: `nx format:check`, `nx affected -t lint test build`, `dotnet test`)
   - Linked issue (`Closes #...`)
   - Checklist (ran the gate, updated docs if needed)
2. **`.github/ISSUE_TEMPLATE/`** with:
   - `bug_report.md` (or `.yml` form): steps to reproduce, expected vs actual,
     environment (OS, Node, .NET version).
   - `feature_request.md` (or `.yml` form): problem, proposed solution, alternatives.
   - Optionally `config.yml` to tune the chooser.
3. Keep wording consistent with the README Contributing section and `AGENTS.md`.

## Design notes

- YAML issue forms are preferred for structured input but Markdown templates are
  acceptable; pick one and be consistent.
- The PR "How tested" section should explicitly reference the project's gate so
  contributors paste real evidence.

## Tasks

- [ ] Add `.github/PULL_REQUEST_TEMPLATE.md`.
- [ ] Add `.github/ISSUE_TEMPLATE/` bug + feature templates (+ optional config.yml).
- [ ] Align wording with README Contributing / AGENTS.md.

## Acceptance criteria

- Templates render on GitHub when opening new PRs/issues.
- PR template prompts for testing evidence and a linked issue.

## Verification

- Opening a new PR/issue in the GitHub UI shows the templates prefilled.
