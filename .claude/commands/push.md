# Push Changes

Stage, commit, and push all current changes to the remote branch with a descriptive commit message.

---

## Steps

### 1. Gather context (run in parallel)
- `git status` — see all modified and untracked files
- `git diff` — review the actual changes
- `git log --oneline -5` — learn the commit message style used in this repo

### 2. Draft a commit message
- First line: short summary (≤ 72 chars), following the repo's existing style (read from `git log --oneline -5`)
- Body: bullet points grouped by area — one line per meaningful change
- End with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`

### 3. Commit and push
```bash
# Stage only files shown in git status — do NOT use -A blindly; skip .env / credentials
git add <files from git status>
git commit -m "<message as heredoc>"
git push origin <current-branch>
```

Use a `<<'EOF' ... EOF` heredoc for the commit message to preserve formatting.

---

## Rules
- Never use `--force` or `--no-verify`
- Never commit `.env`, credentials, or secrets — warn the user if any are staged
- Always push to the **current branch** (not main)
- If there is nothing to commit, say so and stop
