# Agent Rules for GarmetixWebStarter

## Change Logging Requirement
**CRITICAL RULE:** After EVERY iteration of changes made to the codebase (after completing a user request or significant implementation phase), you MUST automatically append an entry to `AntigravityAIChanges.md` in the project root. 
- You do not need to ask the user for permission.
- The entry must document:
  - What was changed (high-level description)
  - Which files were modified (e.g. `[MODIFIED] file/path.cs`, `[ADDED] file/path.vue`)
  - The purpose of the changes (Why it was made)

Always perform this logging step as part of your execution wrap-up before informing the user that the task is complete.
