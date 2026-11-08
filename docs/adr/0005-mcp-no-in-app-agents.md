# 0005 MCP server in the app, no in-app agents

**Status:** Accepted, 2026-11-08

## Context
Planning menus and filling a shop cart are done by AI agents in Claude Desktop. Today the data is copied between chats by hand.

## Decision
- The app hosts an MCP server at `/mcp` using the official C# SDK (`ModelContextProtocol.AspNetCore`, Streamable HTTP).
- MCP clients authenticate with personal access tokens created in the UI (one per device or agent, revocable).
- MCP tools reuse the same module services as the REST API. No business logic in tool classes.
- Agent writes to the plan are stored as *proposed* until a user accepts them.
- Server instructions tell agents about the household content language (see 0010).
- The app contains no LLM calls and no API keys for model providers.
- The cart run happens in Claude with a browser agent. The app provides the order and stores the per-item report. No shop-specific code.
- Reusable agent skills and prompts live in the repo.

## Consequences
- No AI cost or keys in the app. Anyone forking it can use any MCP client.
- Only clients that can reach the app on the home network can use MCP (see 0006). Claude Desktop qualifies; claude.ai and the Claude mobile app do not.
