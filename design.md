# ExileApi MCP Server Plugin Design

## Overview

MCP server exposing ExileApi data to Claude via reflection and Dynamic LINQ.

- **Transport**: HTTP (`http://localhost:5099/mcp`)
- **Protocol**: JSON-RPC 2.0 per [MCP Spec 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25)

---

## Tools (3 total)

| Tool | Purpose |
|------|---------|
| `explore` | Discover what's available - browse objects, types, search |
| `eval` | Read values, call methods, access anything by path |
| `query` | Dynamic LINQ for filtering/transforming collections |

---

## `explore` - Discovery Tool

Browse the object graph and type system. Single tool for all discovery needs.

```json
{
  "name": "explore",
  "description": "Discover available data. Browse live objects, type definitions, or search by pattern.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "target": {
        "type": "string",
        "description": "What to explore: path like 'Player.Components', type like 'type:Life', or search like 'search:*Monster*'"
      },
      "depth": {
        "type": "integer",
        "default": 1,
        "description": "How deep to expand (0=summary, 1=members, 2+=nested)"
      },
      "includePrivate": {
        "type": "boolean",
        "default": true
      },
      "limit": {
        "type": "integer",
        "default": 50,
        "description": "Max items for collections/search results"
      }
    }
  }
}
```

### Examples

```javascript
// List all root objects
explore({ target: "" })
// → GameController, Player, Entities, IngameState, IngameUi, Camera, Files, Memory

// Browse Player
explore({ target: "Player", depth: 1 })
// → Shows all Player fields/properties with types and current values

// Browse Player's components
explore({ target: "Player.Components", depth: 2 })
// → Shows component dictionary with each component's members

// Explore a specific type
explore({ target: "type:ExileCore.PoEMemory.Components.Life" })
// → Shows type info: fields, properties, methods (including private)

// Search for types
explore({ target: "search:*Monster*" })
// → Lists all types matching pattern

// Search for members
explore({ target: "search:CurHP" })
// → Lists all fields/properties named CurHP across all types

// Explore collection item
explore({ target: "Entities[0]", depth: 2 })
// → First entity with full details

// Explore struct with memory layout
explore({ target: "type:ActorAnimationStageOffsets" })
// → Shows StructLayout, FieldOffset attributes, sizes

// Explore enum with flags
explore({ target: "type:MonsterRarity" })
// → Shows enum values, [Flags] attribute if present

// Search for offset structures
explore({ target: "search:*Offsets" })
// → Lists all types ending with "Offsets"
```

### Response Format

**Live object response:**
```json
{
  "path": "Player",
  "type": "ExileCore.PoEMemory.MemoryObjects.Entity",
  "value": "Entity (Id=1234)",
  "members": [
    {
      "name": "Id",
      "kind": "property",
      "type": "UInt32",
      "value": 1234,
      "access": "public"
    },
    {
      "name": "Position",
      "kind": "property",
      "type": "Vector3",
      "value": { "X": 100.5, "Y": 200.3, "Z": 0 },
      "access": "public"
    },
    {
      "name": "_components",
      "kind": "field",
      "type": "Dictionary<String, Component>",
      "count": 12,
      "access": "private"
    }
  ],
  "methods": [
    {
      "name": "GetComponent<T>",
      "signature": "T GetComponent<T>()",
      "access": "public"
    }
  ]
}
```

**Type info response (with attributes):**
```json
{
  "type": "GameOffsets.ActorAnimationStageOffsets",
  "kind": "struct",
  "attributes": [
    {
      "name": "StructLayout",
      "args": {
        "LayoutKind": "Explicit",
        "Pack": 1
      }
    }
  ],
  "size": 24,
  "members": [
    {
      "name": "StageStart",
      "kind": "field",
      "type": "Single",
      "access": "public",
      "attributes": [
        { "name": "FieldOffset", "args": { "offset": "0x7FC7A0F2" } }
      ],
      "offset": "0x7FC7A0F2"
    },
    {
      "name": "ActorAnimationListIndex",
      "kind": "field",
      "type": "Int32",
      "access": "public",
      "attributes": [
        { "name": "FieldOffset", "args": { "offset": "0x7F136DAB" } }
      ],
      "offset": "0x7F136DAB"
    },
    {
      "name": "StageName",
      "kind": "field",
      "type": "NativeUtf8Text",
      "access": "public",
      "attributes": [
        { "name": "FieldOffset", "args": { "offset": "0x7F78A0DE" } }
      ],
      "offset": "0x7F78A0DE"
    }
  ]
}
```

**Attributes exported:**
- `[StructLayout]` - LayoutKind (Sequential, Explicit, Auto), Pack, Size, CharSet
- `[FieldOffset]` - Offset value (hex formatted)
- `[MarshalAs]` - UnmanagedType, SizeConst, etc.
- `[Obsolete]` - Message, IsError
- `[Flags]` - For enums
- Custom attributes - Name + all properties

