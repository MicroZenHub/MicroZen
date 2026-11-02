# Must-Fix #80 — README claims AES-512 (does not exist)

GitHub issue: [MicroZenHub/MicroZen#80](https://github.com/MicroZenHub/MicroZen/issues/80)

Target file: `README.md` (line 17)

## 1. Problem (confirmed against actual code)

`README.md:17` advertises "AES-512" encryption:

```markdown
- **Comprehensive Security**: Ensure data integrity and confidentiality with robust authentication mechanisms and secure AES-512 encrypting of sensitive data.
```

AES has no 512-bit variant — the cipher tops out at a 256-bit key. The doc claim
is impossible and should read AES-256.

## 2. Fix

One-line edit on `README.md:17`. Change `AES-512` to `AES-256`.

Before:

```markdown
- **Comprehensive Security**: Ensure data integrity and confidentiality with robust authentication mechanisms and secure AES-512 encrypting of sensitive data.
```

After:

```markdown
- **Comprehensive Security**: Ensure data integrity and confidentiality with robust authentication mechanisms and secure AES-256 encrypting of sensitive data.
```

## 3. Test plan

- `grep -rn "AES-512" README.md` returns nothing (no matches anywhere in the
  README).
- CI format/lint gate green (from `AGENTS.md`): `npx nx format:check` and
  `npx nx affected -t lint test build`. `scripts/check.sh` runs the full gate.
  Note: `.editorconfig` keeps trailing whitespace on `*.md`, so leave the rest of
  the line untouched.

## 4. Note

This change belongs in the **same PR as #46** — both touch the encryption story,
and the #46 spec already calls for correcting this README line. The #46
implementer will make this edit. This spec exists to track the doc bug under its
own issue number; it is not a separate PR.

## 5. Acceptance

The PR into `epic/must-have-fixes` (the #46 PR) has **green CI checks** and the
README no longer contains `AES-512`.
