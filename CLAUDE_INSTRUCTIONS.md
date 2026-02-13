# Claude Code Instructions for ExileCore MCP

Copy the relevant sections below into your project's `CLAUDE.md` file.

## Permissions

### Screenshot Capture (Always Allowed)
Claude has standing permission to take screenshots at any time to analyze screen and UI state. No need to ask before capturing. Use the `mcp__exile-api-eval__screenshot` tool — it captures just the PoE game window as PNG and returns the image directly. No parameters needed.

Use screenshots proactively when:
- Debugging UI issues or visual state
- Verifying that changes look correct in-game
- The user asks about what's on screen
- Understanding game context would help complete a task better