---

## `eval` - Evaluation Tool

Read any value or call any method. Supports dot-paths, indexers, method calls, generics.

```json
{
  "name": "eval",
  "description": "Evaluate an expression - read values, call methods, access nested properties.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "expr": {
        "type": "string",
        "description": "Expression to evaluate. Supports: dot-paths, indexers [], method calls (), generic methods <T>"
      },
      "depth": {
        "type": "integer",
        "default": 2,
        "description": "Serialization depth for result"
      }
    },
    "required": ["expr"]
  }
}
```

### Expression Syntax

| Syntax | Example |
|--------|---------|
| Property | `Player.Position` |
| Nested | `Player.Position.X` |
| Field (private) | `Player._components` |
| Index (int) | `Entities[0]` |
| Index (string) | `Player.Components["Life"]` |
| Method | `Player.IsValid()` |
| Method + args | `IngameState.GetZone(123)` |
| Generic method | `Player.GetComponent<Life>()` |
| Chained | `Player.GetComponent<Life>().CurHP` |
| Null-safe | `Player?.GetComponent<Life>()?.CurHP` |

### Examples

```javascript
// Simple property
eval({ expr: "Player.Position" })
// → { "X": 100.5, "Y": 200.3, "Z": 0 }

// Nested with method call
eval({ expr: "Player.GetComponent<Life>().CurHP" })
// → 4523

// Private field access
eval({ expr: "GameController._entityCache", depth: 1 })
// → { count: 1523, ... }

// Collection indexing
eval({ expr: "Entities[0].RenderName" })
// → "Hillock"

// Complex chain
eval({ expr: "IngameState.IngameUi.InventoryPanel.Items[0].Item.Path" })
// → "Metadata/Items/..."

// Check condition
eval({ expr: "Player.GetComponent<Life>().HPPercentage > 0.5" })
// → true
```

---

## `query` - Dynamic LINQ Tool

