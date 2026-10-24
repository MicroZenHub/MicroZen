<!--
Thanks for contributing to MicroZen! Fill in each section below so the
reviewer (and the next contributor) has the context needed to review your
change. See the Contributing section of the README for the full workflow.
-->

## Summary / what changed

<!-- Describe what this PR changes and why. Call out anything a reviewer
should look at closely. -->

## How tested

<!-- Paste the real output (or a short summary) of the gate you ran locally,
mirroring CI. Run the three steps in order: -->

- [ ] `npx nx format:check`
- [ ] `npx nx affected -t lint test build`
- [ ] `dotnet test core/csharp/MicroZen.Core.sln`

```text
<!-- paste testing evidence here -->
```

## Linked issue

<!-- Link the issue this PR resolves so it closes automatically on merge. -->

Closes #

## Checklist

- [ ] I ran the full gate locally (`npx nx format:check`, `npx nx affected -t lint test build`, `dotnet test core/csharp/MicroZen.Core.sln`).
- [ ] I updated the documentation (README, AGENTS.md, or steering files) if the change needed it.