Full [Dynamic LINQ](https://dynamic-linq.net/) support for querying collections.

```json
{
  "name": "query",
  "description": "Execute Dynamic LINQ query on collections. Supports Where, Select, OrderBy, GroupBy, Any, All, Count, First, etc.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "from": {
        "type": "string",
        "description": "Source collection path (e.g., 'Entities', 'Player.Components.Values')"
      },
      "where": {
        "type": "string",
        "description": "Filter predicate"
      },
      "select": {
        "type": "string",
        "description": "Projection expression"
      },
      "orderBy": {
        "type": "string",
        "description": "Sort expression"
      },
      "take": {
        "type": "integer",
        "default": 100
      },
      "skip": {
        "type": "integer",
        "default": 0
      }
    },
    "required": ["from"]
  }
}
```

### Examples

```javascript
// Get all monsters
query({
  from: "Entities",
  where: "Type.ToString() == \"Monster\"",
  select: "new { Id, RenderName, Position }",
  take: 20
})

// Find rare/unique items on ground
query({
  from: "IngameState.IngameUi.ItemsOnGroundLabels",
  where: "ItemOnGround != null && ItemOnGround.Item.Rarity >= 2",
  select: "new { ItemOnGround.Item.Path, ItemOnGround.Item.Rarity, Label.GetClientRect() }"
})

// Get player buffs with time remaining
query({
  from: "Player.GetComponent<Buffs>().BuffsList",
  where: "Timer > 0",
  select: "new { Name, Timer, Charges }",
  orderBy: "Timer desc"
})

// Count entities by type
query({
  from: "Entities",
  select: "Type.ToString()",
  // returns grouped counts
})

// Find entities near player
query({
  from: "Entities",
  where: "Distance < 50 && IsHostile",
  orderBy: "Distance",
  take: 10
})

// Check if any portal exists
query({
  from: "Entities",
  where: "Path.Contains(\"Portal\")"
  // returns matching entities
})
```

### Dynamic LINQ Features

| Feature | Syntax |
|---------|--------|
| Comparison | `==`, `!=`, `<`, `>`, `<=`, `>=` |
| Logical | `&&`, `\|\|`, `!` |
| String | `.Contains()`, `.StartsWith()`, `.EndsWith()` |
| Null check | `!= null`, `== null` |
| Arithmetic | `+`, `-`, `*`, `/`, `%` |
| Conditional | `x ? y : z`, `iif(x, y, z)` |
| Type check | `is`, `as` |
| New object | `new { Prop1, Prop2 }`, `new { Name = x.Prop }` |
| Property | `it.PropertyName` or just `PropertyName` |
| Method | `.ToString()`, `.GetHashCode()` |
| Null-safe | `np(x.y.z, defaultValue)` |

---

## Root Objects

Available root objects for `explore` and `eval`:

| Root | Description |
|------|-------------|
| `GameController` | Main game controller |
| `Player` | Current player entity |
| `Entities` | All loaded entities (ICollection) |
| `IngameState` | Current game state |
| `IngameUi` | UI elements |
| `Camera` | Game camera |
| `Files` | Game data files |
| `Memory` | Memory access |

---

## Protocol

### Initialize

```json
// Request
{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
    "protocolVersion": "2025-11-25",
    "clientInfo": { "name": "claude-code" }
}}

// Response
{ "jsonrpc": "2.0", "id": 1, "result": {
    "protocolVersion": "2025-11-25",
    "capabilities": { "tools": {} },
    "serverInfo": { "name": "exile-api-mcp", "version": "1.0.0" }
}}
```

### Tools List

```json
// Request
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }

// Response
{ "jsonrpc": "2.0", "id": 2, "result": { "tools": [...] }}
```

### Tool Call

```json
// Request
{ "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {
    "name": "eval",
    "arguments": { "expr": "Player.GetComponent<Life>().CurHP" }
}}

// Response
{ "jsonrpc": "2.0", "id": 3, "result": {
    "content": [{ "type": "text", "text": "4523" }]
}}
```

---

## Implementation

### File Structure

```
ExportGlobals/
├── ExportGlobals.cs           # Plugin entry, starts MCP server
├── ExportGlobals.csproj
├── ExportGlobalsSettings.cs
├── Global.cs                  # Root object registry
├── Mcp/
│   ├── McpServer.cs           # HTTP server + JSON-RPC routing
│   ├── Tools/
│   │   ├── ExploreTool.cs     # explore implementation
│   │   ├── EvalTool.cs        # eval implementation
│   │   └── QueryTool.cs       # query implementation
│   └── Protocol/
│       ├── JsonRpc.cs         # Message types
│       └── McpMessages.cs     # MCP-specific messages
├── Reflection/
│   ├── ExpressionEvaluator.cs # Parse & evaluate expressions
│   ├── TypeExplorer.cs        # Type metadata extraction
│   └── ObjectSerializer.cs    # Reflection-based JSON serialization
└── design.md
```

### Dependencies

```xml
<PackageReference Include="System.Linq.Dynamic.Core" Version="1.4.5" />
```

### Key Implementation Notes

**ExpressionEvaluator** - Parses expressions like `Player.GetComponent<Life>().CurHP`:
- Tokenize into segments (property, indexer, method call)
- Resolve each segment via reflection
- Handle generics by searching loaded assemblies for type
- Support null-conditional (`?.`) by checking null at each step

**TypeExplorer** - Extracts type metadata including attributes:
```csharp
public class TypeExplorer
{
    public TypeInfo GetTypeInfo(Type type, bool includePrivate)
    {
        var info = new TypeInfo
        {
            Name = type.Name,
            FullName = type.FullName,
            Kind = GetKind(type), // class, struct, enum, interface
            Attributes = GetAttributes(type),
            Size = GetStructSize(type)
        };

        var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
        if (includePrivate) flags |= BindingFlags.NonPublic;

        info.Members = type.GetFields(flags)
            .Select(f => new MemberInfo
            {
                Name = f.Name,
                Kind = "field",
                Type = FormatType(f.FieldType),
                Access = GetAccess(f),
                Attributes = GetAttributes(f),
                Offset = GetFieldOffset(f) // From [FieldOffset] attribute
            })
            .Concat(type.GetProperties(flags).Select(...))
            .ToList();

        return info;
    }

    private List<AttributeInfo> GetAttributes(MemberInfo member)
    {
        return member.GetCustomAttributesData()
            .Select(a => new AttributeInfo
            {
                Name = a.AttributeType.Name.Replace("Attribute", ""),
                Args = a.ConstructorArguments
                    .Concat(a.NamedArguments)
                    .ToDictionary(...)
            })
            .ToList();
    }

    private string GetFieldOffset(FieldInfo field)
    {
        var attr = field.GetCustomAttribute<FieldOffsetAttribute>();
        return attr != null ? $"0x{attr.Value:X}" : null;
    }
}
```

**ObjectSerializer** - Converts objects to JSON:
- Handles circular references (track visited objects)
- Depth limiting
- Private member access via `BindingFlags.NonPublic`
- Special handling for collections (show count + items)
- Special handling for primitives, enums, structs

**QueryTool** - Uses Dynamic LINQ:
- Wrap collection in `AsQueryable()`
- Apply `.Where()`, `.Select()`, `.OrderBy()` etc. with string expressions
- Custom `ParsingConfig` with `ExileApiTypeProvider` to allow method calls

---

## Configuration

```json
// .mcp.json
{
  "mcpServers": {
    "exile-api": {
      "type": "http",
      "url": "http://localhost:5099/mcp"
    }
  }
}
```

Or CLI:
```bash
claude mcp add --transport http exile-api http://localhost:5099/mcp
```

---

## Security

- Localhost only (`127.0.0.1`)
- Read-only (no setters, no state modification)
- Depth limits (prevent infinite recursion)
- Collection limits (prevent memory issues)
- Timeout on long queries
